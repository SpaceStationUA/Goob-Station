// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Pirate.Shared.SurveillanceCamera;

[RegisterComponent]
public sealed partial class ToggleableCameraComponent : Component
{
    [DataField]
    public LocId EmpPopup = "pirate-toggleable-camera-emp";
}
