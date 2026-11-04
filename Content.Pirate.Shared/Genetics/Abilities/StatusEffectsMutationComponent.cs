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
/// Adds permanetn status effects while this mutation is active.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(StatusEffectsMutationSystem))]
public sealed partial class StatusEffectsMutationComponent : Component
{
    [DataField(required: true)]
    public List<EntProtoId> StatusEffects = default!;
}
