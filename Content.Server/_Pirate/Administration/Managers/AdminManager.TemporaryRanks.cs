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
