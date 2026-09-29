// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Inventory;

namespace Content.Pirate.Shared.Emp;

/// <summary>
/// Clothing that shields its wearer, and everything the wearer carries or has implanted, from EMP pulses
/// while it is worn in one of <see cref="Slots"/>.
/// </summary>
[RegisterComponent]
public sealed partial class EmpShieldClothingComponent : Component
{
    [DataField]
    public SlotFlags Slots = SlotFlags.OUTERCLOTHING;
}

/// <summary>
/// Added to a wearer while at least one <see cref="EmpShieldClothingComponent"/> item is worn.
/// </summary>
[RegisterComponent]
public sealed partial class EmpShieldedWearerComponent : Component
{
    [ViewVariables]
    public HashSet<EntityUid> Sources = new();
}
