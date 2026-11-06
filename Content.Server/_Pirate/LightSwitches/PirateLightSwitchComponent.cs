// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Pirate.LightSwitches;

[RegisterComponent, Access(typeof(PirateLightSwitchSystem))]
public sealed partial class PirateLightSwitchComponent : Component
{
    [DataField]
    public bool StartsOff;

    [ViewVariables]
    public bool IsOn = true;
}
