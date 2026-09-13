// SPDX-License-Identifier: MIT

using Content.Goobstation.Common.CCVar;
using Content.Shared.GameTicking;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._Pirate.Wipe;

/// <summary>
///     Spawns the round-start wipe overlay when the server tells the client to join
///     the game, covering the world-streaming glitches under a dissolve from the
///     splash/lobby art into the live view. Releases when the local player attaches
///     (plus a short pad), or on a failsafe timer.
/// </summary>
public sealed class RoundWipeSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private bool _enabled;
    private string _artPath = "";

    /// <summary>Whether the wipe is armed. Armed after leaving a round, so only
    /// lobby→round transitions wipe; mid-round re-attaches do nothing.</summary>
    private bool _armed = true;

    private RoundWipeOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TickerJoinGameEvent>(OnJoinGame);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnDetached);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        _config.OnValueChanged(PirateCVars.PirateRoundWipe, v => _enabled = v, invokeImmediately: true);
        _config.OnValueChanged(PirateCVars.PirateRoundWipeArt, v => _artPath = v, invokeImmediately: true);
    }

    private void OnJoinGame(TickerJoinGameEvent args)
    {
        if (!_enabled || !_armed || !TryGetArt(out var art))
            return;

        _armed = false;
        StartWipe(art);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        // Returned to lobby (or a new round); re-arm for the next join.
        _armed = true;
        _overlay?.Release();
    }

    private void OnDetached(LocalPlayerDetachedEvent args)
    {
        _lastDetach = _timing.RealTime;
        _armed = true;
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
        var overlay = new RoundWipeOverlay(art, (float) _random.NextDouble(), mode)
        {
            ZIndex = 100
        };
        _overlay = overlay;
        Log.Info($"[WIPE] started mode={mode} art={_artPath}");
        _overlayMan.AddOverlay(overlay);
    }
}
