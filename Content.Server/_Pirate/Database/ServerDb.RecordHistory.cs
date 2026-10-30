// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared._Pirate.CriminalRecords;
using Content.Shared._Pirate.StationRecords;
using Microsoft.EntityFrameworkCore;

namespace Content.Server.Database;
public sealed record RecordHistorySnapshot(int ProfileId, RecordLogBook Book, List<RecordLogEntry> Entries);
public sealed record RecordCitationSnapshot(int ProfileId, List<CriminalRecordCitation> Citations);

public partial interface IServerDbManager
{
    Task<List<(RecordLogBook Book, RecordLogEntry Entry)>> GetRecordHistoryAsync(int profileId, int limitPerBook, CancellationToken cancel = default);
    Task SaveRecordHistoryAsync(IReadOnlyCollection<RecordHistorySnapshot> snapshots, int limitPerBook, CancellationToken cancel = default);
    Task<List<CriminalRecordCitation>> GetRecordCitationsAsync(int profileId, int limit, CancellationToken cancel = default);
    Task SaveRecordCitationsAsync(IReadOnlyCollection<RecordCitationSnapshot> snapshots, int limit, CancellationToken cancel = default);
}

public sealed partial class ServerDbManager
{
    public Task<List<(RecordLogBook Book, RecordLogEntry Entry)>> GetRecordHistoryAsync(int profileId, int limitPerBook, CancellationToken cancel = default)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetRecordHistoryAsync(profileId, limitPerBook, cancel));
    }

    public Task SaveRecordHistoryAsync(IReadOnlyCollection<RecordHistorySnapshot> snapshots, int limitPerBook, CancellationToken cancel = default)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.SaveRecordHistoryAsync(snapshots, limitPerBook, cancel));
    }

    public Task<List<CriminalRecordCitation>> GetRecordCitationsAsync(int profileId, int limit, CancellationToken cancel = default)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetRecordCitationsAsync(profileId, limit, cancel));
    }

    public Task SaveRecordCitationsAsync(IReadOnlyCollection<RecordCitationSnapshot> snapshots, int limit, CancellationToken cancel = default)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.SaveRecordCitationsAsync(snapshots, limit, cancel));
    }
}

public abstract partial class ServerDbBase
{
    private static List<RecordBackgroundEntry> FromStoredRecordBackgrounds(List<ProfileRecordBackground> stored)
    {
        return stored.Select(e => new RecordBackgroundEntry
        {
            Book = (RecordLogBook) e.Book,
            Title = e.Title,
            Author = e.Author,
            Body = e.Body,
        }).ToList();
    }

    private static List<ProfileRecordBackground> ToStoredRecordBackgrounds(List<RecordBackgroundEntry> entries)
    {
        return entries.Select(e => new ProfileRecordBackground
        {
            Book = (int) e.Book,
            Title = e.Title,
            Author = e.Author,
            Body = e.Body,
        }).ToList();
    }

    public async Task<List<(RecordLogBook Book, RecordLogEntry Entry)>> GetRecordHistoryAsync(int profileId, int limitPerBook, CancellationToken cancel = default)
    {
        await using var db = await GetDb(cancel);

        var rows = await db.DbContext.PirateRecordHistory
            .AsNoTracking()
            .Where(e => e.ProfileId == profileId)
            .OrderByDescending(e => e.Time)
            .ToListAsync(cancel);

        var result = new List<(RecordLogBook, RecordLogEntry)>();
        foreach (var book in rows.GroupBy(e => e.Book))
        {
            foreach (var row in book.Take(Math.Max(0, limitPerBook)))
            {
                result.Add(((RecordLogBook) row.Book, new RecordLogEntry
                {
                    Kind = (RecordLogKind) row.Kind,
                    Topic = row.Topic,
                    Status = row.Status,
                    Message = row.Message,
                    AuthorName = row.AuthorName,
                    AuthorTitle = row.AuthorTitle,
                    StationName = row.StationName,
                    Time = DateTime.SpecifyKind(row.Time, DateTimeKind.Utc),
                    Archived = true,
                }));
            }
        }

        return result;
    }

    public async Task SaveRecordHistoryAsync(IReadOnlyCollection<RecordHistorySnapshot> snapshots, int limitPerBook, CancellationToken cancel = default)
    {
        if (snapshots.Count == 0)
            return;

        await using var db = await GetDb(cancel);

        var profileIds = snapshots.Select(s => s.ProfileId).Distinct().ToList();
        var existing = (await db.DbContext.Profile
                .Where(p => profileIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync(cancel))
            .ToHashSet();

        var saved = await db.DbContext.PirateRecordHistory
            .Where(e => profileIds.Contains(e.ProfileId))
            .ToListAsync(cancel);

        foreach (var snapshot in snapshots)
        {
            if (!existing.Contains(snapshot.ProfileId))
                continue;

            var book = (int) snapshot.Book;
            db.DbContext.PirateRecordHistory.RemoveRange(
                saved.Where(e => e.ProfileId == snapshot.ProfileId && e.Book == book));

            foreach (var entry in snapshot.Entries.OrderByDescending(e => e.Time).Take(Math.Max(0, limitPerBook)))
            {
                db.DbContext.PirateRecordHistory.Add(new PirateRecordHistoryEntry
                {
                    ProfileId = snapshot.ProfileId,
                    Book = book,
                    Kind = (int) entry.Kind,
                    Topic = Truncate(entry.Topic, 128),
                    Status = Truncate(entry.Status, 32),
                    Message = Truncate(entry.Message, 1024) ?? string.Empty,
                    AuthorName = Truncate(entry.AuthorName, 128),
                    AuthorTitle = Truncate(entry.AuthorTitle, 128),
                    StationName = Truncate(entry.StationName, 128),
                    Time = DateTime.SpecifyKind(entry.Time, DateTimeKind.Utc),
                });
            }
        }

        await db.DbContext.SaveChangesAsync(cancel);

        static string? Truncate(string? text, int max) => text is { Length: > 0 } && text.Length > max ? text[..max] : text;
    }

    // Preserve issue order when assigning IDs.
    public async Task<List<CriminalRecordCitation>> GetRecordCitationsAsync(int profileId, int limit, CancellationToken cancel = default)
    {
        await using var db = await GetDb(cancel);

        var rows = await db.DbContext.PirateRecordCitations
            .AsNoTracking()
            .Where(c => c.ProfileId == profileId)
            .OrderByDescending(c => c.IssuedAt)
            .Take(Math.Max(0, limit))
            .ToListAsync(cancel);

        return rows
            .OrderBy(c => c.IssuedAt)
            .Select(row => new CriminalRecordCitation
            {
                Reason = row.Reason,
                Details = row.Details,
                Fine = row.Fine,
                Status = (CitationStatus) row.Status,
                IssuedBy = row.IssuedBy,
                IssuedAt = DateTime.SpecifyKind(row.IssuedAt, DateTimeKind.Utc),
                ClosedBy = row.ClosedBy,
                ClosedAt = row.ClosedAt is { } closed ? DateTime.SpecifyKind(closed, DateTimeKind.Utc) : null,
            })
            .ToList();
    }

    public async Task SaveRecordCitationsAsync(IReadOnlyCollection<RecordCitationSnapshot> snapshots, int limit, CancellationToken cancel = default)
    {
        if (snapshots.Count == 0)
            return;

        await using var db = await GetDb(cancel);

        var profileIds = snapshots.Select(s => s.ProfileId).Distinct().ToList();
        var existing = (await db.DbContext.Profile
                .Where(p => profileIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync(cancel))
            .ToHashSet();

        var saved = await db.DbContext.PirateRecordCitations
            .Where(c => profileIds.Contains(c.ProfileId))
            .ToListAsync(cancel);

        foreach (var snapshot in snapshots)
        {
            if (!existing.Contains(snapshot.ProfileId))
                continue;

            db.DbContext.PirateRecordCitations.RemoveRange(saved.Where(c => c.ProfileId == snapshot.ProfileId));

            foreach (var citation in snapshot.Citations.OrderByDescending(c => c.IssuedAt).Take(Math.Max(0, limit)))
            {
                db.DbContext.PirateRecordCitations.Add(new PirateRecordCitation
                {
                    ProfileId = snapshot.ProfileId,
                    Reason = Truncate(citation.Reason, 64) ?? string.Empty,
                    Details = Truncate(citation.Details, 512) ?? string.Empty,
                    Fine = citation.Fine,
                    Status = (int) citation.Status,
                    IssuedBy = Truncate(citation.IssuedBy, 128),
                    IssuedAt = DateTime.SpecifyKind(citation.IssuedAt, DateTimeKind.Utc),
                    ClosedBy = Truncate(citation.ClosedBy, 128),
                    ClosedAt = citation.ClosedAt is { } closed ? DateTime.SpecifyKind(closed, DateTimeKind.Utc) : null,
                });
            }
        }

        await db.DbContext.SaveChangesAsync(cancel);

        static string? Truncate(string? text, int max) => text is { Length: > 0 } && text.Length > max ? text[..max] : text;
    }
}
