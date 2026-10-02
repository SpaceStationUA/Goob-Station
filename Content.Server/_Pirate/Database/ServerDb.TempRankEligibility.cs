// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Robust.Shared.Network;

namespace Content.Server.Database;

public partial interface IServerDbManager
{
    Task<List<PirateTempRankEligibility>> GetTempRankEligibilityAsync(NetUserId? userId, CancellationToken cancel = default);

    Task<bool> IsTempRankEligibleAsync(NetUserId userId, int rankId, CancellationToken cancel = default);

    Task<bool> AddTempRankEligibilityAsync(NetUserId userId, int rankId, NetUserId? addedBy, CancellationToken cancel = default);

    Task<bool> RemoveTempRankEligibilityAsync(NetUserId userId, int rankId, CancellationToken cancel = default);
}

public sealed partial class ServerDbManager
{
    public Task<List<PirateTempRankEligibility>> GetTempRankEligibilityAsync(NetUserId? userId, CancellationToken cancel = default)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetTempRankEligibilityAsync(userId, cancel));
    }

    public Task<bool> IsTempRankEligibleAsync(NetUserId userId, int rankId, CancellationToken cancel = default)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.IsTempRankEligibleAsync(userId, rankId, cancel));
    }

    public Task<bool> AddTempRankEligibilityAsync(NetUserId userId, int rankId, NetUserId? addedBy, CancellationToken cancel = default)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.AddTempRankEligibilityAsync(userId, rankId, addedBy, cancel));
    }

    public Task<bool> RemoveTempRankEligibilityAsync(NetUserId userId, int rankId, CancellationToken cancel = default)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.RemoveTempRankEligibilityAsync(userId, rankId, cancel));
    }
}

public abstract partial class ServerDbBase
{
    public async Task<List<PirateTempRankEligibility>> GetTempRankEligibilityAsync(NetUserId? userId, CancellationToken cancel)
    {
        await using var db = await GetDb(cancel);

        var query = db.DbContext.PirateTempRankEligibility
            .AsNoTracking()
            .Include(e => e.AdminRank)
            .AsQueryable();

        if (userId is { } id)
            query = query.Where(e => e.UserId == id.UserId);

        return await query.ToListAsync(cancel);
    }

    public async Task<bool> IsTempRankEligibleAsync(NetUserId userId, int rankId, CancellationToken cancel)
    {
        await using var db = await GetDb(cancel);

        return await db.DbContext.PirateTempRankEligibility
            .AnyAsync(e => e.UserId == userId.UserId && e.AdminRankId == rankId, cancel);
    }

    public async Task<bool> AddTempRankEligibilityAsync(NetUserId userId, int rankId, NetUserId? addedBy, CancellationToken cancel)
    {
        await using var db = await GetDb(cancel);

        var exists = await db.DbContext.PirateTempRankEligibility
            .AnyAsync(e => e.UserId == userId.UserId && e.AdminRankId == rankId, cancel);
        if (exists)
            return false;

        db.DbContext.PirateTempRankEligibility.Add(new PirateTempRankEligibility
        {
            UserId = userId.UserId,
            AdminRankId = rankId,
            AddedById = addedBy?.UserId,
            CreatedAt = DateTime.UtcNow,
        });

        await db.DbContext.SaveChangesAsync(cancel);
        return true;
    }

    public async Task<bool> RemoveTempRankEligibilityAsync(NetUserId userId, int rankId, CancellationToken cancel)
    {
        await using var db = await GetDb(cancel);

        var entry = await db.DbContext.PirateTempRankEligibility
            .SingleOrDefaultAsync(e => e.UserId == userId.UserId && e.AdminRankId == rankId, cancel);
        if (entry == null)
            return false;

        db.DbContext.PirateTempRankEligibility.Remove(entry);
        await db.DbContext.SaveChangesAsync(cancel);
        return true;
    }
}
