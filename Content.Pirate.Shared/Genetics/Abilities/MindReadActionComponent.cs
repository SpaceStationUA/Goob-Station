// SPDX-License-Identifier: AGPL-3.0-or-later

// Pirate: explicit imports supplied by Trauma global usings.
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Shared.Actions;

namespace Content.Pirate.Shared.Genetics.Abilities;

/// <summary>
/// Action component for use with <see cref="MindReadActionEvent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MindReadActionComponent : Component
{
    /// <summary>
    /// Probability of alerting the read-ee their mind is being read.
    /// </summary>
    [DataField]
    public float AlertProb = 0.2f;

    /// <summary>
    /// Maximum number of messages to try reveal.
    /// </summary>
    [DataField]
    public int MaxMessages = 3;

    /// <summary>
    /// Chance of each message being revealed.
    /// </summary>
    [DataField]
    public float MessageChance = 0.5f;
}

public sealed partial class MindReadActionEvent : EntityTargetActionEvent;
