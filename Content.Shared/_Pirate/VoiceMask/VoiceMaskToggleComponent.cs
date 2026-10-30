// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.VoiceMask;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VoiceMaskToggleComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active = true;
}
