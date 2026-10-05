// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading.Tasks;
using Content.Server._Pirate.Administration.Systems;
using Content.Shared.Administration;
using Robust.Shared.Player;

namespace Content.Server.Administration.Managers;

public sealed partial class AdminManager
{
    [Dependency] private readonly IEntitySystemManager _pirateSystems = default!;
    // A later grant, revocation, or rank edit must win if an earlier database read finishes afterward.
    private readonly Dictionary<ICommonSession, long> _adminLoadVersions = new();
    private long _adminLoadVersion;

    private long BeginAdminLoad(ICommonSession session)
    {
        var version = ++_adminLoadVersion;
        _adminLoadVersions[session] = version;
        return version;
    }

    private bool IsCurrentAdminLoad(ICommonSession session, long version)
    {
        return session.Status == Robust.Shared.Enums.SessionStatus.InGame &&
               _adminLoadVersions.TryGetValue(session, out var current) && current == version;
    }

    private void EndAdminLoad(ICommonSession session)
    {
        _adminLoadVersions.Remove(session);
    }

    private bool HasTemporaryRank(ICommonSession session, int rankId)
    {
        return _pirateSystems.TryGetEntitySystem<TemporaryRankSystem>(out var system) &&
               system.TryGetGrant(session.UserId, out var grant) && grant.RankId == rankId;
    }

    // Preserve the existing title and add the temporary rank's flags.
    private async Task<(AdminData dat, int? rankId, bool specialLogin)?> ApplyTemporaryRank(
        ICommonSession session,
        (AdminData dat, int? rankId, bool specialLogin)? result)
    {
        if (!_pirateSystems.TryGetEntitySystem<TemporaryRankSystem>(out var system) ||
            !system.TryGetGrant(session.UserId, out var grant))
        {
            return result;
        }

        var rank = await _dbManager.GetAdminRankAsync(grant.RankId);
        if (rank == null)
        {
            _sawmill.Warning($"Temporary rank {grant.RankName} of {session.Name} no longer exists, ignoring it");
            return result;
        }

        var flags = TemporaryRankSystem.RankFlags(rank);

        if (result == null)
        {
            var data = new AdminData
            {
                Title = rank.Name,
                Flags = flags,
                // Temporary-only admins have no DB row, so keep their deadmin state on the grant.
                Active = !grant.Deadminned,
            };
            return (data, rank.Id, false);
        }

        result.Value.dat.Flags |= flags;
        return result;
    }
}
