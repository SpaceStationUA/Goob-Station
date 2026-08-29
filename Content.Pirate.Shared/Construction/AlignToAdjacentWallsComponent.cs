using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Construction;

[RegisterComponent, NetworkedComponent]
public sealed partial class AlignToAdjacentWallsComponent : Component
{
    [DataField]
    public float AlongEastWest;

    [DataField]
    public float AlongNorthSouth = 90f;
}
