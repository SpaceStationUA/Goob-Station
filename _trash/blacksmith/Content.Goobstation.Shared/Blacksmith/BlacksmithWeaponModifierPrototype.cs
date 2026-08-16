using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.Blacksmith;

/// <summary>
/// Random forge quality that can be rolled onto weapons produced by a blacksmith anvil.
/// Each modifier is rolled independently and can stack with others unless marked exclusive/incompatible.
/// </summary>
[Prototype("blacksmithWeaponModifier")]
public sealed partial class BlacksmithWeaponModifierPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Chance from 0 to 1 that this modifier is applied when forging a weapon.
    /// </summary>
    [DataField(required: true)]
    public double Chance;

    /// <summary>
    /// LocId for the short quality name shown in the item name (e.g. "тупа").
    /// </summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// Multiplier applied to melee damage. 1 = unchanged.
    /// </summary>
    [DataField]
    public float DamageMultiplier = 1f;

    /// <summary>
    /// Multiplier applied to melee attack rate. 1 = unchanged.
    /// </summary>
    [DataField]
    public float AttackRateMultiplier = 1f;

    /// <summary>
    /// Multiplier applied to walk/sprint speed while the weapon is held. 1 = unchanged.
    /// </summary>
    [DataField]
    public float HeldSpeedMultiplier = 1f;

    /// <summary>
    /// Flat bonus damage added on each melee hit (stacks additively across modifiers).
    /// </summary>
    [DataField]
    public DamageSpecifier? BonusDamage;

    /// <summary>
    /// Chance (0–1) to deal <see cref="CritDamage"/> as bonus damage on a melee hit.
    /// </summary>
    [DataField]
    public float CritChance;

    /// <summary>
    /// Flat bonus damage dealt when a critical hit procs.
    /// </summary>
    [DataField]
    public DamageSpecifier? CritDamage;

    /// <summary>
    /// Reagents injected into the target on hit.
    /// </summary>
    [DataField]
    public List<ReagentQuantity>? InjectReagents;

    /// <summary>
    /// Optional cap for injected reagent amount on the target.
    /// </summary>
    [DataField]
    public float? InjectReagentLimit;

    /// <summary>
    /// If true, this modifier is treated as a debuff for forge outcome pairing.
    /// </summary>
    [DataField]
    public bool IsDebuff;

    /// <summary>
    /// If true, this modifier cannot stack with any other — it becomes the sole applied quality.
    /// </summary>
    [DataField]
    public bool Exclusive;

    /// <summary>
    /// Modifier IDs that cannot be applied together with this one.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<BlacksmithWeaponModifierPrototype>> IncompatibleWith = new();

    /// <summary>
    /// If non-empty, this modifier can only roll on these entity prototypes.
    /// </summary>
    [DataField]
    public HashSet<EntProtoId> AllowedPrototypes = new();
}
