// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Clothing.Accessories;

[RegisterComponent, NetworkedComponent]
[Access(typeof(ClothingAccessorySystem))]
public sealed partial class ClothingAccessoryHolderComponent : Component
{
    [DataField]
    public string ContainerId = "pirate-clothing-accessories";

    [DataField(required: true)]
    public Dictionary<ProtoId<ClothingAccessoryCategoryPrototype>, int> Limits = new();

    [DataField]
    public int MaxAccessories = 5;
}
