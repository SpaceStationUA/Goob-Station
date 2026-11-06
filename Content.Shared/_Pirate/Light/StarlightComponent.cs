// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Light;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StarlightComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color? Color;

    [DataField, AutoNetworkedField]
    public float? Strength;
}
