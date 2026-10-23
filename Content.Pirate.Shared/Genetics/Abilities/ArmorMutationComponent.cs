// SPDX-License-Identifier: AGPL-3.0-or-later

// Pirate: explicit imports supplied by Trauma global usings.
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Shared.Damage;

namespace Content.Pirate.Shared.Genetics.Abilities;

/// <summary>
/// Modifies incoming damage for the mutated mob.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(ArmorMutationSystem))]
public sealed partial class ArmorMutationComponent : Component
{
    [DataField(required: true)]
    public DamageModifierSet Modifiers = new();
}
