using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.ZLevels.Elevators;

/// <summary>Appearance keys set by the elevator system on floor indicators.</summary>
[Serializable, NetSerializable]
public enum ElevatorIndicatorVisuals : byte
{
    /// <summary>Current cab floor number (int, 1-based for display).</summary>
    Floor,

    /// <summary>Travel direction (<see cref="ElevatorDirection"/>).</summary>
    Direction
}

[Serializable, NetSerializable]
public enum ElevatorDirection : byte
{
    Idle,
    Up,
    Down
}

/// <summary>Sprite layer map ids for elevator parts.</summary>
[Serializable, NetSerializable]
public enum ElevatorVisualLayers : byte
{
    /// <summary>The indicator housing — always visible.</summary>
    Base,

    /// <summary>The up/down arrow overlay — only visible while travelling.</summary>
    Arrow,

    /// <summary>The floor-number digit overlay.</summary>
    Digit
}
