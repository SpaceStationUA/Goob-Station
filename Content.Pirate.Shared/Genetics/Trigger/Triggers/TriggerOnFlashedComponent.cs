// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Shared.Trigger.Components.Triggers;

namespace Content.Pirate.Shared.Genetics.Trigger.Triggers;

/// <summary>
/// Triggers when this entity is directly flashed or area flashed.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(TriggerOnFlashedSystem))]
[AutoGenerateComponentState]
public sealed partial class TriggerOnFlashedComponent : BaseTriggerOnXComponent
{
    /// <summary>
    /// Probability of being triggered when flashed.
    /// </summary>
    [DataField]
    public float Prob = 1f;
}
