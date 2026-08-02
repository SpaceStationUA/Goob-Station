/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.ZLevels.Core.Components;

/// <summary>
/// Allows entities not to fall if they are above this entity at a higher level.
/// Think of it as the ability to walk on top of walls, for example.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEZLevelHighGroundComponent : Component
{
    /// <summary>
    /// Height profile points, forming a simple curve (0..1 by X, height by Y).
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<float> HeightCurve = new()
    {
        1.05f,
        1.05f,
    };

    /// <summary>
    /// Forcibly attaches the entity to itself along the z-axis if the character descends smoothly. Needed for prevent falling from staircases.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Stick = false;

    /// <summary>
    /// SHITCODE - we can't map entities rotated by 45 degrees, so we just use this
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Corner = false;

    /// <summary>
    /// If true, only supports entities probing from a higher Z-level. Lets a ladder base hold a climber
    /// above without pulling a mover on the same tile onto its curve.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool SupportOnlyFromAbove = false;

    /// <summary>
    /// If true, this high-ground can reveal the level above when a viewer stands near it (used by probe-eyes).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool PreviewUpLevel = true;

    /// <summary>
    /// Max tile/world distance at which this high-ground reveals the level above.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float PreviewRange = 5f;
}
