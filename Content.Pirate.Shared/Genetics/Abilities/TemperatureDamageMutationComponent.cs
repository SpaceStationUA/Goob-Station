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
/// Adds an offset to cold and/or heat damage thresholds.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(TemperatureDamageMutationSystem))]
public sealed partial class TemperatureDamageMutationComponent : Component
{
    [DataField]
    public float ColdOffset;

    [DataField]
    public float HeatOffset;
}
