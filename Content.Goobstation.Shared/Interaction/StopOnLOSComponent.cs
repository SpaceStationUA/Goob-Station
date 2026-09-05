// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.Interaction;

/// <summary>
/// Freezes movement/attacks while a living mind is looking at this entity within angle+LOS.
/// Used by halloween slendermime.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StopOnLOSComponent : Component
{
    [AutoNetworkedField]
    public bool CanMove = false;

    [DataField, AutoNetworkedField]
    public float SightRange = 12f;

    [DataField, AutoNetworkedField]
    public float SightAngle = 120f;
}
