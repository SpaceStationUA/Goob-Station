// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.ZLevels.Movement;

/// <summary>Enables z-level movement through openings or while phase-shifted.</summary>
[RegisterComponent]
public sealed partial class CEZLevelContextualMoverComponent : Component
{
    [DataField]
    public EntProtoId UpActionProto = "CEActionZLevelUp";

    [DataField]
    public EntProtoId DownActionProto = "CEActionZLevelDown";

    public EntityUid? ZLevelUpActionEntity;
    public EntityUid? ZLevelDownActionEntity;

    /// <summary>Earliest allowed z-level move time.</summary>
    public TimeSpan NextMove;
}
