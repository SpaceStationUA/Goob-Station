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
/// Triggers every time this entity walks enough to make a footstep sound.
/// Does nothing if you can't make footstep sounds e.g. with ninja shoes.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class TriggerOnWalkComponent : BaseTriggerOnXComponent;
