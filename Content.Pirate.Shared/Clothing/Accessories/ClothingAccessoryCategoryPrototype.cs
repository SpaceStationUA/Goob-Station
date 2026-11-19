// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Clothing.Accessories;

[Prototype("clothingAccessoryCategory")]
public sealed partial class ClothingAccessoryCategoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Slot = "neck";

    [DataField]
    public List<Vector2> Offsets = new();
}
