using Content.Goobstation.Maths.FixedPoint;
using Content.Pirate.Shared.Witch.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Pirate.Server.Witch.Systems;

public sealed class WitchSubstitutionSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<WitchSubstitutionStatusEffectComponent, StatusEffectComponent>();
        while (query.MoveNext(out _, out var comp, out var status))
        {
            if (status.AppliedTo is not { } target || _timing.CurTime < comp.NextAttempt)
                continue;

            comp.NextAttempt = _timing.CurTime + TimeSpan.FromSeconds(comp.Interval);
            TrySubstitute(target, comp);
        }
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WitchSubstitutionStatusEffectComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<WitchSubstitutionStatusEffectComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<StatusEffectComponent>(ent.Owner, out var status)
            || status.AppliedTo is not { } target)
        {
            return;
        }

        ent.Comp.NextAttempt = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.Interval);
        TrySubstitute(target, ent.Comp);
    }

    private void TrySubstitute(EntityUid target, WitchSubstitutionStatusEffectComponent comp)
    {
        if (!TryComp<MobStateComponent>(target, out _) || !TryComp<DamageableComponent>(target, out _))
            return;

        var candidates = new List<EntityUid>();
        var targetXform = Transform(target);

        foreach (var entity in _lookup.GetEntitiesInRange(target, comp.Range, LookupFlags.Dynamic | LookupFlags.Sundries))
        {
            if (entity == target || !TryComp<MobStateComponent>(entity, out _) || !TryComp<DamageableComponent>(entity, out _))
                continue;

            if (Transform(entity).MapID != targetXform.MapID)
                continue;

            candidates.Add(entity);
        }

        if (candidates.Count == 0)
        {
            if (comp.FallbackDamage <= 0)
                return;

            var damage = new DamageSpecifier(_prototype.Index<DamageTypePrototype>("Blunt"), FixedPoint2.New(comp.FallbackDamage));
            _damageable.TryChangeDamage(target, damage, true, origin: target, ignoreBlockers: true);
            return;
        }

        var other = _random.Pick(candidates);
        var targetCoords = targetXform.Coordinates;
        var otherCoords = Transform(other).Coordinates;

        _transform.SetCoordinates(target, otherCoords);
        _transform.SetCoordinates(other, targetCoords);
    }
}
