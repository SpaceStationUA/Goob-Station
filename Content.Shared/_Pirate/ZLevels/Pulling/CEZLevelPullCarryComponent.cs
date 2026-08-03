/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

namespace Content.Shared._Pirate.ZLevels.Pulling;

/// <summary>
/// Transient marker placed on a puller during a single z-level move so the matching
/// <c>CEZLevelMapMoveEvent</c> (raised after the puller has reparented to its final tile) can carry
/// the pulled entity over and rebuild the pull. Added and removed within the same move, never saved
/// or networked.
/// </summary>
[RegisterComponent, Access(typeof(CEZLevelPullingSystem))]
public sealed partial class CEZLevelPullCarryComponent : Component
{
    public EntityUid Pulled;
    public int Offset;
    public int TargetZLevel;
    public float PullDistance;
}
