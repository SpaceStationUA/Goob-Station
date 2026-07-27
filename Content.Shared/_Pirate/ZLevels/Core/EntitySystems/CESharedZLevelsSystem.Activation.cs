/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared._Pirate.ZLevels.Ghost;
using Content.Shared.Ghost;
using JetBrains.Annotations;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Events;

namespace Content.Shared._Pirate.ZLevels.Core.EntitySystems;

/// <summary>
/// Raised on a z-physics entity when it wakes (becomes active) or sleeps (becomes inactive).
/// </summary>
[ByRefEvent]
public readonly record struct CEZPhysicsActivationChangedEvent(bool Active);

/// <summary>
/// Raised by the platform ghost systems when <see cref="GhostComponent"/> starts or stops.
/// This avoids observing every component mutation just to refresh Z-physics for ghosts.
/// </summary>
[ByRefEvent]
public readonly record struct CEZPhysicsGhostStateChangedEvent(bool IsGhost);

public abstract partial class CESharedZLevelsSystem
{
    private static readonly TimeSpan StartupActivationDelay = TimeSpan.FromSeconds(0.5);

    /// <summary>
    /// Entities currently driven by the z-physics update loop.
    /// Membership is mutated only through <see cref="WakeBody"/> / <see cref="SleepBody"/>.
    /// </summary>
    private readonly List<EntityUid> _activeBodies = new();
    private readonly HashSet<EntityUid> _activeBodySet = new();

    /// <summary>
    /// Entities whose movement cache will be refreshed at the start of the next physics update.
    /// Used to deduplicate cache work when many entities are invalidated at once (e.g. tile
    /// changes hitting an AABB full of bodies, or a grid moving its children).
    /// </summary>
    private readonly HashSet<EntityUid> _dirtyMovementBodies = new();

    [PublicAPI]
    public IReadOnlyList<EntityUid> ActiveBodies => _activeBodies;

    [PublicAPI]
    public bool IsBodyActive(EntityUid uid) => _activeBodySet.Contains(uid);

    /// <summary>
    /// Queues a coalesced movement-cache refresh, drained at the start of the next physics update.
    /// Use when many bodies are invalidated at once; synchronous callers needing the cache current
    /// before their next read should call <see cref="CacheMovement"/> directly.
    /// </summary>
    [PublicAPI]
    public void DirtyMovement(EntityUid uid)
    {
        _dirtyMovementBodies.Add(uid);
    }

    /// <summary>Drains the dirty-movement queue, refreshing each body's cache once.</summary>
    protected void UpdateDirtyMovement()
    {
        foreach (var uid in _dirtyMovementBodies)
        {
            if (ZPhysQuery.TryComp(uid, out var zPhys))
                CacheMovement((uid, zPhys));
        }

        _dirtyMovementBodies.Clear();
    }

    private void InitializeActivation()
    {
        SubscribeLocalEvent<CEZPhysicsEligibleComponent, MapInitEvent>(OnEligibleMapInit);
        SubscribeLocalEvent<CEZPhysicsEligibleComponent, EntParentChangedMessage>(OnEligibleParentChanged);
        SubscribeLocalEvent<CEZPhysicsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CEZPhysicsComponent, ComponentShutdown>(OnZPhysicsShutdown);
        SubscribeLocalEvent<CEZPhysicsComponent, AnchorStateChangedEvent>(OnAnchorStateChange);
        SubscribeLocalEvent<CEZPhysicsComponent, PhysicsBodyTypeChangedEvent>(OnPhysicsBodyTypeChange);
        SubscribeLocalEvent<CEZPhysicsComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<CEZPhysicsComponent, CEZPhysicsGhostStateChangedEvent>(OnGhostStateChanged);
        SubscribeLocalEvent<CEZLevelGhostMoverComponent, ComponentStartup>(OnGhostMoverStartup);
        SubscribeLocalEvent<CEZLevelGhostMoverComponent, ComponentShutdown>(OnGhostMoverShutdown);
    }

    private void OnEligibleMapInit(Entity<CEZPhysicsEligibleComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient || ZPhysQuery.HasComp(ent))
            return;

        TryActivateEligibleBody(ent, Transform(ent));
    }

    private void OnEligibleParentChanged(Entity<CEZPhysicsEligibleComponent> ent, ref EntParentChangedMessage args)
    {
        if (_net.IsClient || ZPhysQuery.HasComp(ent))
            return;

        TryActivateEligibleBody(ent, args.Transform);
    }

    private void OnAnchorStateChange(Entity<CEZPhysicsComponent> ent, ref AnchorStateChangedEvent args)
    {
        RefreshBody(ent);
    }

    private void OnMapInit(Entity<CEZPhysicsComponent> ent, ref MapInitEvent args)
    {
        InitializeZPhysicsBody(ent, Transform(ent), suppressStartup: true);
    }

    private void OnZPhysicsShutdown(Entity<CEZPhysicsComponent> ent, ref ComponentShutdown args)
    {
        SleepBody(ent);
    }

    private void InitializeZPhysicsBody(
        Entity<CEZPhysicsComponent> ent,
        TransformComponent xform,
        bool suppressStartup)
    {
        if (suppressStartup)
            ent.Comp.StartupSuppressedUntil = _timing.CurTime + StartupActivationDelay;

        RefreshBody(ent);

        if (!TryGetTraversalDepth(xform, out var depth))
            return;

        ent.Comp.CurrentZLevel = depth;
        DirtyField(ent, ent.Comp, nameof(CEZPhysicsComponent.CurrentZLevel));
    }

    private void TryActivateEligibleBody(
        Entity<CEZPhysicsEligibleComponent> ent,
        TransformComponent xform)
    {
        if (TerminatingOrDeleted(ent) || !HasTraversalContext(xform))
            return;

        var alreadyPresent = ZPhysQuery.TryComp(ent, out var zPhysics);
        zPhysics ??= EnsureComp<CEZPhysicsComponent>(ent);

        if (!alreadyPresent)
        {
            zPhysics.Bounciness = ent.Comp.Bounciness;
            zPhysics.GravityMultiplier = ent.Comp.GravityMultiplier;
            zPhysics.AutoStep = ent.Comp.AutoStep;
        }

        InitializeZPhysicsBody((ent, zPhysics), xform, suppressStartup: !alreadyPresent);
    }

    /// <summary>
    /// Activates eligible entities that were already initialized when their map became a Z-level.
    /// </summary>
    protected void ActivateEligibleBodiesOnMap(EntityUid mapUid)
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<CEZPhysicsEligibleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var eligible, out var xform))
        {
            if (xform.MapUid != mapUid || LifeStage(uid) < EntityLifeStage.MapInitialized)
                continue;

            TryActivateEligibleBody((uid, eligible), xform);
        }
    }

    private void OnPhysicsBodyTypeChange(Entity<CEZPhysicsComponent> ent, ref PhysicsBodyTypeChangedEvent args)
    {
        RefreshBody(ent);
    }

    private void OnParentChanged(Entity<CEZPhysicsComponent> ent, ref EntParentChangedMessage args)
    {
        RefreshBody(ent);
        if (!IsBodyActive(ent))
            return;

        var xform = args.Transform;
        if (!HasTraversalContext(xform))
            return;

        if (_net.IsClient && !_timing.ApplyingState)
            return;

        var oldParentWorld = GetEntityWorldPositionCsv(args.OldParent);
        var oldParentVelocity = GetEntityVelocityCsv(args.OldParent);
        var newParentUid = xform.ParentUid;
        var newParentWorld = GetEntityWorldPositionCsv(newParentUid);
        var newParentVelocity = GetEntityVelocityCsv(newParentUid);

        DebugZStairCsv(ent,
            "parent_change",
            $"old_parent={args.OldParent},old_parent_world={oldParentWorld},old_parent_vel={oldParentVelocity},new_parent={newParentUid},new_parent_world={newParentWorld},new_parent_vel={newParentVelocity},new_grid={xform.GridUid},new_map={xform.MapUid}");

        if (ZPhysQuery.TryComp(args.OldParent, out var oldParentZPhys))
            SetZPosition((ent, ent), oldParentZPhys.LocalPosition);
    }

    private void OnGhostMoverStartup(Entity<CEZLevelGhostMoverComponent> ent, ref ComponentStartup args)
    {
        RefreshZPhysicsActivation(ent);
    }

    private void OnGhostMoverShutdown(Entity<CEZLevelGhostMoverComponent> ent, ref ComponentShutdown args)
    {
        RefreshZPhysicsActivation(ent);
    }

    private void OnGhostStateChanged(Entity<CEZPhysicsComponent> ent, ref CEZPhysicsGhostStateChangedEvent args)
    {
        if (args.IsGhost)
        {
            ResetInactiveZPhysics(ent);
            return;
        }

        // ComponentShutdown/ComponentRemove is raised while GhostComponent is still queryable.
        RefreshBody(ent, ignoreGhost: true);
    }

    private void RefreshZPhysicsActivation(EntityUid uid)
    {
        if (!ZPhysQuery.TryComp(uid, out var zPhys))
            return;

        RefreshBody((uid, zPhys));
    }

    private bool IsAutomaticZPhysicsExcluded(EntityUid uid, bool ignoreGhost = false)
    {
        return (!ignoreGhost && HasComp<GhostComponent>(uid)) ||
               HasComp<CEZLevelGhostMoverComponent>(uid) ||
               HasComp<CEZLevelPhysicsExemptComponent>(uid) || // Pirate: multiz - free-floating camera eyes
               _container.IsEntityInContainer(uid); // Pirate: multiz - contained entities (e.g. mech pilot) ride their holder, never fall independently
    }

    /// <summary>
    /// Re-evaluates whether <paramref name="ent"/> should be in the active list and dispatches
    /// to <see cref="WakeBody"/> or <see cref="SleepBody"/>.
    /// </summary>
    [PublicAPI]
    public void RefreshBody(Entity<CEZPhysicsComponent> ent)
    {
        RefreshBody(ent, ignoreGhost: false);
    }

    private void RefreshBody(Entity<CEZPhysicsComponent> ent, bool ignoreGhost)
    {
        if (TerminatingOrDeleted(ent))
        {
            SleepBody(ent);
            return;
        }

        var xform = Transform(ent);

        if (!HasTraversalContext(xform))
        {
            DeactivateOutsideTraversal(ent);
            return;
        }

        if (IsAutomaticZPhysicsExcluded(ent, ignoreGhost))
        {
            ResetInactiveZPhysics(ent);
            return;
        }

        if (xform.ParentUid != xform.MapUid && xform.ParentUid != xform.GridUid)
        {
            DebugZ(ent, "z-physics inactive: parent is neither the map nor the grid");
            SleepBody(ent);
            return;
        }

        if (xform.Anchored)
        {
            DebugZ(ent, "z-physics inactive: entity is anchored");
            SleepBody(ent);
            return;
        }

        if (PhysicsQuery.TryComp(ent, out var physics) && physics.BodyType == BodyType.Static)
        {
            DebugZ(ent, "z-physics inactive: body type is static");
            SleepBody(ent);
            return;
        }

        DebugZ(ent, "z-physics active");
        WakeBody(ent);
    }

    private void DeactivateOutsideTraversal(Entity<CEZPhysicsComponent> ent)
    {
        if (_net.IsServer && HasComp<CEZPhysicsEligibleComponent>(ent))
        {
            SleepBody(ent);
            SetZGravityInfluenced(ent, false);
            RemComp<CEZPhysicsComponent>(ent);
            return;
        }

        ResetInactiveZPhysics(ent);
    }

    private void ResetInactiveZPhysics(Entity<CEZPhysicsComponent> ent)
    {
        if (ent.Comp.Velocity != 0f)
        {
            ent.Comp.Velocity = 0f;
            DirtyField(ent, ent.Comp, nameof(CEZPhysicsComponent.Velocity));
        }

        if (ent.Comp.LocalPosition != 0f)
        {
            ent.Comp.LocalPosition = 0f;
            DirtyField(ent, ent.Comp, nameof(CEZPhysicsComponent.LocalPosition));
        }

        SleepBody(ent);
        SetZGravityInfluenced(ent, false);
        ent.Comp.DetachedCarrierGridUid = EntityUid.Invalid;
        ent.Comp.DetachedCarrierLocalPosition = default;
        ent.Comp.DetachedCarrierReferenceExpiresAt = default;
    }

    /// <summary>
    /// Adds the entity to the active list and primes its movement cache. No-op if already active.
    /// </summary>
    [PublicAPI]
    public void WakeBody(Entity<CEZPhysicsComponent> ent)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (!_activeBodySet.Add(ent))
            return;

        _activeBodies.Add(ent);
        EnsureComp<CEZPhysicsActiveComponent>(ent);

        CacheMovement(ent);

        var ev = new CEZPhysicsActivationChangedEvent(true);
        RaiseLocalEvent(ent, ref ev);
    }

    /// <summary>
    /// Removes the entity from the active list. No-op if it wasn't active.
    /// </summary>
    [PublicAPI]
    public void SleepBody(EntityUid uid)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (!_activeBodySet.Remove(uid))
            return;

        _activeBodies.Remove(uid);
        RemComp<CEZPhysicsActiveComponent>(uid);
        SetZGravityInfluenced(uid, false);

        var ev = new CEZPhysicsActivationChangedEvent(false);
        RaiseLocalEvent(uid, ref ev);
    }
}
