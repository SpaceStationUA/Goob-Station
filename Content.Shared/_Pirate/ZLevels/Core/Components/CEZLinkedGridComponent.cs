using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.ZLevels.Core.Components;

/// <summary>
/// Added to grid entities that are physically linked across Z-levels in a Z-network.
/// All linked grids move, rotate, and share velocity as a single rigid structure.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, UnsavedComponent]
public sealed partial class CEZLinkedGridComponent : Component
{
    /// <summary>
    /// The Z-network entity this grid is linked in.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid ZNetwork;

    /// <summary>
    /// Peer grids on other Z-levels. Key is depth, value is grid EntityUid.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<int, EntityUid> PeerGrids = new();

    /// <summary>
    /// This grid's depth in the Z-network.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Depth;
}
