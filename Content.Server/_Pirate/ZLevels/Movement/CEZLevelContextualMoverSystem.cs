// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.PhaseShift;
using Content.Server._FarHorizons.Planets; // Far Horizons: landing pads
using Content.Shared._FarHorizons.Planets; // Far Horizons: landing pads
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Content.Shared._Pirate.ZLevels.Ghost;
using Content.Shared._Pirate.ZLevels.Movement;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components; // Far Horizons: jetpack gate
using Robust.Shared.Timing;

namespace Content.Server._Pirate.ZLevels.Movement;

/// <summary>Manages z-level actions for opening traversal and phase shifting.</summary>
public sealed class CEZLevelContextualMoverSystem : EntitySystem
{
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.25);
    private static readonly TimeSpan MoveCooldown = TimeSpan.FromSeconds(1);

    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly CEPlanetSystem _planetSystem = default!; // Far Horizons: landing pads
    [Dependency] private readonly SharedTransformSystem _transform = default!; // Far Horizons: landing pads
    [Dependency] private readonly InventorySystem _inventory = default!; // Far Horizons: jetpack gate

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEZLevelContextualMoverComponent, CEZLevelActionUp>(OnZLevelUp);
        SubscribeLocalEvent<CEZLevelContextualMoverComponent, CEZLevelActionDown>(OnZLevelDown);
        SubscribeLocalEvent<CEZLevelContextualMoverComponent, ComponentShutdown>(OnShutdown);
        // Far Horizons: excavate the touchdown pad when a mob arrives on a planet surface.
        SubscribeLocalEvent<MobStateComponent, CEZLevelMapMoveEvent>(OnMovedDownLevel);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + UpdateInterval;

        var query = EntityQueryEnumerator<CEZLevelContextualMoverComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var mover, out var xform))
        {
            UpdateActions(uid, mover, xform);

            // Far Horizons: keep the touchdown pad under a sky-leaver clear while they float
            // above a planet surface, so biome rocks never block the auto-fall onto the ground.
            if (xform.MapUid is { } mapUid &&
                _zLevels.TryGetPlanetGroundLayerBelow(mapUid, out var groundMapUid) &&
                _zLevels.IsInEmptySpaceOnCurrentLevel(uid, xform))
            {
                _planetSystem.ExcavateLandingPad(groundMapUid.Value, _transform.GetWorldPosition(xform), 2.5f);
            }
        }
    }

    private void UpdateActions(EntityUid uid, CEZLevelContextualMoverComponent mover, TransformComponent xform)
    {
        // Ghost movers manage their own actions.
        if (HasComp<CEZLevelGhostMoverComponent>(uid))
        {
            SetAction(uid, mover, up: true, enabled: false);
            SetAction(uid, mover, up: false, enabled: false);
            return;
        }

        var alive = _mobState.IsAlive(uid);
        var phased = HasComp<PhaseShiftedComponent>(uid);

        SetAction(uid, mover, up: true, enabled: alive && CanGoUp(uid, xform, phased));
        SetAction(uid, mover, up: false, enabled: alive && CanGoDown(uid, xform, phased));
    }

    private bool CanGoUp(EntityUid uid, TransformComponent xform, bool phased)
    {
        if (xform.MapUid is not { } mapUid || !_zLevels.TryMapUp(mapUid, out _))
            return false;

        if (phased)
            return true;

        // Jetpack = free level travel in the sky, exactly like before the planet changes.
        if (HasActiveJetpack(uid))
            return !_zLevels.IsAscentBlocked(uid, xform);

        // Far Horizons: planet stacks — climbing up from the open terrain or floating around in
        // the sky is a jetpack thing (there's only void above). Aboard a ship the action still
        // moves between its decks.
        var onPlanetStack = HasComp<CEZGroundLayerComponent>(mapUid) ||
                            HasComp<CEZPlanetSkyLayerComponent>(mapUid) ||
                            _zLevels.TryGetPlanetGroundLayerBelow(mapUid, out _);
        if (onPlanetStack)
        {
            if (HasComp<CEZGroundLayerComponent>(mapUid))
            {
                if (xform.GridUid is not { } gridUid || gridUid == mapUid)
                    return false;
            }
            else if (_zLevels.IsInEmptySpaceOnCurrentLevel(uid, xform))
            {
                return false;
            }
        }

        return !_zLevels.IsAscentBlocked(uid, xform);
    }

    private bool CanGoDown(EntityUid uid, TransformComponent xform, bool phased)
    {
        if (xform.MapUid is not { } mapUid || !_zLevels.TryMapDown(mapUid, out _))
            return false;

        if (phased)
            return true;

        var inEmptySpace = _zLevels.IsInEmptySpaceOnCurrentLevel(uid, xform);
        if (!inEmptySpace)
            return false;

        // Jetpack = free level travel in the sky; the landing block only applies to falls.
        if (HasActiveJetpack(uid))
            return true;

        // Far Horizons: hopping levels while floating needs a jetpack — walking off a planet
        // shuttle means falling (or drifting to a landing spot), not free level travel.
        var onPlanetStack = HasComp<CEZGroundLayerComponent>(mapUid) ||
                            HasComp<CEZPlanetSkyLayerComponent>(mapUid) ||
                            _zLevels.TryGetPlanetGroundLayerBelow(mapUid, out _);
        if (onPlanetStack)
            return false;

        return !_zLevels.IsLandingBelowBlocked(uid, xform);
    }

    private void OnZLevelUp(Entity<CEZLevelContextualMoverComponent> ent, ref CEZLevelActionUp args)
    {
        if (args.Handled || HasComp<CEZLevelGhostMoverComponent>(ent) || _timing.CurTime < ent.Comp.NextMove)
            return;

        var xform = Transform(ent);
        var phased = HasComp<PhaseShiftedComponent>(ent);

        if (!_mobState.IsAlive(ent) || !CanGoUp(ent, xform, phased))
            return;

        if (!_zLevels.TryMoveUp(ent, bypassPassability: phased))
            return;

        StartCooldown(ent.Comp);
        args.Handled = true;
    }

    private void OnZLevelDown(Entity<CEZLevelContextualMoverComponent> ent, ref CEZLevelActionDown args)
    {
        if (args.Handled || HasComp<CEZLevelGhostMoverComponent>(ent) || _timing.CurTime < ent.Comp.NextMove)
            return;

        var xform = Transform(ent);
        var phased = HasComp<PhaseShiftedComponent>(ent);

        if (!_mobState.IsAlive(ent) || !CanGoDown(ent, xform, phased))
            return;

        if (!_zLevels.TryMoveDown(ent, bypassPassability: phased))
            return;

        StartCooldown(ent.Comp);
        args.Handled = true;
    }

    /// <summary>
    /// Far Horizons: a mob that moved down onto a planet surface excavates its touchdown pad
    /// immediately, so a fall from the sky lands on open ground instead of inside rocks/walls.
    /// </summary>
    private void OnMovedDownLevel(Entity<MobStateComponent> ent, ref CEZLevelMapMoveEvent args)
    {
        if (args.Offset >= 0)
            return;

        var xform = Transform(ent);
        if (xform.MapUid is not { } mapUid || !HasComp<CEZGroundLayerComponent>(mapUid))
            return;

        _planetSystem.ExcavateLandingPad(mapUid, _transform.GetWorldPosition(ent), 2.5f);
    }

    /// <summary>Far Horizons: true when the mob is an active jetpack user (toggled on, anywhere on them).</summary>
    private bool HasActiveJetpack(EntityUid uid)
    {
        // JetpackUserComponent marks an active jetpack user regardless of where the jetpack is
        // carried (back, suit slot, even hands).
        return HasComp<JetpackUserComponent>(uid);
    }

    private void SetAction(EntityUid uid, CEZLevelContextualMoverComponent mover, bool up, bool enabled)
    {
        ref var actionEntity = ref (up ? ref mover.ZLevelUpActionEntity : ref mover.ZLevelDownActionEntity);

        if (enabled)
        {
            if (actionEntity is { } existing &&
                TryComp<ActionComponent>(existing, out var action) &&
                action.AttachedEntity == uid)
            {
                return;
            }

            if (actionEntity is { } invalid && !Exists(invalid))
                actionEntity = null;

            _actions.AddAction(uid, ref actionEntity, up ? mover.UpActionProto : mover.DownActionProto);
        }
        else
        {
            if (actionEntity is not { } action)
                return;

            _actions.RemoveAction(uid, action);
            actionEntity = null;
        }
    }

    private void StartCooldown(CEZLevelContextualMoverComponent mover)
    {
        var start = _timing.CurTime;
        mover.NextMove = start + MoveCooldown;

        _actions.SetCooldown(mover.ZLevelUpActionEntity, start, mover.NextMove);
        _actions.SetCooldown(mover.ZLevelDownActionEntity, start, mover.NextMove);
    }

    private void OnShutdown(Entity<CEZLevelContextualMoverComponent> ent, ref ComponentShutdown args)
    {
        SetAction(ent, ent.Comp, up: true, enabled: false);
        SetAction(ent, ent.Comp, up: false, enabled: false);
    }
}
