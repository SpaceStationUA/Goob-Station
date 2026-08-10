/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

namespace Content.Shared._Pirate.ZLevels.Core.EntitySystems;

public abstract partial class CESharedZLevelsSystem
{
    public const int MaxZLevelsBelowRendering = 6;

    /// <summary>
    /// Cap on z-physics substeps per engine Update, preventing a death spiral where a slow frame
    /// accumulates enough frametime that the next frame substeps even longer.
    /// </summary>
    public const int MaxStepsPerFrame = 10;

    /// <summary>Downward acceleration applied per second to falling z-physics bodies.</summary>
    internal const float ZGravityForce = 9.8f;

    /// <summary>Hard cap on absolute vertical velocity.</summary>
    internal const float ZVelocityLimit = 20.0f;

    /// <summary>Minimum |velocity| required to fire <c>CEZLevelHitEvent</c> / <c>LandEvent</c> on landing.</summary>
    internal const float ImpactVelocityLimit = 3f;

    /// <summary>
    /// Vertical world-units the renderer offsets adjacent Z layers by. Used by cross-Z shooting
    /// to compensate the projectile sprite back to the source-layer barrel position.
    /// </summary>
    public const float ZLevelVisualOffset = 0.7f;
}
