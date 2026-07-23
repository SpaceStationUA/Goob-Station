namespace Content.Shared.Damage.Events;

/// <summary>
/// Raised when an entity enters stamina critical state.
/// </summary>
[ByRefEvent]
public record struct EnterStaminaCritEvent();
