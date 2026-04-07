using System;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Ghost;

[Serializable, NetSerializable]
public sealed class GhostRespawnLobbyRequest : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class GhostRespawnStatusEvent : EntityEventArgs
{
    public bool HasStatus;
    public bool CanRespawn;
    public TimeSpan RemainingTime;

    public GhostRespawnStatusEvent()
    {
    }

    public GhostRespawnStatusEvent(bool hasStatus, bool canRespawn, TimeSpan remainingTime)
    {
        HasStatus = hasStatus;
        CanRespawn = canRespawn;
        RemainingTime = remainingTime;
    }
}
