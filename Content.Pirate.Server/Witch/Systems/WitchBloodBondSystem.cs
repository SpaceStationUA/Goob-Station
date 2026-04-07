using Content.Goobstation.Maths.FixedPoint;
using Content.Pirate.Shared.Witch.Components;
using Content.Shared.Damage;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;

namespace Content.Pirate.Server.Witch.Systems;

public sealed class WitchBloodBondSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private readonly HashSet<EntityUid> _suppressed = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WitchBloodBondComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(Entity<WitchBloodBondComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta is not { } delta || _suppressed.Contains(ent.Owner))
            return;

        var copiedDamage = DamageSpecifier.GetPositive(delta);
        if (copiedDamage.Empty || copiedDamage.GetTotal() <= FixedPoint2.Zero)
            return;

        _suppressed.Add(ent.Owner);

        try
        {
            foreach (var entity in _lookup.GetEntitiesInRange(ent.Owner, ent.Comp.Radius, LookupFlags.Dynamic | LookupFlags.Sundries))
            {
                if (entity == ent.Owner || !HasComp<MobStateComponent>(entity) || !HasComp<DamageableComponent>(entity))
                    continue;

                _suppressed.Add(entity);

                try
                {
                    _damageable.TryChangeDamage(entity, copiedDamage, true, origin: ent.Owner, ignoreBlockers: true);
                }
                finally
                {
                    _suppressed.Remove(entity);
                }
            }
        }
        finally
        {
            _suppressed.Remove(ent.Owner);
        }
    }
}
