// SPDX-License-Identifier: MIT

using Content.Goobstation.Common.CCVar;
using Content.Shared.GameTicking;
using Content.Goobstation.Common.CCVar;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Client;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Content.Client.Lobby;
using Robust.Client.UserInterface.Controls;
using Content.Client.MainMenu;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client._Pirate.Wipe;

/// <summary>
///     Drives the round-start wipe: a fullscreen UI panel shows the lobby art
///     around round transitions:
///       - when a round the player is ready for starts in the lobby,
///       - when joining a running round from the lobby,
///       - when a round ends / restarts ("restarting..." on the live server),
///     holding ~5s and dissolving into the live game. It does not cover plain
///     connect/reconnect/loading screens.
/// </summary>
public sealed class RoundWipeSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IStateManager _state = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    private static readonly TimeSpan Failsafe = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ReleaseTime = TimeSpan.FromSeconds(3);
    private static TimeSpan HoldSpan => TimeSpan.FromSeconds(_holdCache);

    // covers idle ("breathing") for this long before the dissolve starts
    // hold is a cvar (pirate.roundwipe_hold), default 1s
    private static float _holdCache = 1f;

    // Connection state lives across system re-initialization (content modules
    // re-init on every connect), otherwise a second system instance would spawn a
    // duplicate panel.
    private static bool _enabled;
    private static string _artPath = "";

    /// <summary>Whether the wipe is armed. Armed after leaving a round; a cover can
    /// only start while armed, so mid-round re-attaches do nothing.</summary>
    private static bool _armed = true;

    private static RoundWipeUiPanel? _panel;
    private static TimeSpan _coverRealTime;
    private static TimeSpan _releaseElapsed;
    private static bool _release;
    private static float _seed;
    private static int _mode;
    private static float _hold;
    private static bool _needAttach;
    private static bool _attachSeen;
    private static TimeSpan _attachRealTime;
    private static TimeSpan _lastDetach;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<TickerJoinGameEvent>(OnJoinGame);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnDetached);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        _config.OnValueChanged(PirateCVars.PirateRoundWipe, v => _enabled = v, invokeImmediately: true);
        _config.OnValueChanged(PirateCVars.PirateRoundWipeArt, v => _artPath = v, invokeImmediately: true);

        _state.OnStateChanged += OnStateChange;
        _config.OnValueChanged(PirateCVars.PirateRoundWipeTest, v => _testMode = v, invokeImmediately: true);
        _config.OnValueChanged(PirateCVars.PirateRoundWipeHold, v => _holdCache = v, invokeImmediately: true);
    }

    private static bool _testMode;

    private void OnStateChange(StateChangedEventArgs args)
    {
        RaisePanel();
        if (args.NewState is LobbyState)
        {
            // Pre-game lobby reached. Drop the cover only if it waits for an attach
            // (i.e. it was started for a join that got cancelled); round-end covers
            // keep playing over the lobby.
            if (_panel != null && _needAttach && !_release)
            {
                StopPanel("lobby");
                _armed = true;
            }
            else
            {
                _armed = true;
            }
        }
        else if (args.NewState is MainScreen)
        {
            // back at the main menu (disconnect): re-arm
            _armed = true;
        }
    }

    private void OnJoinGame(TickerJoinGameEvent args)
    {
        if (!_enabled || !_armed)
            return;

        // Round starting with us ready, or joining from the lobby: the ticker
        // switches into the game right after this event.
        TryStartCover(needAttach: true);
    }

    private void TryStartCover(bool needAttach)
    {
        if (_panel != null || !TryGetArt(out var art))
            return;

        // The attach event often lands BEFORE the ticker join message; if the player
        // is already attached, count the attach as seen right away.
        _attachRealTime = TimeSpan.FromTicks(0);
        if (needAttach && _player.LocalEntity != null)
        {
            needAttach = false;
            _attachRealTime = _timing.RealTime;
        }

        _armed = false;
        var maskCvar = _config.GetCVar(PirateCVars.PirateRoundWipeMask);
        _mode = maskCvar >= 0 ? maskCvar : _random.Next(0, 4);
        _seed = (float) _random.NextDouble();
        _coverRealTime = _timing.RealTime;
        _releaseElapsed = TimeSpan.Zero;
        _needAttach = needAttach;
        _attachSeen = !needAttach;
        _panel = new RoundWipeUiPanel(art, _mode, _seed) { Progress = 0f };
        _ui.RootControl.AddChild(_panel);
        AnchorsForce();
        Log.Info($"[WIPE] cover started mask={_mode} art={_artPath} root={_ui.RootControl.Size}");
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
        AnchorsForce();
    }

    private static void AnchorsForce()
    {
        if (_panel == null)
            return;
        LayoutContainer.SetAnchorPreset(_panel, LayoutContainer.LayoutPreset.Wide);
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

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        // The round teardown ("restart..."): cover the world reset into the next
        // round/lobby, then arm for the next join. Join covers waiting on a dead
        // load are dropped.
        if (_panel != null && _needAttach && !_release)
            StopPanel("roundrestart");
        _armed = true;
        TryStartCover(needAttach: false);
    }

    private void OnDetached(LocalPlayerDetachedEvent args)
    {
        _lastDetach = _timing.RealTime;
        _armed = true;
        StopPanel("detach");
    }

    private void OnAttached(LocalPlayerAttachedEvent args)
    {
        Log.Info($"[WIPE] attach armed={_armed} panel={_panel != null}");

        // dev: isolated shader test - force a cover that releases right after the hold
        if (_panel == null && _testMode && _enabled)
        {
            TryStartCover(needAttach: false);
            return;
        }

        if (_panel == null)
            return;

        _attachSeen = true;
        _attachRealTime = _timing.RealTime;
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

        // force the cover above freshly-spawned HUD/chat controls
        RaisePanel();

        if (!_release)
        {
            var elapsed = _timing.RealTime - _coverRealTime;
            if (_needAttach)
            {
                // join covers: hold counts from the ATTACH (the breathing happens over
                // the live world, not the loading screen); failsafe guards against
                // stuck loads
                if ((_attachRealTime > TimeSpan.Zero
                     && _timing.RealTime - _attachRealTime >= HoldSpan)
                    || elapsed >= Failsafe)
                    _release = true;
            }
            else if (elapsed >= HoldSpan)
            {
                // round-end covers do not wait for the next attach
                _release = true;
            }
        }

        if (_release)
            _releaseElapsed += TimeSpan.FromSeconds(frameTime);

        if (_panel != null)
        {
            _panel.Progress = _release ? MathF.Min(1f, (float) (_releaseElapsed / ReleaseTime)) : 0f;
            if (_panel.Progress >= 1f)
                StopPanel("done");
        }
    }
}
