namespace Content.Shared._Pirate.ZLevels.Elevators.Components;

/// <summary>
/// Wall-mounted elevator control panel. Opens a floor-selection UI and dispatches the cab.
/// Resolves its controller at runtime by matching <see cref="ElevatorId"/>.
/// </summary>
[RegisterComponent]
public sealed partial class ElevatorPanelComponent : Component
{
    /// <summary>Id of the elevator this panel controls.</summary>
    [DataField(required: true)]
    public string ElevatorId = string.Empty;
}
