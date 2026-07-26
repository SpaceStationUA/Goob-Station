// SPDX-License-Identifier: AGPL-3.0-or-later
// Pirate - ported from Trauma Station

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Viewcone.Components;

/// <summary>
/// Spawns a visual effect shown outside your vision cone when this entity makes a footstep sound.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ViewconeFootstepsEffectComponent : Component
{
    [DataField]
    public EntProtoId Effect = "ViewconeEffectFootstep";
}
