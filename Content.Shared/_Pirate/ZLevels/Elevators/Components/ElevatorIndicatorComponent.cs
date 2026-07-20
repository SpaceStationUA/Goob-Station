namespace Content.Shared._Pirate.ZLevels.Elevators.Components;

/// <summary>
/// A floor indicator / hall lantern. Displays the cab's current floor and travel direction,
/// driven through appearance data by the elevator system. Links by <see cref="ElevatorId"/>.
/// </summary>
[RegisterComponent]
public sealed partial class ElevatorIndicatorComponent : Component
{
    /// <summary>Id of the elevator this indicator tracks.</summary>
    [DataField(required: true)]
    public string ElevatorId = string.Empty;
}
