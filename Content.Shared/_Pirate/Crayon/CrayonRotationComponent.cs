// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Crayon;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(DecalRotationSystem))]
public sealed partial class CrayonRotationComponent : Component
{
    [DataField, AutoNetworkedField]
    public Angle Rotation;
}
