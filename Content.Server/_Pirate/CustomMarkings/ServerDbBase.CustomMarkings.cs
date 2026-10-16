// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server._Pirate.CustomMarkings;
using Content.Shared._Pirate.CustomMarkings;
using Microsoft.EntityFrameworkCore;
using Robust.Shared.Utility;

namespace Content.Server.Database;

// Custom marking art and each player's library of it.
public abstract partial class ServerDbBase
{
    /// <summary>How many art rows one statement of the cleanup names.</summary>
    private const int CustomMarkingPurgeBatch = 200;

    // Profile references must stay unchanged from the cleanup's snapshot through its final deletion.
    private readonly SemaphoreSlim _customMarkingProfiles = new(1, 1);

    // Bounded gates serialize each player's quota checks through commit. Hash collisions only add waiting.
    private readonly SemaphoreSlim[] _customMarkingSaveLocks = Enumerable.Range(0, 64)
        .Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    private async ValueTask<LockUtility.SemaphoreGuard> LockCustomMarkingProfilesAsync(CancellationToken cancel = default)
    {
        await _customMarkingProfiles.WaitAsync(cancel);
        return new LockUtility.SemaphoreGuard(_customMarkingProfiles);
    }

    public async Task<List<PirateCustomMarking>> GetCustomMarkingsAsync(Guid userId, CancellationToken cancel = default)
    {
        await using var db = await GetDb(cancel);

        return await db.DbContext.PirateCustomMarking
            .Where(m => m.PlayerUserId == userId)
            .OrderBy(m => m.Id)
            .ToListAsync(cancel);
    }

    public async Task<CustomMarkingSaveResult> SaveCustomMarkingAsync(
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
        var gate = _customMarkingSaveLocks[(int) ((uint) userId.GetHashCode() % _customMarkingSaveLocks.Length)];
        await gate.WaitAsync(cancel);
        using var saveLock = new LockUtility.SemaphoreGuard(gate);
        await using var db = await GetDb(cancel);
        var ctx = db.DbContext;

        PirateCustomMarking? entry = null;
        if (id != 0)
        {
            entry = await ctx.PirateCustomMarking.SingleOrDefaultAsync(m => m.Id == id && m.PlayerUserId == userId, cancel);
            if (entry == null)
                return CustomMarkingSaveResult.Fail("wf-custom-marking-error-missing");
        }
        else if (hash == null || art == null)
        {
            return CustomMarkingSaveResult.Fail("wf-custom-marking-error-invalid");
        }
        else if (await ctx.PirateCustomMarking.CountAsync(m => m.PlayerUserId == userId, cancel) >= limit)
        {
            return CustomMarkingSaveResult.Fail("wf-custom-marking-error-full");
        }

        // Pirate: metadata-only edits must also validate the art the entry keeps.
        var effectiveHash = hash ?? entry!.ArtHash;
        var blocked = await ctx.PirateCustomMarkingArt
            .Where(a => a.Hash == effectiveHash)
            .Select(a => (bool?) a.Blocked)
            .SingleOrDefaultAsync(cancel);

        if (blocked == true)
            return CustomMarkingSaveResult.Fail("wf-custom-marking-error-blocked");

        await using var transaction = await ctx.Database.BeginTransactionAsync(cancel);
        if (blocked == null)
        {
            if (art == null)
                return CustomMarkingSaveResult.Fail("wf-custom-marking-error-missing");

            // Art the server doesn't hold yet is a new row, and a player only gets so many of those a day.
            if (dailyArtLimit > 0)
            {
                var since = DateTime.UtcNow - TimeSpan.FromDays(1);
                var today = await ctx.PirateCustomMarkingArt
                    .CountAsync(a => a.UploaderUserId == userId && a.UploadedAt > since, cancel);

                if (today >= dailyArtLimit)
                    return CustomMarkingSaveResult.Fail("wf-custom-marking-error-daily");
            }

            // Pirate: concurrent saves share the winning row, including its uploader and moderation data.
            await ctx.Database.ExecuteSqlAsync($"""
                INSERT INTO pirate_custom_marking_art
                    (hash, png, frame_times, erase, uploader_user_id, uploaded_at, blocked)
                VALUES ({effectiveHash}, {art.Png}, {art.FrameTimes}, {art.Erase}, {userId}, {DateTime.UtcNow}, {false})
                ON CONFLICT (hash) DO NOTHING
                """, cancel);
        }

        // Pirate: lock the unblocked row through commit so moderation cannot race this save on either provider.
        var unblocked = await ctx.PirateCustomMarkingArt
            .Where(a => a.Hash == effectiveHash && !a.Blocked)
            .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.Blocked, false), cancel);
        if (unblocked == 0)
            return CustomMarkingSaveResult.Fail("wf-custom-marking-error-blocked");

        string? previous = entry != null && hash != null && entry.ArtHash != hash ? entry.ArtHash : null;
        if (entry == null)
        {
            entry = new PirateCustomMarking { PlayerUserId = userId };
            ctx.PirateCustomMarking.Add(entry);
        }

        if (hash != null)
            entry.ArtHash = hash;

        entry.Name = name;
        entry.Placement = placement;
        entry.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync(cancel);
        await transaction.CommitAsync(cancel);

        return new CustomMarkingSaveResult(entry, previous, null);
    }

    public async Task<bool> DeleteCustomMarkingAsync(Guid userId, int id, CancellationToken cancel = default)
    {
        await using var db = await GetDb(cancel);

        var entry = await db.DbContext.PirateCustomMarking
            .SingleOrDefaultAsync(m => m.Id == id && m.PlayerUserId == userId, cancel);

        if (entry == null)
            return false;

        db.DbContext.PirateCustomMarking.Remove(entry);
        await db.DbContext.SaveChangesAsync(cancel);
        return true;
    }

    public async Task<CustomMarkingStoredArt?> GetCustomMarkingArtAsync(string hash, CancellationToken cancel = default)
    {
        await using var db = await GetDb(cancel);

        var art = await db.DbContext.PirateCustomMarkingArt
            .Where(a => a.Hash == hash && !a.Blocked)
            .Select(a => new { a.Png, a.FrameTimes, a.Erase })
            .SingleOrDefaultAsync(cancel);

        return art == null ? null : new CustomMarkingStoredArt(art.Png, art.FrameTimes, art.Erase);
    }

    public async Task<(int Found, int Deleted)> PurgeUnusedCustomMarkingArtAsync(TimeSpan keep, CancellationToken cancel = default)
    {
        using var profiles = await LockCustomMarkingProfilesAsync(cancel);
        await using var db = await GetDb(cancel);
        var ctx = db.DbContext;
        var now = DateTime.UtcNow;

        // Art a library holds is in use, whatever an earlier look found.
        await ctx.PirateCustomMarkingArt
            .Where(a => a.UnusedSince != null && ctx.PirateCustomMarking.Any(m => m.ArtHash == a.Hash))
            .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.UnusedSince, (DateTime?) null), cancel);

        var loose = await ctx.PirateCustomMarkingArt
            .Where(a => !a.Blocked && !ctx.PirateCustomMarking.Any(m => m.ArtHash == a.Hash))
            .Select(a => new { a.Hash, a.UnusedSince })
            .ToListAsync(cancel);

        if (loose.Count == 0)
            return (0, 0);

        // Art no library holds may still be worn: a saved character lists its art by hash.
        var worn = new HashSet<string>();
        var lists = await ctx.Profile
            .Where(p => p.CustomMarkings != "")
            .Select(p => p.CustomMarkings)
            .ToListAsync(cancel);

        foreach (var list in lists)
        {
            foreach (var marking in CustomMarkingRules.FromStored(list))
            {
                worn.Add(marking.Hash);
            }
        }

        var used = new List<string>();
        var found = new List<string>();
        var old = new List<string>();
        foreach (var art in loose)
        {
            if (worn.Contains(art.Hash))
            {
                if (art.UnusedSince != null)
                    used.Add(art.Hash);
            }
            else if (art.UnusedSince is not { } since)
            {
                found.Add(art.Hash);
            }
            else if (since <= now - keep)
            {
                old.Add(art.Hash);
            }
        }

        foreach (var batch in used.Chunk(CustomMarkingPurgeBatch))
        {
            await ctx.PirateCustomMarkingArt
                .Where(a => batch.Contains(a.Hash))
                .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.UnusedSince, (DateTime?) null), cancel);
        }

        foreach (var batch in found.Chunk(CustomMarkingPurgeBatch))
        {
            await ctx.PirateCustomMarkingArt
                .Where(a => batch.Contains(a.Hash))
                .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.UnusedSince, (DateTime?) now), cancel);
        }

        // Asked again as it deletes: a library may have taken a row up since the list was made.
        var deleted = 0;
        foreach (var batch in old.Chunk(CustomMarkingPurgeBatch))
        {
            deleted += await ctx.PirateCustomMarkingArt
                .Where(a => batch.Contains(a.Hash) && !a.Blocked && !ctx.PirateCustomMarking.Any(m => m.ArtHash == a.Hash))
                .ExecuteDeleteAsync(cancel);
        }

        return (found.Count, deleted);
    }

    public async Task<Guid?> SetCustomMarkingArtBlockedAsync(string hash, bool blocked, CancellationToken cancel = default)
    {
        await using var db = await GetDb(cancel);

        var art = await db.DbContext.PirateCustomMarkingArt.SingleOrDefaultAsync(a => a.Hash == hash, cancel);
        if (art == null)
            return null;

        art.Blocked = blocked;
        await db.DbContext.SaveChangesAsync(cancel);
        return art.UploaderUserId;
    }
}
