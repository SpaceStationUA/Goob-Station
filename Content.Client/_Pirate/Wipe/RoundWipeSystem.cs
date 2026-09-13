// SPDX-License-Identifier: MIT

using Content.Goobstation.Common.CCVar;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Client;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.State;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Robust.Client.UserInterface.Controls;
using Content.Client.GameTicking.Managers;
using Robust.Client.Console;
using Robust.Shared.Console;

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
    [Dependency] private readonly IStateManager _state = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IConsoleHost _console = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    private static readonly TimeSpan MaxCover = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ReleaseTime = TimeSpan.FromSeconds(1.4);
    private static readonly TimeSpan ReattachGrace = TimeSpan.FromSeconds(5);

    // Connection state lives across system re-initialization (content modules
    // re-init on every connect), otherwise a second system instance would spawn a
    // duplicate panel.
    private static bool _enabled;
    private static string _artPath = "";

    /// <summary>Whether the wipe is armed. Armed after leaving a round, so only
    /// lobby→round transitions wipe; mid-round re-attaches do nothing.</summary>
    private static bool _armed = true;
    private static RoundWipeUiPanel? _panel;
    private static TimeSpan _coverRealTime;
    private static TimeSpan _releaseElapsed;
    private static bool _release;
    private static float _seed;
    private static int _mode;

    private static TimeSpan _lastDetach;

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
        _console.AnyCommandExecuted += OnAnyCommand;
        _state.OnStateChanged += OnStateChange;

        // --connect flows switch runlevel before content systems exist, so the
        // Connecting event is missed; catch up here.
        if (_client.RunLevel is ClientRunLevel.Connecting or ClientRunLevel.Connected)
        {
            Log.Info($"[WIPE] init catch-up at runlevel {_client.RunLevel}");
            TryStartCover();
        }
    }

    private void OnStateChange(StateChangedEventArgs args)
    {
        RaisePanel();
        if (args.NewState is LobbyState && _panel != null && !_release)
        {
            // Pre-game lobby: no join pending, drop the cover and re-arm.
            StopPanel("lobby");
            _armed = true;
        }
    }

    private void OnAnyCommand(IConsoleShell shell, string command, string argStr, string[] args)
    {
        // Late-join job dialogs and the console send "joingame"; cover from that
        // exact click, before the server has processed the join.
        if (!_enabled || !_armed)
            return;

        if (command != "joingame")
            return;
        if (!_entityManager.System<ClientGameTicker>().IsGameStarted && !_release)
            return;

        TryStartCover();
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
        Log.Info($"[WIPE] runlevel {args.OldLevel} -> {args.NewLevel} armed={_armed} panel={_panel != null}");
        RaisePanel();
        if (!_enabled || !_armed)
            return;

        // Cover from the first connect attempt (right after the handshake), so the
        // whole network/content-load freeze is under the art. If the player ends up in a pregame
        // lobby, the LobbyState handler removes the panel and re-arms.
        if (args.NewLevel == ClientRunLevel.Connecting || args.NewLevel == ClientRunLevel.Connected)
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
        LayoutContainer.SetAnchorPreset(_panel, LayoutContainer.LayoutPreset.Wide);
        _ui.RootControl.AddChild(_panel);
        Log.Info($"[WIPE] cover started mode={_mode} art={_artPath}");
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        // Returned to lobby (or a new round); re-arm for the next join.
        StopPanel("roundrestart");
        _armed = true;
    }

    private void OnDetached(LocalPlayerDetachedEvent args)
    {
        _lastDetach = _timing.RealTime;
        _armed = true;
        StopPanel("detach" + _timing.RealTime);
    }

    private void OnAttached(LocalPlayerAttachedEvent args)
    {
        Log.Info($"[WIPE] attach armed={_armed} panel={_panel != null}");
        if (_panel != null)
        {
            // World is ready: start the dissolve (unless the failsafe already did).
            if (!_release)
            {
                _release = true;
                Log.Info($"[WIPE] attach -> release after {(float)(_timing.RealTime - _coverRealTime).TotalSeconds:F3}s");
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

    private void RaisePanel()
    {
        if (_panel == null)
            return;
        if (_panel.Parent != _ui.RootControl)
        {
            _panel.Orphan();
            _ui.RootControl.AddChild(_panel);
        }
        else
        {
            _panel.SetPositionLast();
        }
    }

    private void StopPanel(string reason)
    {
        if (_panel != null)
        {
            _panel.Orphan();
            _panel = null;
            Log.Info($"[WIPE] panel removed reason={reason}");
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
                StopPanel("done");
        }
    }
}
