// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Pirate.Instruments;

/// <summary>Plays a sampled song from an entity.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class SampledSongPlayerComponent : Component
{
    [DataField(required: true)]
    public ProtoId<SampledSongPrototype> Song;

    /// <summary>Default means the next tick.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan StartTime;

    [DataField]
    public int NextNote;

    [DataField]
    public bool Started;

    [DataField]
    public float Range = 10f;

    /// <summary>Applied after per-note velocity scaling.</summary>
    [DataField]
    public float Volume = 6f;

    [DataField]
    public bool Loop;
}
