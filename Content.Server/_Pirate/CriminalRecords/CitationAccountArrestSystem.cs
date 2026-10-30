// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server._Pirate.Banking;
using Content.Server.StationRecords.Systems;
using Content.Shared._Pirate.Banking;
using Content.Shared._Pirate.CriminalRecords;
using Content.Shared.CriminalRecords;
using Content.Shared.GameTicking;
using Content.Shared.StationRecords;
using Robust.Shared.Utility;

namespace Content.Server._Pirate.CriminalRecords;

public sealed class CitationAccountArrestSystem : EntitySystem
{
    [Dependency] private readonly BankCardSystem _bankCards = default!;
    [Dependency] private readonly StationRecordsSystem _records = default!;

    // An account stays arrested while any record holds it.
    private readonly Dictionary<StationRecordKey, HashSet<int>> _arrests = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RecordRemovedEvent>(args => Refresh(args.Key));
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _arrests.Clear());
    }

    public void Refresh(StationRecordKey key)
    {
        var accounts = new HashSet<int>();
        if (_records.TryGetRecord<CriminalRecord>(key, out var record)
            && record.Citations.Any(c => c.Status == CitationStatus.Active))
        {
            CollectAccounts(key, record, accounts);
        }

        _arrests.Remove(key, out var previous);
        if (accounts.Count > 0)
            _arrests[key] = accounts;

        foreach (var account in accounts.Concat(previous ?? []))
        {
            _bankCards.SetArrested(account, _arrests.Values.Any(held => held.Contains(account)));
        }
    }

    public void NotifyIssued(StationRecordKey key, CriminalRecordCitation citation)
    {
        if (!_records.TryGetRecord<CriminalRecord>(key, out var record))
            return;

        var accounts = new HashSet<int>();
        CollectAccounts(key, record, accounts);
        _bankCards.NotifyAccountHolders(accounts, Loc.GetString("bank-card-citation-issued",
            ("fine", citation.Fine), ("reason", FormattedMessage.EscapeText(citation.Reason))));
    }

    // Name matching covers records that are not linked to an ID card.
    private void CollectAccounts(StationRecordKey key, CriminalRecord record, HashSet<int> accounts)
    {
        var query = EntityQueryEnumerator<BankCardComponent, StationRecordKeyStorageComponent>();
        while (query.MoveNext(out var card, out var storage))
        {
            if (storage.Key is { } cardKey && cardKey.Equals(key) && card.AccountId is { } id && !card.CommandBudgetCard)
                accounts.Add(id);
        }

        var name = _records.TryGetRecord<GeneralStationRecord>(key, out var general)
            ? general.Name
            : record.GeneralRecordSnapshot?.Name;
        if (!string.IsNullOrWhiteSpace(name))
            accounts.UnionWith(_bankCards.GetAccountIdsByName(name));
    }
}
