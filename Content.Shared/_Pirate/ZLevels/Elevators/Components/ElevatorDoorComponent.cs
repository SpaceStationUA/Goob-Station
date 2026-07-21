namespace Content.Shared._Pirate.ZLevels.Elevators.Components;

/// <summary>
/// Add to any vanilla <c>Door</c> to make it an elevator door for the matching <see cref="ElevatorId"/>.
/// Linked doors close while the cab is in transit; only the door on the cab's floor opens on arrival.
/// </summary>
[RegisterComponent]
public sealed partial class ElevatorDoorComponent : Component
{
    /// <summary>Id of the elevator this door belongs to.</summary>
    [DataField(required: true)]
    public string ElevatorId = string.Empty;
}
