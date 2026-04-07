using Content.Shared.Radio;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Radio;

[RegisterComponent]
public sealed partial class RadioSoundProfileComponent : Component
{
    [DataField("transmitSound")]
    public SoundSpecifier? TransmitSound;

    [DataField("receiveSound")]
    public SoundSpecifier? ReceiveSound;

    [DataField("allowedChannels")]
    public List<ProtoId<RadioChannelPrototype>> AllowedChannels = new();
}
