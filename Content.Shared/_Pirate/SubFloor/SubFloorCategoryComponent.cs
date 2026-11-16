// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Pirate.SubFloor;

/// <summary>Use when the client cannot infer the category, such as for disposal tubes.</summary>
[RegisterComponent]
public sealed partial class SubFloorCategoryComponent : Component
{
    [DataField(required: true)]
    public SubFloorCategory Category;
}

public enum SubFloorCategory : byte
{
    Wires,
    Pipes,
}
