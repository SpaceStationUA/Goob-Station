// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading;
using System.Threading.Tasks;
using Content.Server._Pirate.CustomMarkings;

namespace Content.Server.Database;

public partial interface IServerDbManager
{
    /// <summary>A player's library of custom markings, oldest first.</summary>
    Task<List<PirateCustomMarking>> GetCustomMarkingsAsync(Guid userId, CancellationToken cancel = default);

    /// <summary>
    /// Adds a marking to a player's library, or changes their entry with this id. New art is stored under its
    /// hash; null art keeps the entry's own.
    /// </summary>
    /// <param name="limit">Most entries the library may hold; adding past it fails.</param>
    /// <param name="dailyArtLimit">
    /// Most art rows the player may add in a day; storing art past it fails. Art the server already holds adds no
    /// row. 0 for no limit.
    /// </param>
    Task<CustomMarkingSaveResult> SaveCustomMarkingAsync(
        Guid userId,
        int id,
        string name,
        int placement,
        string? hash,
        CustomMarkingStoredArt? art,
        int limit,
        int dailyArtLimit = 0,
        CancellationToken cancel = default);

    /// <summary>Removes an entry from a player's library. Its art stays, as saved characters may wear it.</summary>
    Task<bool> DeleteCustomMarkingAsync(Guid userId, int id, CancellationToken cancel = default);

    /// <summary>The art stored under a hash, or null when there is none or it is blocked.</summary>
    Task<CustomMarkingStoredArt?> GetCustomMarkingArtAsync(string hash, CancellationToken cancel = default);

    /// <summary>Blocks or unblocks art. Returns who first saved it, or null when no art has this hash.</summary>
    Task<Guid?> SetCustomMarkingArtBlockedAsync(string hash, bool blocked, CancellationToken cancel = default);

    /// <summary>
    /// Looks for art nothing uses: no library holds it and no saved character wears it. Art found unused for the
    /// first time is only noted, with the time; art noted at least <paramref name="keep"/> ago and still unused
    /// is deleted; art that is in use again has its note taken off. Blocked art is left alone, so a block holds.
    /// Bodies in a round aren't looked at, so this is for when there are none.
    /// </summary>
    /// <returns>How many rows were newly found unused, and how many were deleted.</returns>
    Task<(int Found, int Deleted)> PurgeUnusedCustomMarkingArtAsync(TimeSpan keep, CancellationToken cancel = default);
}

public sealed partial class ServerDbManager
{
    public Task<List<PirateCustomMarking>> GetCustomMarkingsAsync(Guid userId, CancellationToken cancel = default)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetCustomMarkingsAsync(userId, cancel));
    }

    public Task<CustomMarkingSaveResult> SaveCustomMarkingAsync(
        Guid userId,
        int id,
        string name,
        int placement,
        string? hash,
        CustomMarkingStoredArt? art,
        int limit,
        int dailyArtLimit = 0,
        CancellationToken cancel = default)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.SaveCustomMarkingAsync(userId, id, name, placement, hash, art, limit, dailyArtLimit, cancel));
    }

    public Task<bool> DeleteCustomMarkingAsync(Guid userId, int id, CancellationToken cancel = default)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.DeleteCustomMarkingAsync(userId, id, cancel));
    }

    public Task<CustomMarkingStoredArt?> GetCustomMarkingArtAsync(string hash, CancellationToken cancel = default)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetCustomMarkingArtAsync(hash, cancel));
    }

    public Task<Guid?> SetCustomMarkingArtBlockedAsync(string hash, bool blocked, CancellationToken cancel = default)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.SetCustomMarkingArtBlockedAsync(hash, blocked, cancel));
    }

    public Task<(int Found, int Deleted)> PurgeUnusedCustomMarkingArtAsync(TimeSpan keep, CancellationToken cancel = default)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.PurgeUnusedCustomMarkingArtAsync(keep, cancel));
    }
}
