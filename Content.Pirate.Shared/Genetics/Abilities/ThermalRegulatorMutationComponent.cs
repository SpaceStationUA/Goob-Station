// SPDX-License-Identifier: AGPL-3.0-or-later

// Pirate: explicit imports supplied by Trauma global usings.
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;


namespace Content.Pirate.Shared.Genetics.Abilities;

/// <summary>
/// Mutation component to multiply <c>ThermalRegulator</c> fields while added.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ThermalRegulatorMutationComponent : Component
{
    [DataField]
    public float Shivering = 1f;

    [DataField]
    public float Sweating = 1f;

    [DataField]
    public float Metabolism = 1f;

    [DataField]
    public float Regulation = 1f;
}
