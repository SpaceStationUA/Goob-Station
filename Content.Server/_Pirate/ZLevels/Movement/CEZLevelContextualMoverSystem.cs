// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.PhaseShift;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Content.Shared._Pirate.ZLevels.Ghost;
using Content.Shared._Pirate.ZLevels.Movement;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Mobs.Systems;
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

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEZLevelContextualMoverComponent, CEZLevelActionUp>(OnZLevelUp);
        SubscribeLocalEvent<CEZLevelContextualMoverComponent, CEZLevelActionDown>(OnZLevelDown);
        SubscribeLocalEvent<CEZLevelContextualMoverComponent, ComponentShutdown>(OnShutdown);
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

        return phased || !_zLevels.IsAscentBlocked(uid, xform);
    }

    private bool CanGoDown(EntityUid uid, TransformComponent xform, bool phased)
    {
        if (xform.MapUid is not { } mapUid || !_zLevels.TryMapDown(mapUid, out _))
            return false;

        if (phased)
            return true;

        return _zLevels.IsInEmptySpaceOnCurrentLevel(uid, xform) && !_zLevels.IsLandingBelowBlocked(uid, xform);
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
