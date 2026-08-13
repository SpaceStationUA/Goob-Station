// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Audio;

[RegisterComponent]
public sealed partial class SequencedSoundLoopComponent : Component
{
    [DataField]
    public SoundSpecifier? StartSound;

    [DataField]
    public TimeSpan StartLength = TimeSpan.Zero;

    [DataField]
    public ProtoId<SoundCollectionPrototype>? MidSounds;

    [DataField]
    public TimeSpan MidLength = TimeSpan.FromSeconds(1);

    [DataField]
    public SoundSpecifier? EndSound;

    [DataField]
    public AudioParams Params = AudioParams.Default;

    [DataField]
    public AudioParams? StartParams;

    [ViewVariables]
    public bool Running;

    [ViewVariables]
    public bool LoopStarted;

    [ViewVariables]
    public int MidIndex;

    [ViewVariables]
    public TimeSpan NextPlayTime;
}
