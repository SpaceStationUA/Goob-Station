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
/// Enables and disables a mutation's radiation source when alive or dead.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class RadiationMutationComponent : Component;
