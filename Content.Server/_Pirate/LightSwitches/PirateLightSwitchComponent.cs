// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Pirate.LightSwitches;

[RegisterComponent, Access(typeof(PirateLightSwitchSystem))]
public sealed partial class PirateLightSwitchComponent : Component
{
    [DataField]
    public bool StartsOff;

    // Saved, so a persistence save of a running map keeps the switch in step with its lamps (PoweredLight.On is saved too).
    [DataField]
    public bool IsOn = true;
}
