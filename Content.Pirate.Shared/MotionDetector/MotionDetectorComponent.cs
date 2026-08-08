// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Pirate.Shared.MotionDetector;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(MotionDetectorSystem))]
public sealed partial class MotionDetectorComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled;

    [DataField, AutoNetworkedField]
    public bool ShortRangeMode;

    [DataField, AutoNetworkedField]
    public int ShortRange = 7;

    [DataField, AutoNetworkedField]
    public int LongRange = 14;

    [DataField, AutoNetworkedField]
    public TimeSpan ShortRefresh = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public TimeSpan LongRefresh = TimeSpan.FromSeconds(2);

    [DataField, AutoNetworkedField]
    public TimeSpan MoveTime = TimeSpan.FromSeconds(2);

    [DataField, AutoNetworkedField]
    public TimeSpan ScanDuration = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextScanAt;

    [AutoNetworkedField]
    public TimeSpan LastScan;

    [AutoNetworkedField]
    public List<MotionDetectorBlip> Blips = new();

    [AutoNetworkedField]
    public EntityUid? LastUser;

    [DataField]
    public float PowerUse = 10f;

    [DataField]
    public bool DeactivateOnDrop = true;

    [DataField]
    public SoundSpecifier? ScanSound = new SoundPathSpecifier(
        "/Audio/_Pirate/Effects/motion_detector.ogg",
        AudioParams.Default.WithMaxDistance(7f));

    [DataField]
    public SoundSpecifier? EmptyScanSound = new SoundPathSpecifier(
        "/Audio/_Pirate/Effects/motion_detector_none.ogg");

    [DataField]
    public SoundSpecifier? ToggleSound = new SoundPathSpecifier(
        "/Audio/_Pirate/Machines/motion_detector_click.ogg");
}

[Serializable, NetSerializable]
public readonly record struct MotionDetectorBlip(MapCoordinates Coordinates);

[Serializable, NetSerializable]
public enum MotionDetectorVisualLayers : byte
{
    Setting,
    Number,
}

[Serializable, NetSerializable]
public enum MotionDetectorSetting : byte
{
    Short,
    Long,
}
