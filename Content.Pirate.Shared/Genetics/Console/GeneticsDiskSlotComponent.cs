// SPDX-License-Identifier: AGPL-3.0-or-later

// Pirate: explicit imports supplied by Trauma global usings.
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;


namespace Content.Pirate.Shared.Genetics.Console;

[RegisterComponent, NetworkedComponent, Access(typeof(GeneticsDiskSystem))]
public sealed partial class GeneticsDiskSlotComponent : Component
{
    /// <summary>
    /// Name of the item slot that holds a genetics disk.
    /// </summary>
    [DataField]
    public string DiskSlot = "genetics_disk";
}
