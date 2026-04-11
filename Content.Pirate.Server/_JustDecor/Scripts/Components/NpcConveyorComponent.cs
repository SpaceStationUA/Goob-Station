using System;
using Content.Shared.DeviceLinking;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Pirate.Server._JustDecor.Scripts.Components;

[RegisterComponent]
public sealed partial class NpcConveyorComponent : Component
{
    [DataField("enabled")]
    public bool Enabled;

    [DataField("searchRange")]
    public float SearchRange = 0.35f;

    [DataField("retargetInterval")]
    public float RetargetInterval = 0.5f;

    [DataField("startPort", customTypeSerializer: typeof(PrototypeIdSerializer<SinkPortPrototype>))]
    public string StartPort = "Start";

    [DataField("stopPort", customTypeSerializer: typeof(PrototypeIdSerializer<SinkPortPrototype>))]
    public string StopPort = "Stop";

    [DataField("togglePort", customTypeSerializer: typeof(PrototypeIdSerializer<SinkPortPrototype>))]
    public string TogglePort = "Toggle";
}
