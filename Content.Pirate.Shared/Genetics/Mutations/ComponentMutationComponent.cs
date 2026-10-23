// SPDX-License-Identifier: AGPL-3.0-or-later

// Pirate: explicit imports supplied by Trauma global usings.
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;


namespace Content.Pirate.Shared.Genetics.Mutations;

/// <summary>
/// Adds or removes components from the target mob when mutated and reverts it when removed.
/// </summary>
[RegisterComponent, Access(typeof(ComponentMutationSystem))]
public sealed partial class ComponentMutationComponent : Component
{
    /// <summary>
    /// Components added to the mutated entity when active.
    /// Existing components are preserved.
    /// </summary>
    [DataField]
    public ComponentRegistry? Added;

    /// <summary>
    /// Components removed from the mutated entity when active.
    /// Existing component data is saved and restored on removal.
    /// </summary>
    [DataField]
    public ComponentRegistry? Removed;

    // Pirate: remember only changes made to the current host, including removed component data.
    [DataField]
    public ComponentRegistry AddedComponents = new();

    [DataField]
    public ComponentRegistry RemovedComponents = new();
}
