/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Content.Shared._Pirate.ZLevels.Flight.Components;
using Content.Shared._Pirate.ZLevels.Ghost;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Gravity;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.ZLevels.Weightless;

/// <summary>
/// Grants ghost-style z-level movement actions while a living mob is floating in weightlessness.
/// </summary>
public sealed class CEWeightlessZLevelMoverSystem : EntitySystem
{
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.25);
    private static readonly TimeSpan MoveCooldown = TimeSpan.FromSeconds(1);

    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedGravitySystem _gravity = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly CESharedZLevelsSystem _zLevels = default!;

    private TimeSpan _nextUpdate;
    // Active living mobs: the only bodies these actions apply to. Maintained by activation,
    // mob-state and component events; every poll still re-checks full eligibility.
    private readonly HashSet<EntityUid> _eligibleMobs = new();
    private readonly List<EntityUid> _pollSnapshot = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEWeightlessZLevelMoverComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CEWeightlessZLevelMoverComponent, CEZLevelActionUp>(OnZLevelUp);
        SubscribeLocalEvent<CEWeightlessZLevelMoverComponent, CEZLevelActionDown>(OnZLevelDown);
        SubscribeLocalEvent<CEZPhysicsComponent, ComponentShutdown>(OnZPhysicsShutdown);
        SubscribeLocalEvent<CEZPhysicsComponent, CEZPhysicsActivationChangedEvent>(OnZPhysicsActivationChanged);
        SubscribeLocalEvent<CEZPhysicsComponent, MobStateChangedEvent>(OnMobStateChanged);
        // Directed MobState component lifecycle events are already owned by other systems.
        EntityManager.ComponentAdded += OnComponentAdded;
        EntityManager.ComponentRemoved += OnComponentRemoved;
    }

    public override void Shutdown()
    {
        EntityManager.ComponentAdded -= OnComponentAdded;
        EntityManager.ComponentRemoved -= OnComponentRemoved;
        base.Shutdown();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + UpdateInterval;

        // Action and component callbacks can change membership during UpdateActions.
        _pollSnapshot.Clear();
        _pollSnapshot.AddRange(_eligibleMobs);
        foreach (var uid in _pollSnapshot)
        {
            if (!_eligibleMobs.Contains(uid))
                continue;

            if (!Exists(uid))
            {
                _eligibleMobs.Remove(uid);
                continue;
            }

            if (!TryComp<PhysicsComponent>(uid, out var physics) ||
                !TryComp<TransformComponent>(uid, out var xform))
            {
                continue;
            }

            UpdateActions(uid, physics, xform);
        }
    }

    private void UpdateActions(EntityUid uid, PhysicsComponent physics, TransformComponent xform)
    {
        if (!CanUseWeightlessZMovement(uid, physics, xform))
        {
            RemCompDeferred<CEWeightlessZLevelMoverComponent>(uid);
            return;
        }

        var hasUp = HasZLevel(uid, 1, xform);
        var hasDown = _zLevels.IsInEmptySpaceOnCurrentLevel(uid, xform) &&
                      HasZLevel(uid, -1, xform);

        if (!hasUp && !hasDown)
        {
            RemCompDeferred<CEWeightlessZLevelMoverComponent>(uid);
            return;
        }

        var mover = EnsureComp<CEWeightlessZLevelMoverComponent>(uid);

        if (hasUp)
            EnsureAction(uid, mover, true);
        else
            RemoveAction(uid, mover, true);

        if (hasDown)
            EnsureAction(uid, mover, false);
        else
            RemoveAction(uid, mover, false);
    }

    private bool CanUseWeightlessZMovement(EntityUid uid, PhysicsComponent physics, TransformComponent xform)
    {
        if (HasComp<CEZLevelGhostMoverComponent>(uid))
            return false;

        if (!_mobState.IsAlive(uid))
            return false;

        if (TryComp<CEZFlyerComponent>(uid, out var flyer) && flyer.Active)
            return false;

        if (!_gravity.IsWeightless(uid)) // Pirate: multiz - IsWeightless now reads cached GravityAffectedComponent, takes only the entity
            return false;

        return true;
    }

    private bool HasZLevel(EntityUid uid, int offset, TransformComponent? xform = null)
    {
        xform ??= Transform(uid);

        if (xform.MapUid is not { } mapUid)
            return false;

        if (offset > 0)
            return _zLevels.TryMapUp(mapUid, out _) && !_zLevels.IsAscentBlocked(uid, xform);

        return _zLevels.TryMapDown(mapUid, out _) && !_zLevels.IsLandingBelowBlocked(uid, xform);
    }

    private void EnsureAction(EntityUid uid, CEWeightlessZLevelMoverComponent mover, bool up)
    {
        ref var actionEntity = ref (up ? ref mover.ZLevelUpActionEntity : ref mover.ZLevelDownActionEntity);

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

    private void RemoveAction(EntityUid uid, CEWeightlessZLevelMoverComponent mover, bool up)
    {
        ref var actionEntity = ref (up ? ref mover.ZLevelUpActionEntity : ref mover.ZLevelDownActionEntity);

        if (actionEntity is not { } action)
            return;

        _actions.RemoveAction(uid, action);
        actionEntity = null;
    }

    private void OnZLevelUp(Entity<CEWeightlessZLevelMoverComponent> ent, ref CEZLevelActionUp args)
    {
        if (args.Handled || !CanUseActionNow(ent, ent.Comp, 1))
            return;

        if (!_zLevels.TryMoveUp(ent))
            return;

        StartCooldown(ent.Comp);
        args.Handled = true;
    }

    private void OnZLevelDown(Entity<CEWeightlessZLevelMoverComponent> ent, ref CEZLevelActionDown args)
    {
        if (args.Handled || !CanUseActionNow(ent, ent.Comp, -1))
            return;

        if (!_zLevels.TryMoveDown(ent))
            return;

        StartCooldown(ent.Comp);
        args.Handled = true;
    }

    private bool CanUseActionNow(EntityUid uid, CEWeightlessZLevelMoverComponent mover, int offset)
    {
        if (_timing.CurTime < mover.NextMove)
            return false;

        if (!HasComp<CEZPhysicsComponent>(uid) ||
            !_zLevels.IsBodyActive(uid) ||
            !TryComp<PhysicsComponent>(uid, out var physics))
        {
            return false;
        }

        var xform = Transform(uid);
        if (!CanUseWeightlessZMovement(uid, physics, xform) ||
            !HasZLevel(uid, offset, xform))
        {
            return false;
        }

        return offset > 0 || _zLevels.IsInEmptySpaceOnCurrentLevel(uid, xform);
    }

    private void StartCooldown(CEWeightlessZLevelMoverComponent mover)
    {
        var start = _timing.CurTime;
        mover.NextMove = start + MoveCooldown;

        _actions.SetCooldown(mover.ZLevelUpActionEntity, start, mover.NextMove);
        _actions.SetCooldown(mover.ZLevelDownActionEntity, start, mover.NextMove);
    }

    private void OnZPhysicsShutdown(Entity<CEZPhysicsComponent> ent, ref ComponentShutdown args)
    {
        _eligibleMobs.Remove(ent);
        _zLevels.SleepBody(ent);
        RemCompDeferred<CEWeightlessZLevelMoverComponent>(ent);
    }

    private void OnZPhysicsActivationChanged(Entity<CEZPhysicsComponent> ent, ref CEZPhysicsActivationChangedEvent args)
    {
        if (args.Active && _mobState.IsAlive(ent))
        {
            _eligibleMobs.Add(ent);
        }
        else
        {
            _eligibleMobs.Remove(ent);
            RemCompDeferred<CEWeightlessZLevelMoverComponent>(ent);
        }
    }

    private void OnMobStateChanged(Entity<CEZPhysicsComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive && _zLevels.IsBodyActive(ent))
        {
            _eligibleMobs.Add(ent);
        }
        else
        {
            _eligibleMobs.Remove(ent);
            RemCompDeferred<CEWeightlessZLevelMoverComponent>(ent);
        }
    }

    private void OnComponentAdded(AddedComponentEventArgs args)
    {
        if (args.BaseArgs.Component is not MobStateComponent mobState)
            return;

        var uid = args.BaseArgs.Owner;
        if (_zLevels.IsBodyActive(uid) && _mobState.IsAlive(uid, mobState))
            _eligibleMobs.Add(uid);
    }

    private void OnComponentRemoved(RemovedComponentEventArgs args)
    {
        if (args.BaseArgs.Component is not MobStateComponent)
            return;

        var uid = args.BaseArgs.Owner;
        _eligibleMobs.Remove(uid);
        if (Exists(uid))
            RemCompDeferred<CEWeightlessZLevelMoverComponent>(uid);
    }

    private void OnShutdown(Entity<CEWeightlessZLevelMoverComponent> ent, ref ComponentShutdown args)
    {
        RemoveAction(ent, ent.Comp, true);
        RemoveAction(ent, ent.Comp, false);
    }
}
