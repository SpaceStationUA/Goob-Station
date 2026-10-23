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
/// Mutation that modifies bleeding rate and blood refresh rate.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(BleedingMutationSystem))]
public sealed partial class BleedingMutationComponent : Component
{
    [DataField]
    public float BleedModifier = 1.5f;

    [DataField]
    public float RefreshModifier = 6f;
}
