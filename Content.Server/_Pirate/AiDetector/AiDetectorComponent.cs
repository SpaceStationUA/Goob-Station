// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Pirate.AiDetector;

[RegisterComponent, Access(typeof(AiDetectorSystem))]
public sealed partial class AiDetectorComponent : Component
{
    [DataField]
    public string Default = "none";

    [DataField(required: true)]
    public List<AiDetectorRange> Ranges = new();

    [DataField]
    public string State = string.Empty;

    [DataField]
    public TimeSpan UpdateDelay = TimeSpan.FromSeconds(0.5);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextUpdate = TimeSpan.Zero;
}

[DataRecord]
public partial record struct AiDetectorRange(string State = "", float Range = 0f);
