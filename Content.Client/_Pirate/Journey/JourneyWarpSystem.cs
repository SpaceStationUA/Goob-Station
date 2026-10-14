// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Parallax;
using Content.Client.Parallax.Data;
using Content.Client.Parallax.Managers;
using Content.Shared._Pirate.Journey;
using Content.Shared.GameTicking;
using Content.Shared.Parallax;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Pirate.Journey;

/// <summary>
///     Client side of the journey mode: replays the warp when the rule jumps the station.
/// </summary>
/// <remarks>
///     The server cannot touch parallax, so it only says where the ship is headed. Every connected
///     client, ghosts included, runs the same warp, so spectating stays coherent.
/// </remarks>
public sealed class JourneyWarpSystem : EntitySystem
{
    [Dependency] private readonly IParallaxManager _parallax = default!;
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const float SetFadeDuration = 10f;

    // A warp waiting out its run-up. The entry sound has already played; the visuals start at _warpAt.
    private string? _pendingParallax;
    private float _pendingDuration;
    private double _warpAt;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<JourneySectorJumpEvent>(OnJump);
        SubscribeNetworkEvent<JourneySectorSetEvent>(OnSet);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    /// <summary>
    ///     A jump still waiting out its run-up when the round ends must not fire into the next
    ///     one: the manager clears its own state at restart, but this timer lives here.
    /// </summary>
    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _pendingParallax = null;
        _pendingDuration = 0f;
        _warpAt = 0;
    }

    /// <summary>
    ///     Fades to a sector's sky without warp effects. Skipped when that sky is already up, and when
    ///     there is no map yet: the spawn resync delivers it again.
    /// </summary>
    private void OnSet(JourneySectorSetEvent ev)
    {
        var current = CurrentParallax();
        if (current == null || current == ev.Parallax)
            return;

        if (!_prototypes.HasIndex<ParallaxPrototype>(ev.Parallax))
        {
            Log.Warning($"journey set to '{ev.Parallax}' did not play: no such parallax prototype.");
            return;
        }

        _parallax.RequestTransition(current, ev.Parallax, SetFadeDuration, 0f);
    }

    private void OnJump(JourneySectorJumpEvent ev)
    {
        // The entry sound plays on receipt, as the muster warning. The flash waits out the run-up.
        _entities.System<WarpEffectsSystem>().PlayEntrySound();

        if (ev.Delay <= 0f)
        {
            FireWarp(ev.Parallax, ev.Duration);
            return;
        }

        _pendingParallax = ev.Parallax;
        _pendingDuration = ev.Duration;
        _warpAt = _timing.CurTime.TotalSeconds + ev.Delay;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingParallax == null || _timing.CurTime.TotalSeconds < _warpAt)
            return;

        var parallax = _pendingParallax;
        var duration = _pendingDuration;
        _pendingParallax = null;
        FireWarp(parallax, duration);
    }

    private void FireWarp(string parallax, float duration)
    {
        var error = WarpJumpRunner.Run(_parallax, _prototypes, CurrentParallax(), parallax, duration);
        if (error != null)
            Log.Warning($"journey jump to '{parallax}' did not play: {error}");

        // Warm the other sectors while this warp plays, so the next jump fades on arrival instead of
        // waiting for a load.
        foreach (var sector in _prototypes.EnumeratePrototypes<JourneySectorPrototype>())
        {
            if (sector.Parallax != parallax && !_parallax.IsLoaded(sector.Parallax))
                _parallax.LoadParallaxByName(sector.Parallax);
        }
    }

    /// <summary>
    ///     The parallax on screen: the one the last fade settled on, or else the local player's map's.
    /// </summary>
    private string? CurrentParallax()
    {
        if (_parallax.GetCurrent() is { } current)
            return current;

        if (_players.LocalEntity is not { } local
            || !_entities.TryGetComponent<TransformComponent>(local, out var xform)
            || xform.MapUid is not { } map
            || !_entities.TryGetComponent<ParallaxComponent>(map, out var parallax))
        {
            return null;
        }

        return parallax.Parallax;
    }
}
