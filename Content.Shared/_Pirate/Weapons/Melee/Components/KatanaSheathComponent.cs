using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Utility;

namespace Content.Shared._Pirate.Weapons.Melee.Components;

[RegisterComponent]
public sealed partial class KatanaSheathComponent : Component
{
    [DataField("slot")]
    public string Slot = "item";

    /// <summary>
    /// Fallback sprite used when the slotted item has no <see cref="KatanaSheathHandleComponent"/>.
    /// Allows sheaths that accept a fixed item type to define their own appearance.
    /// </summary>
    [DataField]
    public ResPath? FallbackSprite;

    [DataField]
    public string? FallbackInventoryState;

    [DataField]
    public string? FallbackBeltState;

    [DataField]
    public string? FallbackBackpackState;
}
