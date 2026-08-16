using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.Blacksmith;

/// <summary>
/// Marks a lathe (e.g. blacksmith anvil) that rolls forge modifiers onto produced melee weapons.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BlacksmithAnvilComponent : Component
{
    /// <summary>
    /// Explicit modifier list. If empty, all <see cref="BlacksmithWeaponModifierPrototype"/>s are used.
    /// </summary>
    [DataField]
    public List<ProtoId<BlacksmithWeaponModifierPrototype>> Modifiers = new();

    /// <summary>
    /// Last player who queued a recipe (checks <see cref="BlacksmithKnowledgeComponent"/>).
    /// </summary>
    [ViewVariables]
    public EntityUid? LastCrafter;
}
