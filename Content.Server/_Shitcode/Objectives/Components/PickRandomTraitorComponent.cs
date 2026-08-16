// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.Objectives.Systems;
using Content.Server.Objectives.Components;

namespace Content.Server._Goobstation.Objectives.Components;

/// <summary>
/// Sets the target for <see cref="TargetObjectiveComponent"/> to a random traitor.
/// </summary>
[RegisterComponent, Access(typeof(PickRandomTraitorSystem))]
public sealed partial class PickRandomTraitorComponent : Component
{
    /// <summary>
    /// Minimum reputation to require, or 0 for no requirement.
    /// </summary>
    [DataField]
    public int MinReputation;
}
