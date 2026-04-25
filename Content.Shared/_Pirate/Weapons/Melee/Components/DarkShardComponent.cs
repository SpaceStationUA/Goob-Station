using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Weapons.Melee.Components;

/// <summary>
/// Configuration for the dark shard item. When used in-hand the shard is consumed,
/// permanently granting the holder the call-katana action.
/// </summary>
[RegisterComponent]
public sealed partial class DarkShardComponent : Component
{
    [DataField]
    public EntProtoId KatanaProto = "CursedKatana";

    [DataField]
    public float BloodCostPercent = 0.05f;

    [DataField]
    public EntProtoId ActionProto = "ActionCallCursedKatana";

    [DataField]
    public SoundSpecifier ConsumeSound = new SoundPathSpecifier("/Audio/Effects/demon_consume.ogg");

    [DataField]
    public SoundSpecifier SummonSound = new SoundPathSpecifier("/Audio/Effects/demon_attack1.ogg");

    [DataField]
    public SoundSpecifier RetractSound = new SoundPathSpecifier("/Audio/Effects/demon_consume.ogg");
}
