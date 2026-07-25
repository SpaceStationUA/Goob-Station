using Content.Server.Polymorph.Components;
using Content.Server.Polymorph.Systems;
using Content.Shared.Polymorph;
using Content.Shared.Projectiles;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Components.Effects;
using Content.Shared.Trigger.Components.Triggers;
using Robust.Shared.Prototypes;

namespace Content.Server.Trigger.Systems;

public sealed partial class PolymorphOnTriggerSystem : EntitySystem
{
    private const int MaxUnlimitedCollisionPolymorphsPerTick = 1;

    [Dependency] private readonly PolymorphSystem _polymorph = default!;

    /// <summary>
    /// Need to do this so we don't get a collection enumeration error in physics by polymorphing
    /// an entity we're colliding with in case of TriggerOnCollide.
    /// Also makes sure other trigger effects don't activate in nullspace after we have polymorphed.
    /// </summary>
    private readonly Queue<(EntityUid Uid, ProtoId<PolymorphPrototype> Polymorph)> _queuedPolymorphUpdates = new();

    // Pirate: spread unlimited collision fan-out across ticks instead of polymorphing every stacked target at once.
    private readonly Queue<(EntityUid Uid, ProtoId<PolymorphPrototype> Polymorph)> _queuedUnlimitedCollisionPolymorphUpdates = new();

    // Pirate: multiple contacts in one physics step must not polymorph the same hidden parent repeatedly.
    private readonly HashSet<EntityUid> _queuedPolymorphTargets = [];

    // Pirate: an unlimited collision trigger may stay overlapped with the replacement entity.
    // Track polymorph roots per source so it can still hit distinct stacked entities exactly once.
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _collisionPolymorphRootsBySource = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PolymorphOnTriggerComponent, TriggerEvent>(OnTrigger);
        SubscribeLocalEvent<PolymorphOnTriggerComponent, ComponentShutdown>(OnComponentShutdown);
    }

    private void OnTrigger(Entity<PolymorphOnTriggerComponent> ent, ref TriggerEvent args)
    {
        if (args.Key != null && !ent.Comp.KeysIn.Contains(args.Key))
            return;

        var target = ent.Comp.TargetUser ? args.User : ent.Owner;

        if (target == null)
            return;

        var targetUid = target.Value;
        var unlimitedCollision = TryComp<TriggerOnCollideComponent>(ent.Owner, out var collision) &&
                                 collision.MaxTriggers == null;
        var deleteOnCollideProjectile = TryComp<ProjectileComponent>(ent.Owner, out var projectile) &&
                                        projectile.DeleteOnCollide;
        if (unlimitedCollision && !deleteOnCollideProjectile)
        {
            var root = GetPolymorphRoot(targetUid);
            if (!_collisionPolymorphRootsBySource.TryGetValue(ent.Owner, out var roots))
            {
                roots = [];
                _collisionPolymorphRootsBySource.Add(ent.Owner, roots);
            }

            if (!roots.Add(root))
            {
                args.Handled = true;
                return;
            }
        }

        if (_queuedPolymorphTargets.Add(targetUid))
        {
            var update = (targetUid, ent.Comp.Polymorph);
            if (unlimitedCollision)
                _queuedUnlimitedCollisionPolymorphUpdates.Enqueue(update);
            else
                _queuedPolymorphUpdates.Enqueue(update);
        }

        args.Handled = true;
    }

    private void OnComponentShutdown(Entity<PolymorphOnTriggerComponent> ent, ref ComponentShutdown args)
    {
        _collisionPolymorphRootsBySource.Remove(ent.Owner);
    }

    private EntityUid GetPolymorphRoot(EntityUid uid)
    {
        var root = uid;
        while (TryComp<PolymorphedEntityComponent>(root, out var polymorphed) &&
               polymorphed.Parent is { } parent &&
               parent != root)
        {
            root = parent;
        }

        return root;
    }

    public override void Update(float frametime)
    {
        while (_queuedPolymorphUpdates.TryDequeue(out var data))
            ProcessPolymorph(data);

        var processed = 0;
        while (processed < MaxUnlimitedCollisionPolymorphsPerTick &&
               _queuedUnlimitedCollisionPolymorphUpdates.TryDequeue(out var data))
        {
            if (ProcessPolymorph(data))
                processed++;
        }
    }

    private bool ProcessPolymorph((EntityUid Uid, ProtoId<PolymorphPrototype> Polymorph) data)
    {
        try
        {
            if (TerminatingOrDeleted(data.Uid))
                return false;

            _polymorph.PolymorphEntity(data.Uid, data.Polymorph);
            return true;
        }
        finally
        {
            _queuedPolymorphTargets.Remove(data.Uid);
        }
    }
}
