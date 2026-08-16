// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Instruments;

/// <summary>A song baked into one-shot sample events.</summary>
[Prototype("sampledSong")]
public sealed partial class SampledSongPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public ProtoId<SampleBankPrototype> Bank;

    /// <summary>Note starts, ordered by time.</summary>
    [DataField(required: true)]
    public List<SampledNote> Notes = new();

    [DataField]
    public float Duration;
}

/// <summary>A sampled note start.</summary>
[DataDefinition]
public sealed partial class SampledNote
{
    [DataField(required: true)]
    public float Time;

    [DataField(required: true)]
    public int Key;

    [DataField]
    public byte Velocity = 110;
}
