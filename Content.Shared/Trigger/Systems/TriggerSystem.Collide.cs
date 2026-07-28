using Content.Shared.Projectiles;
using Content.Shared.Trigger.Components.Effects;
using Content.Shared.Trigger.Components.Triggers;
using Content.Shared.StepTrigger.Systems;
using Robust.Shared.Physics.Events;

namespace Content.Shared.Trigger.Systems;

public sealed partial class TriggerSystem
{
    // Pirate: pure disposable polybolts share the first successful target hit within a physics batch.
    private readonly HashSet<EntityUid> _handledDisposablePolymorphTargets = [];
    private readonly Dictionary<EntityUid, bool> _disposablePolymorphSources = [];

    private void InitializeCollide()
    {
        SubscribeLocalEvent<TriggerOnCollideComponent, StartCollideEvent>(OnCollide);
        SubscribeLocalEvent<TriggerOnStepTriggerComponent, StepTriggeredOffEvent>(OnStepTriggered);

        SubscribeLocalEvent<TriggerOnTimedCollideComponent, StartCollideEvent>(OnTimedCollide);
        SubscribeLocalEvent<TriggerOnTimedCollideComponent, EndCollideEvent>(OnTimedEndCollide);
        SubscribeLocalEvent<TriggerOnTimedCollideComponent, ComponentShutdown>(OnTimedShutdown);
    }

    private void OnCollide(Entity<TriggerOnCollideComponent> ent, ref StartCollideEvent args)
    {
        // Pirate: trigger effects on projectiles must obey the same pre-shot collision guard as ProjectileSystem.
        if (TryComp<ProjectileComponent>(ent, out var projectile) &&
            projectile is { Weapon: null, OnlyCollideWhenShot: true })
        {
            return;
        }

        if (
            args.OurFixtureId == ent.Comp.FixtureID
            && (!ent.Comp.IgnoreOtherNonHard || args.OtherFixture.Hard)
            && (ent.Comp.MaxTriggers == null || ent.Comp.MaxTriggers > 0)
        )
        {
            var deduplicatePolymorph = ShouldDeduplicateDisposablePolymorph(ent);
            if (deduplicatePolymorph && _handledDisposablePolymorphTargets.Contains(args.OtherEntity))
                return;

            if (ent.Comp.MaxTriggers != null)
            {
                ent.Comp.MaxTriggers--;
                Dirty(ent);
                if (ent.Comp.MaxTriggers <= 0)
                    RemCompDeferred<TriggerOnCollideComponent>(ent);
            }

            if (Trigger(ent.Owner, args.OtherEntity, ent.Comp.KeyOut) && deduplicatePolymorph)
                _handledDisposablePolymorphTargets.Add(args.OtherEntity);
        }
    }

    private bool ShouldDeduplicateDisposablePolymorph(Entity<TriggerOnCollideComponent> ent)
    {
        if (_disposablePolymorphSources.TryGetValue(ent, out var cached))
            return cached;

        var result = ent.Comp.MaxTriggers == null &&
                     TryComp<ProjectileComponent>(ent, out var projectile) &&
                     projectile.DeleteOnCollide &&
                     TryComp<PolymorphOnTriggerComponent>(ent, out var polymorph) &&
                     polymorph.TargetUser;

        if (result)
        {
            foreach (var component in EntityManager.GetComponents(ent))
            {
                if (component is BaseXOnTriggerComponent and not PolymorphOnTriggerComponent)
                {
                    result = false;
                    break;
                }
            }
        }

        _disposablePolymorphSources.Add(ent, result);
        return result;
    }

    private void OnStepTriggered(Entity<TriggerOnStepTriggerComponent> ent, ref StepTriggeredOffEvent args)
    {
        Trigger(ent, args.Tripper, ent.Comp.KeyOut);
    }

    private void OnTimedCollide(Entity<TriggerOnTimedCollideComponent> ent, ref StartCollideEvent args)
    {
        //Ensures the trigger entity will have an active component
        EnsureComp<ActiveTriggerOnTimedCollideComponent>(ent);
        var otherUID = args.OtherEntity;
        if (ent.Comp.Colliding.ContainsKey(otherUID))
            return;
        ent.Comp.Colliding.Add(otherUID, _timing.CurTime + ent.Comp.Threshold);
        Dirty(ent);
    }

    private void OnTimedEndCollide(Entity<TriggerOnTimedCollideComponent> ent, ref EndCollideEvent args)
    {
        var otherUID = args.OtherEntity;
        ent.Comp.Colliding.Remove(otherUID);
        Dirty(ent);

        if (ent.Comp.Colliding.Count == 0)
            RemComp<ActiveTriggerOnTimedCollideComponent>(ent);
    }

    private void OnTimedShutdown(Entity<TriggerOnTimedCollideComponent> ent, ref ComponentShutdown args)
    {
        RemComp<ActiveTriggerOnTimedCollideComponent>(ent);
    }

    private void UpdateTimedCollide()
    {
        _handledDisposablePolymorphTargets.Clear();
        _disposablePolymorphSources.Clear();

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<ActiveTriggerOnTimedCollideComponent, TriggerOnTimedCollideComponent>();
        while (query.MoveNext(out var uid, out _, out var triggerOnTimedCollide))
        {
            foreach (var (collidingEntity, collidingTime) in triggerOnTimedCollide.Colliding)
            {
                if (curTime > collidingTime)
                {
                    triggerOnTimedCollide.Colliding[collidingEntity] += triggerOnTimedCollide.Threshold;
                    // Goob start
                    var attemptTriggerEvent = new AttemptTriggerEvent(uid, collidingEntity.ToString());
                    RaiseLocalEvent(uid, ref attemptTriggerEvent);
                    if (attemptTriggerEvent.Cancelled)
                        return;
                    // Goob end
                    Dirty(uid, triggerOnTimedCollide);
                    Trigger(uid, collidingEntity, triggerOnTimedCollide.KeyOut);
                }
            }
        }
    }
}
