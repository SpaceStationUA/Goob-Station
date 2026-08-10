using Robust.Shared.Serialization;
using Robust.Shared.Map;
using Content.Shared.Shuttles.BUIStates;

namespace Content.Shared._Mono.FireControl;

#region Pirate: multiz
/// <summary>
/// Describes a z-layer selectable on a gunnery console. Each entry maps a depth (z-index in
/// the linked-grid network) to the network's grid at that depth.
/// </summary>
[Serializable, NetSerializable]
public struct FireControlLayerInfo
{
    public int Depth;
    public NetEntity Grid;

    public FireControlLayerInfo(int depth, NetEntity grid)
    {
        Depth = depth;
        Grid = grid;
    }
}
#endregion

[Serializable, NetSerializable]
public sealed class FireControlConsoleUpdateEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class FireControlConsoleBoundInterfaceState : BoundUserInterfaceState
{
    public bool Connected;
    public FireControllableEntry[] FireControllables;
    public NavInterfaceState NavState;

    #region Pirate: multiz
    /// <summary>
    /// Z-layers selectable on this console: one entry per existing depth inside the network
    /// covering <c>[min_gun_depth - 1, max_gun_depth + 1]</c>. Empty for shuttles without a
    /// z-network. Sorted by depth ascending.
    /// </summary>
    public FireControlLayerInfo[] Layers;

    /// <summary>
    /// The depth currently focused by this console. The radar centres on this layer's grid
    /// and only guns whose own depth is within their reach of this value are firing-eligible.
    /// </summary>
    public int CurrentLayer;
    #endregion

    public FireControlConsoleBoundInterfaceState(
        bool connected,
        FireControllableEntry[] fireControllables,
        NavInterfaceState navState,
        FireControlLayerInfo[] layers, // Pirate: multiz
        int currentLayer) // Pirate: multiz
    {
        Connected = connected;
        FireControllables = fireControllables;
        NavState = navState;
        Layers = layers; // Pirate: multiz
        CurrentLayer = currentLayer; // Pirate: multiz
    }
}

#region Pirate: multiz
/// <summary>
/// Sent by the gunnery console UI when the operator switches to a different z-layer.
/// </summary>
[Serializable, NetSerializable]
public sealed class FireControlConsoleSelectLayerMessage : BoundUserInterfaceMessage
{
    public int Depth;

    public FireControlConsoleSelectLayerMessage(int depth)
    {
        Depth = depth;
    }
}
#endregion

[Serializable, NetSerializable]
public enum FireControlConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class FireControlConsoleRefreshServerMessage : BoundUserInterfaceMessage
{

}

[Serializable, NetSerializable]
public sealed class FireControlConsoleFireMessage : BoundUserInterfaceMessage
{
    public List<NetEntity> Selected;
    public NetCoordinates Coordinates;
    public FireControlConsoleFireMessage(List<NetEntity> selected, NetCoordinates coordinates)
    {
        Selected = selected;
        Coordinates = coordinates;
    }
}

/// <summary>
/// Event raised when a fire control console wants to fire weapons at specific coordinates.
/// Used for tracking cursor position.
/// </summary>
public sealed class FireControlConsoleFireEvent : EntityEventArgs
{
    /// <summary>
    /// The coordinates of the cursor/firing position
    /// </summary>
    public NetCoordinates Coordinates;

    /// <summary>
    /// The weapons selected to fire
    /// </summary>
    public List<NetEntity> Selected;

    public FireControlConsoleFireEvent(NetCoordinates coordinates, List<NetEntity> selected)
    {
        Coordinates = coordinates;
        Selected = selected;
    }
}

[Serializable, NetSerializable]
public struct FireControllableEntry
{
    /// <summary>
    /// The entity in question
    /// </summary>
    public NetEntity NetEntity;

    /// <summary>
    /// Location of the entity
    /// </summary>
    public NetCoordinates Coordinates;

    /// <summary>
    /// Display name of the entity
    /// </summary>
    public string Name;

    #region Pirate: multiz
    /// <summary>
    /// Z-network depth of the grid this gun sits on (0 if not in a network).
    /// </summary>
    public int Depth;

    /// <summary>
    /// Maximum number of z-levels the gun can fire above or below its own depth.
    /// </summary>
    public int Reach;
    #endregion

    public FireControllableEntry(NetEntity entity, NetCoordinates coordinates, string name)
    {
        NetEntity = entity;
        Coordinates = coordinates;
        Name = name;
        Depth = 0; // Pirate: multiz
        Reach = 1; // Pirate: multiz
    }
}
