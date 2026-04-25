using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Weapons.Melee.Components;

/// <summary>
/// Added to a player entity when they consume a dark shard.
/// Tracks the permanently granted action and any currently summoned katana.
/// </summary>
[RegisterComponent]
public sealed partial class DarkKatanaUserComponent : Component
{
    [ViewVariables]
    public EntityUid? ActionEntity;

    [ViewVariables]
    public EntityUid? SummonedKatana;

    [DataField]
    public EntProtoId KatanaProto = "CursedKatana";

    [DataField]
    public float BloodCostPercent = 0.05f;

    [DataField]
    public SoundSpecifier SummonSound = new SoundPathSpecifier("/Audio/Effects/demon_attack1.ogg");

    [DataField]
    public SoundSpecifier RetractSound = new SoundPathSpecifier("/Audio/Effects/demon_consume.ogg");
}
