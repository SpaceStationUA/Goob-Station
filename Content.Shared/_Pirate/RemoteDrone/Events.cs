namespace Content.Shared._Pirate.RemoteDrone;

/// <summary>
///     Raised when a remote drone is <i>linked to</i> its controller,
///         on both the drone and controller, after all related internal logic is completed.
/// </summary>
[ByRefEvent]
public record struct RemoteDroneLinkedEvent(Entity<RemoteDroneControllerComponent> ControllerEntity, EntityUid DroneUid);

/// <summary>
///     Raised when a remote drone is <i>unlinked from</i> its controller,
///         on both the drone and controller, after all related internal logic is completed.
/// </summary>
[ByRefEvent]
public record struct RemoteDroneUnlinkedEvent(Entity<RemoteDroneControllerComponent> ControllerEntity, EntityUid DroneUid);

/// <summary>
///     Raised to determine if a remote drone should be allowed to be controlled,
///         on both the drone and controller.
/// </summary>
[ByRefEvent]
public record struct RemoteDroneAttemptControlEvent(Entity<RemoteDroneControllerComponent> ControllerEntity, EntityUid DroneUid, EntityUid UserUid, bool Cancelled = false);

/// <summary>
///     Raised when a remote drone <i>starts</i> being controlled by something,
///         on both the drone and controller. When this is raised, the drone controller's
///         <see cref="RemoteDroneControllerComponent.UserUid"/> should be assumed to be notnull.
///
///     All methods that subscribe to this must be pure.
/// </summary>
[ByRefEvent]
public record struct RemoteDroneControlStartedEvent(Entity<RemoteDroneControllerComponent> ControllerEntity, EntityUid DroneUid);

/// <summary>
///     Raised when a remote drone <i>stops</i> being controlled by something,
///         on both the drone and controller.
///
///     The drone may not exist when this is raised.
/// </summary>
[ByRefEvent]
public record struct RemoteDroneControlEndedEvent(Entity<RemoteDroneControllerComponent> ControllerEntity, EntityUid? DroneUid);
