// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Text;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared._Pirate;
using Content.Shared._Pirate.StationRecords;
using Content.Shared.Access.Systems;
using Content.Shared.CCVar;
using Content.Shared.IdentityManagement;
using Content.Shared.Paper;
using Content.Shared.StationRecords;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
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
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private static readonly SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");
    private static readonly TimeSpan PrintDelay = TimeSpan.FromSeconds(2.3f);
    private const string PrintPaper = "PaperOffice";
    // Leave room under the paper's 10,000-character limit for the overflow notice.
    private const int PrintBudget = 9000;

    private readonly HashSet<EntityUid> _activePrintJobs = new();
    private TimeZoneInfo _printZone = TimeZoneInfo.Local;
    private bool _showArchived = true;

    private void SetPrintZone(string id)
    {
        _printZone = TimeZoneInfo.Local;
        if (string.IsNullOrWhiteSpace(id))
            return;

        // Older tz databases only know Kyiv by its old spelling.
        foreach (var candidate in new[] { id.Trim(), id.Trim().Replace("Kyiv", "Kiev") })
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(candidate, out var zone))
            {
                _printZone = zone;
                return;
            }
        }

        Log.Warning($"Unknown time zone '{id}' in {CCVars.RecordsPrintTimeZone.Name}, printing record histories in the server's zone ({TimeZoneInfo.Local.Id}).");
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AfterGeneralRecordCreatedEvent>(OnGeneralRecordCreated);
        Subs.CVar(_cfg, CCVars.RecordsPrintTimeZone, SetPrintZone, true);
        Subs.CVar(_cfg, CCVars.RecordsHistoryPersistence, enabled => _showArchived = enabled, true);
    }
    private void OnGeneralRecordCreated(AfterGeneralRecordCreatedEvent args)
    {
        if (args.Profile.RecordBackgrounds.Count == 0)
            return;

        foreach (var background in args.Profile.RecordBackgrounds)
        {
            AddBackground(args.Key, background);
        }

        _records.Synchronize(args.Key);
    }

    private void AddBackground(StationRecordKey key, RecordBackgroundEntry background)
    {
        var text = background.Body.Trim();
        if (text.Length == 0)
            return;

        var title = background.Title.Trim();
        if (AddEntry(key, background.Book, RecordLogKind.Background, title.Length > 0 ? title : null, text, null, null)
            is not { } entry)
        {
            return;
        }

        // An empty author shows NanoTrasen HR.
        var author = background.Author.Trim();
        entry.AuthorName = author.Length > 0 ? author : null;

        // Dated to the shift start, also for late joiners: it opens this round's part of the history.
        entry.Time = DateTime.UtcNow - _ticker.RoundDuration();
        entry.RoundTime = TimeSpan.Zero;
    }
    public List<RecordLogEntry> GetLog(StationRecordKey key, RecordLogBook book)
    {
        if (!_records.TryGetRecord<RecordLogs>(key, out var logs))
            return new List<RecordLogEntry>();

        return RecordLogOrder.ForDisplay(_showArchived
            ? logs.Book(book)
            : logs.Book(book).Where(e => !e.Archived));
    }
    public void AddArchived(StationRecordKey key, IEnumerable<(RecordLogBook Book, RecordLogEntry Entry)> entries)
    {
        if (!key.IsValid())
            return;

        var logs = EnsureLogs(key);
        foreach (var (book, entry) in entries)
        {
            logs.Book(book).Add(entry with { Id = logs.NextId++, Archived = true });
        }

        _records.Synchronize(key);
    }
    public RecordLogEntry? Add(StationRecordKey key, RecordLogBook book, RecordLogKind kind, string? topic,
        string message, EntityUid? author, EntityUid? origin, string? status = null)
    {
        var entry = AddEntry(key, book, kind, topic, message, author, origin, status);
        if (entry != null)
            _records.Synchronize(key);

        return entry;
    }

    private RecordLogEntry? AddEntry(StationRecordKey key, RecordLogBook book, RecordLogKind kind, string? topic,
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
    public bool TryAppend(StationRecordKey key, RecordLogBook book, uint entryId, string line, string? status = null)
    {
        if (!_records.TryGetRecord<RecordLogs>(key, out var logs)
            || logs.Book(book).Find(e => e.Id == entryId) is not { Archived: false } entry)
        {
            return false;
        }

        entry.Message = entry.Message.Length == 0 ? line : $"{entry.Message}\n{line}";
        if (status != null)
            entry.Status = status;

        _records.Synchronize(key);
        return true;
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
        _records.Synchronize(key);
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

        for (var i = 0; i < log.Count; i++)
        {
            var entry = PrintEntry(log[i]);
            if (text.Length + entry.Length > PrintBudget)
            {
                text.AppendLine(Loc.GetString("records-log-print-more", ("count", log.Count - i)));
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
            RecordLogKind.Background => entry.Topic ?? Loc.GetString("records-log-kind-background"),
            RecordLogKind.StatusChange => entry.Topic ?? Loc.GetString("records-log-kind-status-change"),
            RecordLogKind.Citation => entry.Topic ?? Loc.GetString("records-log-kind-citation"),
            _ => Loc.GetString("records-log-kind-note"),
        };

        var author = entry.AuthorName
            ?? Loc.GetString(entry.Kind == RecordLogKind.Background ? "records-log-hr-author" : "records-log-unknown-author");
        if (!string.IsNullOrWhiteSpace(entry.AuthorTitle))
            author = $"{author} ({entry.AuthorTitle})";

        var text = new StringBuilder();
        // The shift-start timestamp positions the HR file; it isn't a date anyone wrote down.
        text.AppendLine(entry.Kind == RecordLogKind.Background
            ? $"[bold]{FormattedMessage.EscapeText(title)}[/bold]"
            : $"[bold]{FormattedMessage.EscapeText(title)}[/bold] ({FormatTime(entry.Time)})");
        text.AppendLine($"[italic]{FormattedMessage.EscapeText(author)}[/italic]");
        if (!string.IsNullOrWhiteSpace(entry.StationName))
            text.AppendLine(Loc.GetString("records-log-station", ("station", FormattedMessage.EscapeText(entry.StationName))));
        if (!string.IsNullOrWhiteSpace(entry.Message))
            text.AppendLine(FormattedMessage.EscapeText(entry.Message));
        text.AppendLine();
        return text.ToString();
    }
    private string FormatTime(DateTime utc)
    {
        // Printed paper has no reader context; use the configured zone.
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _printZone);
        return $"{local.Day:00}/{local.Month:00}/{PirateStationCalendar.CurrentYear:0000} {local.Hour:00}:{local.Minute:00}";
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
