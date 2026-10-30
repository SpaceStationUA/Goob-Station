// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Players.PlayTimeTracking;
using Robust.Shared.Player;

namespace Content.Server._Pirate.Newbies;

/// <summary>Reads loaded playtime, including the current round, without requesting the database.</summary>
public sealed class NovicePlaytimeSystem : EntitySystem
{
    [Dependency] private readonly PlayTimeTrackingManager _playtime = default!;

    public bool TryGetPlaytime(ICommonSession session, out TimeSpan playtime)
    {
        playtime = TimeSpan.Zero;
        if (!_playtime.TryGetTrackerTimes(session, out _))
            return false;

        _playtime.FlushTracker(session);
        playtime = _playtime.GetOverallPlaytime(session);
        return true;
    }

    public bool IsRestricted(ICommonSession session, TimeSpan requiredPlaytime)
    {
        return !TryGetPlaytime(session, out var playtime) || playtime < requiredPlaytime;
    }
}
