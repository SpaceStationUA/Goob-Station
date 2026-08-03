using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.ZLevels.View;

/// <summary>
/// Grants remote-eye z-level actions only while the controlled eye has an adjacent deck.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEZLevelEyeMoverComponent : Component
{
    [DataField]
    public EntProtoId UpActionProto = "ActionStationAiViewUp";

    [DataField, AutoNetworkedField]
    public EntityUid? UpActionEntity;

    [DataField]
    public EntProtoId DownActionProto = "ActionStationAiViewDown";

    [DataField, AutoNetworkedField]
    public EntityUid? DownActionEntity;
}
