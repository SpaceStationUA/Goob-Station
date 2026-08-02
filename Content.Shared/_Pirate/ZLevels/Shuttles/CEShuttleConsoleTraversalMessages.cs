using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.ZLevels.Shuttles;

/// <summary>
/// Sent by the shuttle console when the pilot requests to fly the whole shuttle one z-level up.
/// </summary>
[Serializable, NetSerializable]
public sealed class CEShuttleConsoleFlyUpMessage : BoundUserInterfaceMessage
{
}

/// <summary>
/// Sent by the shuttle console when the pilot requests to fly the whole shuttle one z-level down.
/// </summary>
[Serializable, NetSerializable]
public sealed class CEShuttleConsoleFlyDownMessage : BoundUserInterfaceMessage
{
}
