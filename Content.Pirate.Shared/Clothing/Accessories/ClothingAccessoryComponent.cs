// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Pirate.Shared.Clothing.Accessories;

[RegisterComponent, NetworkedComponent]
[Access(typeof(ClothingAccessorySystem))]
public sealed partial class ClothingAccessoryComponent : Component
{
    [DataField(required: true)]
    public ProtoId<ClothingAccessoryCategoryPrototype> Category;

    [DataField]
    public string? EquippedState;

    [DataField]
    public ResPath? Sprite;

    [DataField]
    public bool AllowDuplicates = true;

    [DataField]
    public TimeSpan AttachDelay = TimeSpan.FromSeconds(3);
}
