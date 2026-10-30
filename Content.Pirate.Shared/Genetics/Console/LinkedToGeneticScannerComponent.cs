// SPDX-License-Identifier: AGPL-3.0-or-later

// Pirate: explicit imports supplied by Trauma global usings.
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;


namespace Content.Pirate.Shared.Genetics.Console;

/// <summary>
/// Component added to mobs while they are linked to a handheld genetics scanner.
/// When they leave a range it will unlink.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(GeneticsConsoleSystem))]
[AutoGenerateComponentState]
public sealed partial class LinkedToGeneticScannerComponent : Component
{
    /// <summary>
    /// Square of the range at which it will unlink.
    /// </summary>
    [DataField]
    public float RangeSquared = 5f * 5f;

    /// <summary>
    /// The scanner(s) this mob is linked to.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<EntityUid> Scanners = new();
}
