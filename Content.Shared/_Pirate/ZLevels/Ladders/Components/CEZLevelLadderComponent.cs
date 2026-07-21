using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.ZLevels.Ladders.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class CEZLevelLadderComponent : Component
{
    [DataField]
    public TimeSpan ClimbDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public HashSet<string> PassableSupportTiles = new()
    {
        "Lattice",
        "TrainLattice",
    };

    [DataField]
    public List<string> PassableSupportTilePrefixes = new()
    {
        "LatticeDiagonal",
        "LatticeHalf",
        "LatticeWedge",
    };
}

[Serializable, NetSerializable]
public sealed partial class CEZLevelLadderClimbUpDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CEZLevelLadderClimbDownDoAfterEvent : SimpleDoAfterEvent;
