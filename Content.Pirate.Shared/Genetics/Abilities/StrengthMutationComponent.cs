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
/// Component for the strength mutation.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(StrengthMutationSystem))]
public sealed partial class StrengthMutationComponent : Component
{
    /// <summary>
    /// Modifier for melee damage dealt.
    /// </summary>
    [DataField]
    public float MeleeModifier = 1.2f;
}
