using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.Blacksmith;

/// <summary>
/// Applied forge qualities on a weapon produced by a blacksmith anvil.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedBlacksmithAnvilSystem))]
public sealed partial class BlacksmithForgedWeaponComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<ProtoId<BlacksmithWeaponModifierPrototype>> Modifiers = new();

    /// <summary>
    /// Entity name before forge qualities were appended.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? BaseName;

    [ViewVariables, AutoNetworkedField]
    public float DamageMultiplier = 1f;

    [ViewVariables, AutoNetworkedField]
    public float AttackRateMultiplier = 1f;

    [ViewVariables, AutoNetworkedField]
    public float HeldSpeedMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public DamageSpecifier BonusDamage = new();

    [ViewVariables, AutoNetworkedField]
    public float CritChance;

    [DataField, AutoNetworkedField]
    public DamageSpecifier CritDamage = new();
}
