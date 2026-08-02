/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Pirate.ZLevels.Core.Components;

/// <summary>
/// Allows an entity to move up and down the z-levels by gravity or jumping
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true, fieldDeltas: true),
 Access(typeof(CESharedZLevelsSystem))]
public sealed partial class CEZPhysicsComponent : Component
{
    /// <summary>
    /// The current speed of movement between z-levels.
    /// If greater than 0, the entity moves upward. If less than 0, the entity moves downward.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Velocity;

    /// <summary>
    /// The current height of the entity within the current Z-level.
    /// Takes values from 0 to 1. If the value rises above 1, the entity moves up to the next level and the value is normalized.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float LocalPosition;

    /// Optimization Caches

    /// <summary>
    /// Cached value of the current z-level map height
    /// </summary>
    [AutoNetworkedField]
    public int CurrentZLevel;

    /// <summary>
    /// Cached value of the current distance to the ground in the current z-level. Updates only on MoveEvent and when tiles below change.
    /// </summary>
    public float CurrentGroundHeight;

    /// <summary>
    /// Cached value of whether the entity is currently on sticky ground (ladders).
    /// </summary>
    public bool CurrentStickyGround;

    /// <summary>
    /// Cached flag: true when the Z-level directly below has a real floor or high-ground entity
    /// at this entity's XY tile position. Updated on MoveEvent / TileChangedEvent.
    /// </summary>
    public bool CurrentHasSupportBelow;

    /// <summary>
    /// Cached grid uid that provides the support directly below, if any.
    /// </summary>
    public EntityUid CurrentSupportGridUid = EntityUid.Invalid;

    /// <summary>
    /// Cached flag: true when the support found directly below is a CEZLevelHighGround entity
    /// (stairs or ladder), rather than a plain tile. This only permits automatic descent
    /// when the supporting grid/map has gravity.
    /// </summary>
    public bool CurrentHighGroundBelow;

    /// <summary>
    /// Cached flag: true when the nearest ground found by ComputeGroundHeightInternal came
    /// from the Z-level BELOW the current one (floor scan offset > 0).
    /// When true, AutoStep and Bounce are suppressed so the entity falls through naturally
    /// instead of being held up by a stair peak that pokes above the current-level floor plane.
    /// </summary>
    public bool CurrentGroundFromBelowLevel;

    // Physics

    [DataField, AutoNetworkedField]
    public float Bounciness = 0.3f;

    [DataField, AutoNetworkedField]
    public float GravityMultiplier = 1f;

    /// <summary>
    /// Short grace window after an automatic move down to prevent immediately re-triggering a move up on the same stair.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan AutoUpBlockedUntil;

    /// <summary>
    /// Short grace window after an automatic move up to prevent immediately re-triggering a move down on the same stair.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan AutoDownBlockedUntil;

    /// <summary>
    /// Short startup freeze window for map-loaded entities so z-physics does not begin falling
    /// before adjacent z-level maps, linked grids, and movement caches have all stabilized.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan StartupSuppressedUntil;

    /// <summary>
    /// Runtime cache of the last upper-grid local XY while detaching to the map for descent.
    /// This keeps moving-stair landings anchored to the deck frame that the mover actually left.
    /// </summary>
    public EntityUid DetachedCarrierGridUid = EntityUid.Invalid;

    public Vector2 DetachedCarrierLocalPosition = Vector2.Zero;

    public TimeSpan DetachedCarrierReferenceExpiresAt;

    // Visuals

    /// <summary>
    /// Used only by the client.
    /// Blocks the rotation of an object if it has <see cref="LocalPosition"/> > 0,
    /// and saves the original NoRot value in SpriteComponent here so that it can be restored in the future.
    /// </summary>
    [DataField]
    public bool NoRotDefault;

    /// <summary>
    /// The original DrawDepth of the object is automatically saved here. Increases by 1 when the creature has <see cref="LocalPosition"/> > 0
    /// </summary>
    [DataField]
    public int DrawDepthDefault;

    /// <summary>
    /// When the mapinit entity is created, its initial Sprite Offset value is written here in order to apply an offset based on the Z position relative to this value.
    /// </summary>
    [DataField]
    public Vector2 SpriteOffsetDefault = Vector2.Zero;

    /// <summary>
    /// automatically rises if the current localPosition is lower than the height. Enabled by default, but for ghosts, for example, there is no point in climbing stairs
    /// </summary>
    [DataField]
    public bool AutoStep = true;
}
