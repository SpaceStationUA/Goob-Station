// SPDX-License-Identifier: AGPL-3.0-or-later

// Pirate: explicit imports supplied by Trauma global usings.
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Shared.EntityEffects;

namespace Content.Pirate.Shared.Genetics.Abilities;

/// <summary>
/// Runs entity effects when this mutation is added or removed.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(EffectsMutationSystem))]
public sealed partial class EffectsMutationComponent : Component
{
    /// <summary>
    /// The effects ran on the target when this mutation is added.
    /// </summary>
    [DataField]
    public EntityEffect[] Added = [];

    /// <summary>
    /// The effects ran on the target when this mutation is removed.
    /// </summary>
    [DataField]
    public EntityEffect[] Removed = [];

    /// <summary>
    /// Optional cleanup for transfers and other automatic removals, without a manual cure's side effects.
    /// Local polymorphs restore the old anatomy before applying the mutation to a new body.
    /// </summary>
    [DataField]
    public EntityEffect[]? AutomaticRemoved;

    /// <summary>
    /// If true, doesn't run effects for automatic mutation adding/removing (polymorph).
    /// </summary>
    [DataField]
    public bool IgnoreAutomatic;
}
