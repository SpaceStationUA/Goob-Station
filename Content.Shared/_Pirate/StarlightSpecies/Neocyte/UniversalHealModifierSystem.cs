using System.Linq;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.StarlightSpecies.Neocyte;

[RegisterComponent, NetworkedComponent]
public sealed partial class UniversalHealModifierComponent : Component
{
    [DataField]
    public float Modifier = 1f;
}

/// <summary>Starlight's neocyte healing penalty, including medicine and welding.</summary>
public sealed class UniversalHealModifierSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damage = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<UniversalHealModifierComponent, BeforeDamageChangedEvent>(OnHeal);
        SubscribeLocalEvent<BodyPartComponent, BeforeDamageChangedEvent>(OnPartHeal);
    }

    private void OnHeal(Entity<UniversalHealModifierComponent> ent, ref BeforeDamageChangedEvent args)
        => ModifyHealing(args.Damage, ent.Comp.Modifier);

    private void OnPartHeal(Entity<BodyPartComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (ent.Comp.Body is not { } body ||
            HasComp<UniversalHealModifierComponent>(ent) ||
            _damage.IsApplyingBodyDamage(body) ||
            !TryComp<UniversalHealModifierComponent>(body, out var modifier))
            return;

        ModifyHealing(args.Damage, modifier.Modifier);
    }

    private static void ModifyHealing(DamageSpecifier damage, float modifier)
    {
        foreach (var (type, amount) in damage.DamageDict.ToArray())
        {
            if (amount < 0)
                damage.DamageDict[type] = amount * modifier;
        }
    }
}
