/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.ZLevels.Weightless;

[RegisterComponent]
public sealed partial class CEWeightlessZLevelMoverComponent : Component
{
    [DataField]
    public EntProtoId UpActionProto = "CEActionWeightlessZLevelUp";

    [DataField]
    public EntityUid? ZLevelUpActionEntity;

    [DataField]
    public EntProtoId DownActionProto = "CEActionWeightlessZLevelDown";

    [DataField]
    public EntityUid? ZLevelDownActionEntity;

    public TimeSpan NextMove;
}
