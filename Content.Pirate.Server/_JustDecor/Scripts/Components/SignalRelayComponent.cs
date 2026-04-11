using Content.Shared.DeviceLinking;
using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Pirate.Server._JustDecor.Scripts.Components;

[RegisterComponent]
public sealed partial class SignalRelayComponent : Component
{
    [DataField("sinkPort", customTypeSerializer: typeof(PrototypeIdSerializer<SinkPortPrototype>))]
    public string SinkPort = "Trigger";

    [DataField("outputPort", customTypeSerializer: typeof(PrototypeIdSerializer<SourcePortPrototype>))]
    public string? OutputPort;

    [DataField("sound")]
    public SoundSpecifier? Sound;
}
