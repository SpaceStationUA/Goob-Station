using Content.Shared.Actions;

namespace Content.Shared._Pirate.ZLevels.View;

/// <summary>
/// Moves the performer's controlled eye/camera one z-level floor up.
/// </summary>
public sealed partial class CEZViewUpEvent : InstantActionEvent;

/// <summary>
/// Instant action raised to move the performer's controlled eye/camera one z-level floor down.
/// </summary>
public sealed partial class CEZViewDownEvent : InstantActionEvent;
