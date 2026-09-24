// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Pirate.SurveillanceCamera;

[RegisterComponent]
public sealed partial class RemoteStationEyeMonitorComponent : Component
{
    public readonly Dictionary<EntityUid, EntityUid> ViewerEyes = new();
}
