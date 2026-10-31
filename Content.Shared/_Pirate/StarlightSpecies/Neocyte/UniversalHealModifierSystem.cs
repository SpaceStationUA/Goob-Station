using System.Linq;
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
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<UniversalHealModifierComponent, BeforeDamageChangedEvent>(OnHeal);
    }

    private void OnHeal(Entity<UniversalHealModifierComponent> ent, ref BeforeDamageChangedEvent args)
    {
        foreach (var (type, amount) in args.Damage.DamageDict.ToArray())
        {
            if (amount < 0)
                args.Damage.DamageDict[type] = amount * ent.Comp.Modifier;
        }
    }
}
