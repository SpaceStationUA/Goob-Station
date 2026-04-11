using System;
using System.Collections.Generic;
using Content.Shared.Damage;
using Content.Shared.DeviceLinking;
using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Pirate.Server._JustDecor.Scripts.Components;

[RegisterComponent]
public sealed partial class SignalNpcComponent : Component
{
    [DataField("responses")]
    public List<SignalNpcResponse> Responses = new();
}

[DataDefinition]
public sealed partial class SignalNpcResponse
{
    [DataField("port", required: true, customTypeSerializer: typeof(PrototypeIdSerializer<SinkPortPrototype>))]
    public string Port = "Trigger";

    [DataField("message")]
    public string? Message;

    [DataField("emoteId")]
    public string? EmoteId;

    [DataField("damage")]
    public DamageSpecifier? Damage;

    [DataField("sound")]
    public SoundSpecifier? Sound;

    [DataField("forwardPort", customTypeSerializer: typeof(PrototypeIdSerializer<SourcePortPrototype>))]
    public string? ForwardPort;
}
