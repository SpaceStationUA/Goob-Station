using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Fluids;

/// <summary>
/// Standing water that soaks wading mobs and drains its tile.
/// </summary>
[RegisterComponent]
public sealed partial class FloorWaterComponent : Component
{
    /// <summary>
    /// Water applied to a mob on each wade step.
    /// </summary>
    [DataField]
    public FixedPoint2 ImmersionFlow = FixedPoint2.New(200);
}
