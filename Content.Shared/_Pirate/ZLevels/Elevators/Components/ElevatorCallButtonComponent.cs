namespace Content.Shared._Pirate.ZLevels.Elevators.Components;

/// <summary>
/// A per-floor call button. Activating it summons the cab to this button's deck.
/// The button's floor depth is resolved at runtime from the deck it sits on.
/// </summary>
[RegisterComponent]
public sealed partial class ElevatorCallButtonComponent : Component
{
    /// <summary>Id of the elevator this button calls.</summary>
    [DataField(required: true)]
    public string ElevatorId = string.Empty;

    /// <summary>Cooldown between presses, seconds.</summary>
    [DataField]
    public float Cooldown = 2.0f;

    /// <summary>When the button can next be pressed.</summary>
    [ViewVariables]
    public TimeSpan NextUse;
}
