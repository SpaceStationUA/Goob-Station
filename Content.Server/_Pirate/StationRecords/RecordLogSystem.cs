// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared._Pirate;
using Content.Shared._Pirate.StationRecords;
using Content.Shared.Access.Systems;
using Content.Shared.IdentityManagement;
using Content.Shared.Paper;
using Content.Shared.StationRecords;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Pirate.StationRecords;

public sealed class RecordLogSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly StationRecordsSystem _records = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    private static readonly SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");
    private static readonly TimeSpan PrintDelay = TimeSpan.FromSeconds(2.3f);
    private const string PrintPaper = "PaperOffice";

    // Leave room below the paper's 10,000-character limit for the overflow notice.
    private const int PrintBudget = 9000;

    private readonly HashSet<EntityUid> _activePrintJobs = new();

    public List<RecordLogEntry> GetLog(StationRecordKey key, RecordLogBook book)
    {
        return _records.TryGetRecord<RecordLogs>(key, out var logs)
            ? new List<RecordLogEntry>(logs.Book(book))
            : new List<RecordLogEntry>();
    }

    public RecordLogEntry? Add(StationRecordKey key, RecordLogBook book, RecordLogKind kind, string? topic,
        string message, EntityUid? author, EntityUid? origin, string? status = null)
    {
        if (!key.IsValid())
            return null;

        var logs = EnsureLogs(key);

        string? authorName = null;
        string? authorTitle = null;
        if (author is { } user)
        {
            if (_idCard.TryFindIdCard(user, out var idCard))
            {
                authorName = idCard.Comp.FullName;
                authorTitle = idCard.Comp.LocalizedJobTitle;
            }

            if (string.IsNullOrWhiteSpace(authorName))
                authorName = Identity.Name(user, EntityManager);
        }

        var entry = new RecordLogEntry
        {
            Id = logs.NextId++,
            Kind = kind,
            Topic = topic,
            Status = status,
            Message = message,
            AuthorName = authorName,
            AuthorTitle = string.IsNullOrWhiteSpace(authorTitle) ? null : authorTitle,
            StationName = origin is { } at && _station.GetOwningStation(at) is { } station ? Name(station) : null,
            Time = DateTime.UtcNow,
            RoundTime = _ticker.RoundDuration(),
        };

        logs.Book(book).Add(entry);
        return entry;
    }

    public bool TryDelete(StationRecordKey key, RecordLogBook book, uint entryId, out RecordLogEntry? removed)
    {
        removed = null;
        if (!_records.TryGetRecord<RecordLogs>(key, out var logs))
            return false;

        var entries = logs.Book(book);
        var index = entries.FindIndex(e => e.Id == entryId);
        if (index < 0 || entries[index].Kind == RecordLogKind.Background)
            return false;

        removed = entries[index];
        entries.RemoveAt(index);
        return true;
    }

    public void Print(EntityUid console, EntityUid user, StationRecordKey key, RecordLogBook book)
    {
        if (_activePrintJobs.Contains(console))
        {
            _popup.PopupEntity(Loc.GetString("records-log-print-busy"), console, user);
            return;
        }

        if (!_records.TryGetRecord<GeneralStationRecord>(key, out var person))
            return;

        var name = person.Name;
        var text = BuildPrintout(name, GetLog(key, book), book);

        _activePrintJobs.Add(console);
        _audio.PlayPvs(PrintSound, console);
        _popup.PopupEntity(Loc.GetString("records-log-print-start"), console, user);

        Timer.Spawn(PrintDelay, () =>
        {
            _activePrintJobs.Remove(console);
            if (TerminatingOrDeleted(console))
                return;

            var printed = Spawn(PrintPaper, Transform(console).Coordinates);
            if (!TryComp<PaperComponent>(printed, out var paper))
            {
                Del(printed);
                return;
            }

            _metaData.SetEntityName(printed, Loc.GetString("records-log-print-paper-name", ("name", name)));
            _paper.SetContent((printed, paper), text);
        });
    }

    private string BuildPrintout(string name, List<RecordLogEntry> log, RecordLogBook book)
    {
        var title = Loc.GetString($"records-log-print-title-{book.ToString().ToLowerInvariant()}");
        var text = new StringBuilder();
        text.AppendLine($"[head=2]{FormattedMessage.EscapeText($"{title} - {name}")}[/head]");
        text.AppendLine("────────────────────");

        if (log.Count == 0)
            text.AppendLine(Loc.GetString("records-log-empty"));

        for (var i = log.Count - 1; i >= 0; i--)
        {
            var entry = PrintEntry(log[i]);
            if (text.Length + entry.Length > PrintBudget)
            {
                text.AppendLine(Loc.GetString("records-log-print-more", ("count", i + 1)));
                break;
            }

            text.Append(entry);
        }

        return text.ToString();
    }

    private string PrintEntry(RecordLogEntry entry)
    {
        var title = entry.Kind switch
        {
            RecordLogKind.Background => Loc.GetString("records-log-kind-background"),
            RecordLogKind.StatusChange => entry.Topic ?? Loc.GetString("records-log-kind-status-change"),
            _ => Loc.GetString("records-log-kind-note"),
        };

        var author = entry.AuthorName
            ?? Loc.GetString(entry.Kind == RecordLogKind.Background ? "records-log-hr-author" : "records-log-unknown-author");
        if (!string.IsNullOrWhiteSpace(entry.AuthorTitle))
            author = $"{author} ({entry.AuthorTitle})";

        var text = new StringBuilder();
        text.AppendLine($"[bold]{FormattedMessage.EscapeText(title)}[/bold] ({FormatTime(entry.Time)})");
        text.AppendLine($"[italic]{FormattedMessage.EscapeText(author)}[/italic]");
        if (!string.IsNullOrWhiteSpace(entry.StationName))
            text.AppendLine(Loc.GetString("records-log-station", ("station", FormattedMessage.EscapeText(entry.StationName))));
        if (!string.IsNullOrWhiteSpace(entry.Message))
            text.AppendLine(FormattedMessage.EscapeText(entry.Message));
        text.AppendLine();
        return text.ToString();
    }

    private static string FormatTime(DateTime utc)
    {
        // Paper has no viewer timezone; print times explicitly in UTC.
        return $"{utc.Day:00}/{utc.Month:00}/{PirateStationCalendar.CurrentYear:0000} {utc.Hour:00}:{utc.Minute:00} UTC";
    }

    private RecordLogs EnsureLogs(StationRecordKey key)
    {
        if (_records.TryGetRecord<RecordLogs>(key, out var logs))
            return logs;

        logs = new RecordLogs();
        _records.AddRecordEntry(key, logs);
        return logs;
    }
}
