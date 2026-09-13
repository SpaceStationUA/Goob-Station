// SPDX-License-Identifier: MIT

using Content.Goobstation.Common.CCVar;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Client;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.Client.GameTicking.Managers;

namespace Content.Client._Pirate.Wipe;

/// <summary>
///     Drives the round-start wipe: a fullscreen UI panel shows the splash art while
///     the client loads into a round (from join/connect until the player attaches),
///     then dissolves it into the live game. Also logs the load timeline when the
///     probe is active.
/// </summary>
public sealed class RoundWipeSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IBaseClient _client = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    private static readonly TimeSpan MaxCover = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ReleaseTime = TimeSpan.FromSeconds(1.4);
    private static readonly TimeSpan ReattachGrace = TimeSpan.FromSeconds(5);

    private bool _enabled;
    private string _artPath = "";

    /// <summary>Whether the wipe is armed. Armed after leaving a round, so only
    /// lobby→round transitions wipe; mid-round re-attaches do nothing.</summary>
    private bool _armed = true;

    private RoundWipeUiPanel? _panel;
    private TimeSpan _coverRealTime;
    private TimeSpan _releaseElapsed;
    private bool _release;
    private float _seed;
    private int _mode;

    private TimeSpan _lastDetach;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TickerJoinGameEvent>(OnJoinGame);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnDetached);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        _config.OnValueChanged(PirateCVars.PirateRoundWipe, v => _enabled = v, invokeImmediately: true);
        _config.OnValueChanged(PirateCVars.PirateRoundWipeArt, v => _artPath = v, invokeImmediately: true);

        _client.RunLevelChanged += OnRunLevelChanged;

        LobbyState.OnLobbyGuiReady += OnLobbyGuiReady;
    }

    private LobbyGui? _lobby;

    private void OnLobbyGuiReady(LobbyGui lobby)
    {
        _lobby = lobby;
        // Cover as soon as the player clicks Join in the lobby (the ticker event only
        // arrives after the server has processed the join, which can be seconds later).
        lobby.ReadyButton.OnPressed += _ =>
        {
            if (!_enabled || !_armed)
                return;

            var ticker = _entityManager.System<Content.Client.GameTicking.Managers.ClientGameTicker>();
            if (!ticker.IsGameStarted)
                return;

            TryStartCover();
        };
    }

    private void OnRunLevelChanged(object? sender, RunLevelChangedEventArgs args)
    {
        if (!_enabled || !_armed)
            return;

        // Direct connects (lobby disabled) have no JoinGame event; the client goes
        // straight to a world load after connecting, so cover from there.
        if (args.NewLevel == ClientRunLevel.Connected && !_config.GetCVar(CCVars.GameLobbyEnabled))
            TryStartCover();
    }

    private void OnJoinGame(TickerJoinGameEvent args)
    {
        if (!_enabled || !_armed)
            return;

        TryStartCover();
    }

    private void TryStartCover()
    {
        if (_panel != null || !TryGetArt(out var art))
            return;

        _armed = false;
        _mode = _random.Next(0, 4);
        _seed = (float) _random.NextDouble();
        _coverRealTime = _timing.RealTime;
        _releaseElapsed = TimeSpan.Zero;
        _panel = new RoundWipeUiPanel(art, _mode, _seed) { Progress = 0f };
        _ui.RootControl.AddChild(_panel);
        Log.Info($"[WIPE] cover started mode={_mode} art={_artPath}");
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        // Returned to lobby (or a new round); re-arm for the next join.
        StopPanel();
        _armed = true;
    }

    private void OnDetached(LocalPlayerDetachedEvent args)
    {
        _lastDetach = _timing.RealTime;
        _armed = true;
        StopPanel();
    }

    private void OnAttached(LocalPlayerAttachedEvent args)
    {
        if (_panel != null)
        {
            // World is ready: start the dissolve (unless the failsafe already did).
            if (!_release)
            {
                _release = true;
                Log.Info($"[WIPE] attach -> release after {_timing.RealTime - _coverRealTime:0.000}s");
            }
            return;
        }

        // No cover happened (e.g. attached without a join): fall back to starting the
        // wipe at attach, ignoring quick mid-round hops.
        if (!_enabled || !_armed)
            return;

        if (_timing.RealTime - _lastDetach < ReattachGrace)
            return;

        _armed = false;
        TryStartCover();
        if (_panel != null)
        {
            _release = true;
            Log.Info("[WIPE] attach -> release (fallback start)");
        }
    }

    private void StopPanel()
    {
        if (_panel != null)
        {
            _panel.Orphan();
            _panel = null;
            Log.Info("[WIPE] panel removed");
        }
        _release = false;
        _releaseElapsed = TimeSpan.Zero;
    }

    private bool TryGetArt(out Texture art)
    {
        art = default!;
        return _resourceCache.TryGetResource<TextureResource>(_artPath, out var res) && (art = res.Texture) != null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_panel == null)
            return;

        // state changes (combat HUD, chat) can be re-parented after us; keep the
        // wipe panel the last child of the root so it draws above everything
        if (_panel.Parent != _ui.RootControl)
        {
            _panel.Orphan();
            _ui.RootControl.AddChild(_panel);
        }
        else
        {
            _panel.SetPositionLast();
        }

        if (_release)
        {
            _releaseElapsed += TimeSpan.FromSeconds(frameTime);
        }
        else if (_timing.RealTime - _coverRealTime >= MaxCover)
        {
            _release = true;
            Log.Warning("[WIPE] failsafe release after long cover");
        }

        if (_panel != null)
        {
            _panel.Progress = _release ? MathF.Min(1f, (float) (_releaseElapsed / ReleaseTime)) : 0f;
            if (_panel.Progress >= 1f)
                StopPanel();
        }
    }
}
