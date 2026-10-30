// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Radio;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.VoiceMask;

public sealed partial class VoiceMaskComponent
{
    [DataField]
    public bool TransformSay = true;

    [DataField]
    public bool TransformWhisper = true;

    // Exclusions make new channels use the mask voice by default.
    [DataField]
    public HashSet<ProtoId<RadioChannelPrototype>> ExcludedRadioChannels = new();

    [DataField]
    public bool AltToggle = true;

    [DataField]
    public SoundSpecifier? ToggleSound = new SoundPathSpecifier("/Audio/Machines/button.ogg", AudioParams.Default.WithVolume(-2f));
}
