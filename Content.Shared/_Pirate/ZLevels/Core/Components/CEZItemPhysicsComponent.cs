/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.ZLevels.Core.Components;

/// <summary>
/// Tracks lightweight vertical z-physics for loose items.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class CEZItemPhysicsComponent : Component
{
    /// <summary>
    /// Height within the current z-level, where 0 is the floor and 1 is just below the level above.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float LocalPosition;

    /// <summary>
    /// Vertical speed in z-levels per second. Negative values fall downward.
    /// </summary>
    [DataField]
    public float ZVelocity;

    /// <summary>
    /// True after this item has crossed at least one z-level during the current fall.
    /// </summary>
    [DataField]
    public bool HadZFall;

    /// <summary>
    /// Client-side guard for restoring sprite state after the temporary fall visuals end.
    /// </summary>
    [DataField]
    public bool VisualsInitialized;

    /// <summary>
    /// Client-side: true while the sprite carries a Z offset/draw-depth override, so flat items
    /// can be left untouched yet still restored once when they settle.
    /// </summary>
    [DataField]
    public bool VisualsApplied;

    [DataField]
    public bool NoRotDefault;

    [DataField]
    public int DrawDepthDefault;

    [DataField]
    public Vector2 SpriteOffsetDefault = Vector2.Zero;
}
