using Content.Shared.Inventory;
using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Clothing.ExaminableClothing;

/// <summary>
/// Clothing that contributes to the wearer's examine message when worn.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ExaminableClothingComponent : Component
{
    /// <summary>
    /// Localization ID of the examine text to display. Defaults to the item's name.
    /// </summary>
    [DataField]
    public LocId? ExamineText = null;

    /// <summary>
    /// Only adds the examine text in the given slots.
    /// </summary>
    [DataField]
    public SlotFlags AllowedSlots = SlotFlags.WITHOUT_POCKET;
}
