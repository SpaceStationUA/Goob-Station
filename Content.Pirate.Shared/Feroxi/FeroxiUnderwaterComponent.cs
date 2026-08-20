using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Feroxi;

/// <summary>Allows a Feroxi to dive underwater.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FeroxiUnderwaterComponent : Component
{
    [DataField]
    public EntProtoId DiveAction = "ActionFeroxiDive";

    [DataField, AutoNetworkedField]
    public EntityUid? DiveActionEntity;

    [DataField]
    public EntProtoId SurfaceAction = "ActionFeroxiSurface";

    [DataField, AutoNetworkedField]
    public EntityUid? SurfaceActionEntity;

    [DataField]
    public ProtoId<AlertPrototype> UnderwaterAlert = "FeroxiUnderwater";

    [DataField, AutoNetworkedField]
    public bool IsUnderwater;

    [DataField, AutoNetworkedField]
    public EntityUid? WaterEntity;

    [DataField]
    public float SpeedModifier = 1.5f;

    [DataField]
    public float UnarmedDamageModifier = 2f;

    [ViewVariables]
    public bool RemovedFootstepTag;
}
