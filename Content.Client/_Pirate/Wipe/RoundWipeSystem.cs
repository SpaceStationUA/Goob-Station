// SPDX-License-Identifier: MIT

using Content.Goobstation.Common.CCVar;
using Content.Shared.GameTicking;
using Robust.Client.Graphics;
using Robust.Client;
using Robust.Shared.Player;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Content.Shared.CCVar;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._Pirate.Wipe;

/// <summary>
///     Spawns the round-start wipe when the client joins a round: a fullscreen art
///     panel covers the lobby/loading screens, then a world-space overlay dissolves
///     it into the live game, hiding world-streaming glitches between join and the
///     first stable frames. Releases when the local player attaches (plus a short
///     pad), or on a failsafe timer.
/// </summary>
public sealed class RoundWipeSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IBaseClient _client = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    private bool _enabled;
    private string _artPath = "";

    /// <summary>Whether the wipe is armed. Armed after leaving a round, so only
    /// lobby→round transitions wipe; mid-round re-attaches do nothing.</summary>
    private bool _armed = true;

    private RoundWipeOverlay? _overlay;

    /// <summary>Whether a wipe was started by JoinGame (true) or is pending via attach fallback.</summary>
    private RoundWipeUiPanel? _uiPanel;

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
    }

    private void OnRunLevelChanged(object? sender, RunLevelChangedEventArgs args)
    {
        if (!_enabled || !_armed)
            return;

        // Direct connects (lobby disabled) have no JoinGame event; the freeze the wipe
        // should cover happens while connecting, so cover from the Connected runlevel.
        if (args.NewLevel == ClientRunLevel.Connected && !_config.GetCVar(CCVars.GameLobbyEnabled))
        {
            TryStartCover();
        }
    }

    private void OnJoinGame(TickerJoinGameEvent args)
    {
        if (!_enabled || !_armed)
            return;

        TryStartCover();
    }

    private void TryStartCover()
    {
        if (!TryGetArt(out var art))
            return;

        _armed = false;
        var panel = new RoundWipeUiPanel(art);
        _uiPanel = panel;
        _ui.RootControl.AddChild(panel);
        panel.Visible = true;
        

        StartWipe(art);
    }

    private void CleanupUiPanel()
    {
        if (_uiPanel != null)
        {
            _uiPanel.Parent?.RemoveChild(_uiPanel);
            _uiPanel = null;
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        // Returned to lobby (or a new round); re-arm for the next join.
        _armed = true;
        CleanupUiPanel();
        _overlay?.Release();
    }

    private void OnDetached(LocalPlayerDetachedEvent args)
    {
        _lastDetach = _timing.RealTime;
        _armed = true;
        CleanupUiPanel();
        _overlay?.Release();
    }

    private TimeSpan _lastDetach;

    private void OnAttached(LocalPlayerAttachedEvent args)
    {
        if (_overlay != null)
        {
            // Release trigger for a cover started by JoinGame.
            _overlay.MarkAttached();
            return;
        }

        // No JoinGame cover happened (e.g. direct connects with the lobby disabled):
        // fall back to starting the wipe at attach, ignoring quick mid-round hops.
        if (!_enabled || !_armed)
            return;

        if (_timing.RealTime - _lastDetach < TimeSpan.FromSeconds(5))
            return;

        _armed = false;

        if (!TryGetArt(out var art))
            return;

        StartWipe(art);
    }

    private bool TryGetArt(out Texture art)
    {
        art = default!;
        return _resourceCache.TryGetResource<TextureResource>(_artPath, out var res) && (art = res.Texture) != null;
    }

    private void StartWipe(Texture art)
    {
        var mode = _random.Next(0, 4);
        var seed = (float) _random.NextDouble();
        var overlay = new RoundWipeOverlay(art, seed, mode)
        {
            ZIndex = 100
        };
        _overlay = overlay;
        Log.Info($"[WIPE] started mode={mode} art={_artPath}");
        _overlayMan.AddOverlay(overlay);
    }
}
