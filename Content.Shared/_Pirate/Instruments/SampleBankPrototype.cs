// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Instruments;

/// <summary>A one-octave bank of samples for rendering notes.</summary>
[Prototype("sampleBank")]
public sealed partial class SampleBankPrototype : IPrototype
{
    private const int PitchClasses = 12;

    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Samples ordered by pitch class from C to B.</summary>
    [DataField(required: true)]
    public List<SoundSpecifier> Samples = new();

    /// <summary>Recorded octave's C key; 60 is middle C.</summary>
    [DataField]
    public int BaseKey = 60;

    [DataField]
    public int OctaveRange = 2;

    public (SoundSpecifier Sample, float PitchScale) Resolve(int key)
    {
        key = Math.Clamp(key, 0, 127);

        var sample = Samples[key % PitchClasses % Samples.Count];

        var octaves = Math.Clamp(key / PitchClasses - Math.Clamp(BaseKey, 0, 127) / PitchClasses,
            -OctaveRange,
            OctaveRange);

        return (sample, MathF.Pow(2f, octaves));
    }
}
