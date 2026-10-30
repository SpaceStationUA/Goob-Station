// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;
using Content.Server._Pirate.Banking;
using Content.Server._Pirate.CriminalRecords;
using Content.Server.Cargo.Systems;
using Content.Shared._Pirate.Banking;
using Content.Shared._Pirate.CriminalRecords;
using Content.Shared._Pirate.StationRecords;
using Content.Shared.Access.Systems;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.CriminalRecords;
using Content.Shared.CriminalRecords.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Paper;
using Content.Shared.StationRecords;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.CriminalRecords.Systems;

public sealed partial class CriminalRecordsConsoleSystem
{
    [Dependency] private readonly BankCardSystem _bankCards = default!;
    [Dependency] private readonly CargoSystem _cargo = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly CitationAccountArrestSystem _citationArrests = default!;

    private static readonly ProtoId<CargoAccountPrototype> CitationBudget = "Security";
    private static readonly EntProtoId CitationTicket = "FineTicket";
    private static readonly SoundSpecifier CitationPaidSound = new SoundPathSpecifier("/Audio/_Pirate/Machines/chime.ogg");
    private static readonly SoundSpecifier CitationDeniedSound = new SoundPathSpecifier("/Audio/_Pirate/Machines/buzz-sigh.ogg");
    private static readonly SoundSpecifier CitationPrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");
    private static readonly TimeSpan CitationPrintDelay = TimeSpan.FromSeconds(2);

    private void OnAddCitation(Entity<CriminalRecordsConsoleComponent> ent, ref CriminalRecordAddCitation msg)
    {
        if (!CheckSelected(ent, msg.Actor, out var mob, out var key)
            || !_records.TryGetRecord<CriminalRecord>(key.Value, out var record))
        {
            return;
        }

        var reason = RecordLogNotes.Sanitize(msg.Reason);
        var details = RecordLogNotes.Sanitize(msg.Details);
        if (reason.Length < 1 || reason.Length > CriminalRecordCitations.MaxReasonLength
            || details.Length > CriminalRecordCitations.MaxDetailsLength
            || msg.Fine < 1 || msg.Fine > CriminalRecordCitations.MaxFine)
        {
            return;
        }

        GetOfficer(mob.Value, out var officer);
        var citation = new CriminalRecordCitation
        {
            Id = record.NextCitationId++,
            Reason = reason,
            Details = details,
            Fine = msg.Fine,
            IssuedBy = officer,
            IssuedAt = DateTime.UtcNow,
        };
        record.Citations.Add(citation);
        _citationArrests.Refresh(key.Value);
        _citationArrests.NotifyIssued(key.Value, citation);

        LogCitation(key.Value, citation, mob.Value, ent);
        SendCitationRadio(ent, key.Value, record, citation, ("officer", officer));
        PrintCitationTicket(ent, CitationTarget(key.Value, record).Name, citation);
    }

    private void OnPayCitation(Entity<CriminalRecordsConsoleComponent> ent, ref CriminalRecordPayCitation msg)
    {
        // Anyone may pay another person's fine.
        if (ent.Comp.ActiveKey is not { } id || _station.GetOwningStation(ent) is not { } station)
            return;

        var key = new StationRecordKey(id, station);
        var citationId = msg.CitationId;
        if (!_records.TryGetRecord<CriminalRecord>(key, out var record)
            || record.Citations.Find(c => c.Id == citationId) is not { Status: CitationStatus.Active } citation)
        {
            return;
        }

        var user = msg.Actor;
        if (!TryComp<StationBankAccountComponent>(station, out var budget)
            || !_cargo.TryGetAccount((station, budget), CitationBudget, out _))
        {
            DenyCitationPayment(ent, user, "criminal-records-citation-pay-no-budget");
            return;
        }

        if (!_idCard.TryFindIdCard(user, out var idCard)
            || !TryComp<BankCardComponent>(idCard, out var bankCard)
            || bankCard.AccountId is not { } accountId
            || bankCard.CommandBudgetCard)
        {
            DenyCitationPayment(ent, user, "criminal-records-citation-pay-no-card");
            return;
        }

        var details = Loc.GetString("criminal-records-citation-transaction", ("reason", citation.Reason));
        if (!_bankCards.TryChangeBalance(accountId, -citation.Fine, BankTransactionType.Fine, details))
        {
            DenyCitationPayment(ent, user, "criminal-records-citation-pay-no-funds");
            return;
        }

        _cargo.TryAdjustBankAccount((station, budget), CitationBudget, citation.Fine);

        var payer = idCard.Comp.FullName;
        if (string.IsNullOrWhiteSpace(payer))
            payer = Identity.Name(user, EntityManager);

        citation.Status = CitationStatus.Paid;
        citation.ClosedBy = payer;
        citation.ClosedAt = DateTime.UtcNow;
        _citationArrests.Refresh(key);

        _audio.PlayPvs(CitationPaidSound, ent);
        _popup.PopupEntity(Loc.GetString("criminal-records-citation-pay-success", ("fine", citation.Fine)), ent, user);
        UpdateCitationLog(key, citation);
        SendCitationRadio(ent, key, record, citation, ("payer", payer));
    }

    private void OnVoidCitation(Entity<CriminalRecordsConsoleComponent> ent, ref CriminalRecordVoidCitation msg)
    {
        var citationId = msg.CitationId;
        if (!CheckSelected(ent, msg.Actor, out var mob, out var key)
            || !_records.TryGetRecord<CriminalRecord>(key.Value, out var record)
            || record.Citations.Find(c => c.Id == citationId) is not { Status: CitationStatus.Active } citation)
        {
            return;
        }

        GetOfficer(mob.Value, out var officer);
        citation.Status = CitationStatus.Voided;
        citation.ClosedBy = officer;
        citation.ClosedAt = DateTime.UtcNow;
        _citationArrests.Refresh(key.Value);

        UpdateCitationLog(key.Value, citation);
        SendCitationRadio(ent, key.Value, record, citation, ("officer", officer));
    }

    private void DenyCitationPayment(EntityUid console, EntityUid user, string message)
    {
        _audio.PlayPvs(CitationDeniedSound, console);
        _popup.PopupEntity(Loc.GetString(message), console, user);
    }

    private void LogCitation(StationRecordKey key, CriminalRecordCitation citation, EntityUid author, EntityUid console)
    {
        var topic = Loc.GetString("criminal-records-citation-log-topic", ("reason", citation.Reason), ("fine", citation.Fine));
        var entry = _logs.Add(key, RecordLogBook.Security, RecordLogKind.Citation, topic, citation.Details, author, console,
            citation.Status.ToString());
        citation.LogEntryId = entry?.Id;
        if (entry == null)
        {
            _records.Synchronize(key);
            return;
        }

        // Matching timestamps link the citation to its history entry across rounds.
        citation.IssuedAt = entry.Time;
    }

    // Keep one history entry per citation.
    private void UpdateCitationLog(StationRecordKey key, CriminalRecordCitation citation)
    {
        var line = Loc.GetString($"criminal-records-citation-log-{citation.Status.ToString().ToLowerInvariant()}",
            ("name", citation.ClosedBy ?? Loc.GetString("criminal-records-console-unknown-officer")));
        if (citation.LogEntryId is not { } entryId
            || !_logs.TryAppend(key, RecordLogBook.Security, entryId, line, citation.Status.ToString()))
        {
            // Sync even if the history entry was deleted.
            _records.Synchronize(key);
        }
    }

    private void SendCitationRadio(Entity<CriminalRecordsConsoleComponent> ent, StationRecordKey key, CriminalRecord record,
        CriminalRecordCitation citation, (string, object) actor)
    {
        var (name, job) = CitationTarget(key, record);
        var message = Loc.GetString($"criminal-records-citation-radio-{citation.Status.ToString().ToLowerInvariant()}",
            ("name", name), ("job", job), ("reason", citation.Reason), ("fine", citation.Fine), actor);
        _radio.SendRadioMessage(ent, message, ent.Comp.SecurityChannel, ent);
    }

    private (string Name, string Job) CitationTarget(StationRecordKey key, CriminalRecord record)
    {
        if (TryGetEffectiveGeneralRecord(key, record) is { } general)
            return (general.Name, general.JobTitle);

        return (_records.RecordName(key), Loc.GetString("suit-sensor-component-unknown-job"));
    }

    private void PrintCitationTicket(EntityUid console, string name, CriminalRecordCitation citation)
    {
        var text = BuildCitationTicket(name, citation);
        _audio.PlayPvs(CitationPrintSound, console);

        Timer.Spawn(CitationPrintDelay, () =>
        {
            if (TerminatingOrDeleted(console))
                return;

            var ticket = SpawnAtPosition(CitationTicket, Transform(console).Coordinates);
            if (!TryComp<PaperComponent>(ticket, out var paper))
            {
                Del(ticket);
                return;
            }

            _metaData.SetEntityName(ticket, Loc.GetString("criminal-records-citation-ticket-name", ("name", name)));
            _paper.SetContent((ticket, paper), text);
        });
    }

    private string BuildCitationTicket(string name, CriminalRecordCitation citation)
    {
        var text = new StringBuilder();
        text.AppendLine($"[head=2]{Loc.GetString("criminal-records-citation-ticket-title")}[/head]");
        Field("offender", name);
        Field("violation", citation.Reason);
        Field("fine", Loc.GetString("criminal-records-citation-spesos", ("fine", citation.Fine)));
        Field("officer", citation.IssuedBy ?? Loc.GetString("criminal-records-console-unknown-officer"));
        return text.ToString();

        void Field(string label, string value)
        {
            text.AppendLine($"[bold]{Loc.GetString($"criminal-records-citation-ticket-{label}")}[/bold] {FormattedMessage.EscapeText(value)}");
        }
    }
}
