using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Utility;

namespace Content.Shared._Pirate.Weapons.Melee.Components;

[RegisterComponent]
public sealed partial class KatanaSheathHandleComponent : Component
{
    [DataField("sprite", required: true)]
    public ResPath Sprite = default!;

    [DataField("inventoryState", required: true)]
    public string InventoryState = default!;

    [DataField("beltState", required: true)]
    public string BeltState = default!;

    [DataField("backpackState", required: true)]
    public string BackpackState = default!;
}
