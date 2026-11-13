// Pirate: Starlight jumps, renamed to coexist with the native xenomorph jump.
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.StarlightSpecies.Movement;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StarlightJumpComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId Action = "Jump";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    [DataField, AutoNetworkedField]
    public bool IsEquipment = false;

    [DataField]
    public bool KnockdownTargetOnCollision = false;

    [DataField]
    public float KnockdownTargetDuration = 1f;

    [DataField]
    public bool KnockdownSelfOnCollision = false;

    [DataField]
    public float KnockdownSelfDuration = 1f;

    [DataField]
    public float Cooldown = 30f;
}
