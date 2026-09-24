// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Pirate.SurveillanceCamera;

/// <summary>Records the eye and monitor to restore a viewer when observation ends.</summary>
[RegisterComponent]
public sealed partial class RemoteStationEyeViewerComponent : Component
{
    public EntityUid Monitor;
    public EntityUid Eye;
    public EntityUid? ExitAction;
    public EntityUid? PreviousEyeTarget;
    public int PreviousVisibilityMask;
    public bool PreviousDrawFov;
    public bool HadInteractionBlock;
    public bool PreviousBlockInteraction;
    public bool PreviousBlockUse;
}
