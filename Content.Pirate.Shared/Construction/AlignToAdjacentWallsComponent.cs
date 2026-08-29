using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Construction;

/// <summary>
/// Orients an entity to match the wall run it was built into. Secret doors use this because they can't be
/// rotated after placement, so a manually built one would otherwise always face the construction default.
/// Only applies when the entity is built - mapped instances keep whatever rotation the mapper set.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AlignToAdjacentWallsComponent : Component
{
    /// <summary>
    /// Rotation (degrees) to use when the neighbouring walls run east-west.
    /// </summary>
    [DataField]
    public float AlongEastWest;

    /// <summary>
    /// Rotation (degrees) to use when the neighbouring walls run north-south.
    /// </summary>
    [DataField]
    public float AlongNorthSouth = 90f;
}
