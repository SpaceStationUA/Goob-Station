using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.VulpkaninJump;

[RegisterComponent]
public sealed partial class VulpkaninJumpComponent : Component
{
    [DataField]
    public SoundSpecifier? JumpSound;

    [DataField]
    public float JumpSpeed = 7f;

    [DataField]
    public float StaminaCost = 40f;

    [DataField]
    public TimeSpan StunTime = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan WallParalyzeTime = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan WallKnockdownTime = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan CollisionKnockdownTime = TimeSpan.FromSeconds(1);

    [DataField]
    public EntProtoId JumpAction = "ActionJumpVulpkanin";

    [ViewVariables]
    public EntityUid? JumpActionEntity;
}

public sealed partial class VulpkaninJumpActionEvent : WorldTargetActionEvent;
