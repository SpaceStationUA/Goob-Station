// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Chemistry.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Fluids;

/// <summary>
/// Spills its configured solution on map initialization and then removes itself.
/// </summary>
[EntityCategory("Spawner")]
[RegisterComponent, NetworkedComponent]
public sealed partial class PuddleSpawnerComponent : Component
{
    [DataField(required: true)]
    public Solution Solution = default!;
}
