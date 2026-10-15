using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Pirate.Server.Botany.Components;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class PlantAnalyzerComponent : Component
{
    [DataField]
    public EntityUid? ScannedEntity;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextUpdate = TimeSpan.Zero;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan? OutOfRangeSince = null;

    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    [DataField]
    public PlantAnalyzerSettings Settings = new();

    [DataField]
    public SoundSpecifier? ScanningEndSound;

    // Pirate: retain the existing report printer and its timings/sound.
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan PrintReadyAt = TimeSpan.Zero;

    [DataField]
    public TimeSpan PrintCooldown = TimeSpan.FromSeconds(5);

    [DataField]
    public SoundSpecifier SoundPrint = new SoundPathSpecifier("/Audio/Machines/short_print_and_rip.ogg");

    [DataField]
    public EntProtoId MachineOutput = "PlantAnalyzerReportPaper";
}

[DataRecord]
public partial struct PlantAnalyzerSettings
{
    [DataField]
    public float ScanDelay = 1.5f;

    [DataField]
    public float MaxScanRange = 2f;

    public PlantAnalyzerSettings()
    {
    }
}
