// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.Maps;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Pirate.Shared.DeepSpaceScanner;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class DeepSpaceScannerComponent : Component
{
    [DataField]
    public float Range = 75f;

    // Keep this interval aligned with the screen animation duration.
    [DataField]
    public TimeSpan ScanInterval = TimeSpan.FromSeconds(7.5);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextScan;

    /// <summary>Tiles treated as exterior when detecting off-station mobs.</summary>
    [DataField]
    public HashSet<ProtoId<ContentTileDefinition>> ExteriorTiles = new()
    {
        "Lattice",
        "TrainLattice",
        "LatticeDiagonalNW",
        "LatticeDiagonalNE",
        "LatticeDiagonalSW",
        "LatticeDiagonalSE",
    };

    [DataField]
    public SoundSpecifier? ScanSound = new SoundPathSpecifier(
        "/Audio/_Pirate/Effects/motion_detector.ogg",
        AudioParams.Default.WithMaxDistance(7f));

    [DataField]
    public SoundSpecifier? EmptyScanSound = new SoundPathSpecifier(
        "/Audio/_Pirate/Effects/motion_detector_none.ogg");

    [DataField]
    public TimeSpan WaveDuration = TimeSpan.FromSeconds(1.25);

    [DataField]
    public Color Color = Color.FromHex("#FF3B30");

    [DataField]
    public float OtherLevelAlpha = 0.4f;

    [DataField]
    public float FadedAlpha;

    [DataField]
    public TimeSpan FadeEndBeforeScan = TimeSpan.FromSeconds(1);

    [DataField]
    public float DotRadius = 1.8f;

    // Server-side cache; only viewers with the radar UI open receive a scan event.
    [ViewVariables]
    public DeepSpaceScanEvent? LastScan;

    [ViewVariables]
    public MapId LastScanMap;
}

[Serializable, NetSerializable]
public enum DeepSpaceScannerVisuals : byte
{
    /// <summary>Server timestamp anchors the one-cycle screen animation.</summary>
    ScanStart,
}

[Serializable, NetSerializable]
public readonly record struct DeepSpacePing(Vector2 Position, bool Grid, bool OtherLevel);

[Serializable, NetSerializable]
public sealed class DeepSpaceScanEvent(NetEntity scanner, Vector2 origin, TimeSpan scanTime, List<DeepSpacePing> pings)
    : EntityEventArgs
{
    public readonly NetEntity Scanner = scanner;

    public readonly Vector2 Origin = origin;

    public readonly TimeSpan ScanTime = scanTime;

    public readonly List<DeepSpacePing> Pings = pings;
}
