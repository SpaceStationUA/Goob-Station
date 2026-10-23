// SPDX-License-Identifier: AGPL-3.0-or-later

// Pirate: explicit imports supplied by Trauma global usings.
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Mobs;

namespace Content.Pirate.Shared.Genetics.Abilties;

/// <summary>
/// Removes a mob state from the target mob's thresholds and allowed states.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(MobThresholdMutationSystem))]
[AutoGenerateComponentState]
public sealed partial class MobThresholdMutationComponent : Component
{
    /// <summary>
    /// Removes this mob state from the thresholds and allowed states.
    /// </summary>
    [DataField(required: true)]
    public MobState Removed = MobState.Invalid;

    /// <summary>
    /// The threshold it had, if it was present.
    /// Used when removing the mutation.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2? OldThreshold;
}
