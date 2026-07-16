using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Station.Events;

/// <summary>
/// Raised on a station after a player's jobs are removed from its manifest.
/// </summary>
[ByRefEvent]
public record struct PlayerJobsRemovedEvent(NetUserId NetUserId, List<ProtoId<JobPrototype>> PlayerJobs);

/// <summary>
/// Raised on a station when a job is assigned to a player.
/// </summary>
[ByRefEvent]
public record struct PlayerJobAddedEvent(NetUserId NetUserId, string JobPrototypeId);
