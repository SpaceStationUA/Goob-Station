// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Trigger.Components.Triggers;
using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Trigger;

/// <summary>
/// Triggers when this entity takes damage above a threshold.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(TriggerOnDamageSystem))]
[AutoGenerateComponentState]
public sealed partial class TriggerOnDamageComponent : BaseTriggerOnXComponent
{
    [DataField]
    public FixedPoint2 Threshold = 5;

    [DataField]
    public float Probability = 1f;
}
