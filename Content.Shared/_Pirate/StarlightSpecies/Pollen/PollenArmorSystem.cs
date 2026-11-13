// Pirate: Starlight bark/moss armor status effects adapted to native damage modifiers.
using Content.Shared.Armor;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._Pirate.StarlightSpecies.Pollen;

[RegisterComponent]
public sealed partial class BonusArmorStatusEffectComponent : Component
{
    [DataField(required: true)] public DamageModifierSet Modifiers = default!;
}

[RegisterComponent]
public sealed partial class PollenArmorComponent : Component
{
    public Dictionary<EntityUid, DamageModifierSet> Modifiers = new();
}

public sealed class PollenArmorSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<BonusArmorStatusEffectComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<BonusArmorStatusEffectComponent, StatusEffectRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<PollenArmorComponent, DamageModifyEvent>(OnDamage);
        SubscribeLocalEvent<PollenArmorComponent, CoefficientQueryEvent>(OnCoefficients);
    }

    private void OnApplied(Entity<BonusArmorStatusEffectComponent> ent, ref StatusEffectAppliedEvent args) =>
        EnsureComp<PollenArmorComponent>(args.Target).Modifiers[ent] = new()
        {
            Coefficients = new(ent.Comp.Modifiers.Coefficients),
            FlatReduction = new(ent.Comp.Modifiers.FlatReduction),
        };

    private void OnRemoved(Entity<BonusArmorStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (!TryComp<PollenArmorComponent>(args.Target, out var armor))
            return;
        armor.Modifiers.Remove(ent);
        if (armor.Modifiers.Count == 0)
            RemComp<PollenArmorComponent>(args.Target);
    }

    private void OnDamage(EntityUid uid, PollenArmorComponent comp, DamageModifyEvent args)
    {
        foreach (var modifiers in comp.Modifiers.Values)
            args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage,
                DamageSpecifier.PenetrateArmor(modifiers, args.Damage.ArmorPenetration));
    }

    private void OnCoefficients(EntityUid uid, PollenArmorComponent comp, CoefficientQueryEvent args)
    {
        foreach (var modifiers in comp.Modifiers.Values)
            foreach (var (type, coefficient) in modifiers.Coefficients)
                args.DamageModifiers.Coefficients[type] = args.DamageModifiers.Coefficients.GetValueOrDefault(type, 1f) * coefficient;
    }
}
