// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.CriminalRecords.Systems;
using Content.Server.Power.Components;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared._Pirate.CriminalRecords;
using Content.Shared.CriminalRecords;
using Content.Shared.Interaction;
using Content.Shared.StationRecords;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.CriminalRecords;

public sealed class CitationPaymentConsoleSystem : EntitySystem
{
    [Dependency] private readonly CriminalRecordsConsoleSystem _citations = default!;
    [Dependency] private readonly StationRecordsSystem _records = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CitationPaymentConsoleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<RecordModifiedEvent>(OnRecordChanged);
        SubscribeLocalEvent<RecordRemovedEvent>(OnRecordChanged);
        SubscribeLocalEvent<AfterGeneralRecordCreatedEvent>(OnRecordChanged);

        Subs.BuiEvents<CitationPaymentConsoleComponent>(CitationPaymentConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiUpdate);
            subs.Event<CitationPaymentRefreshMessage>(OnUiUpdate);
            subs.Event<CitationPaymentMessage>(OnPay);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CitationPaymentConsoleComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.ResultEnd is not { } end || _timing.CurTime < end)
                continue;

            component.ResultEnd = null;
            _appearance.SetData(uid, CitationPaymentConsoleVisuals.Status, CitationPaymentConsoleStatus.Idle);
        }
    }

    private void OnMapInit(Entity<CitationPaymentConsoleComponent> ent, ref MapInitEvent args) => UpdateState(ent);

    private void OnUiUpdate<T>(Entity<CitationPaymentConsoleComponent> ent, ref T args) => UpdateState(ent);

    private void OnRecordChanged<T>(T args) where T : StationRecordEvent
    {
        var query = EntityQueryEnumerator<CitationPaymentConsoleComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (args.Key.OriginStation == _station.GetOwningStation(uid))
                UpdateState((uid, component));
        }
    }

    private void OnPay(Entity<CitationPaymentConsoleComponent> ent, ref CitationPaymentMessage msg)
    {
        if (!_ui.IsUiOpen(ent.Owner, CitationPaymentConsoleUiKey.Key, msg.Actor)
            || !TryComp<ApcPowerReceiverComponent>(ent, out var power) || !power.Powered
            || !Transform(ent).Anchored
            || !_interaction.InRangeUnobstructed(msg.Actor, ent.Owner)
            || _station.GetOwningStation(ent) is not { } station)
        {
            return;
        }

        // Resolve both IDs against this terminal's station. The client never supplies a station or payment amount.
        var paid = _citations.TryPayCitation(ent, msg.Actor, new StationRecordKey(msg.RecordId, station), msg.CitationId);
        ShowResult(ent, paid ? CitationPaymentConsoleStatus.Accepted : CitationPaymentConsoleStatus.Denied);
        UpdateState(ent);
    }

    private void ShowResult(Entity<CitationPaymentConsoleComponent> ent, CitationPaymentConsoleStatus status)
    {
        ent.Comp.ResultEnd = _timing.CurTime + ent.Comp.ResultTime;
        _appearance.SetData(ent, CitationPaymentConsoleVisuals.Status, status);
    }

    private void UpdateState(Entity<CitationPaymentConsoleComponent> ent)
    {
        var people = new List<CitationPaymentPerson>();
        var station = _station.GetOwningStation(ent);
        if (station is { } stationId)
        {
            foreach (var (id, record) in _records.GetRecordsOfType<CriminalRecord>(stationId))
            {
                var citations = record.Citations
                    .Where(c => c.Status == CitationStatus.Active && c.Fine > 0)
                    .OrderByDescending(c => c.IssuedAt)
                    .Select(c => c with { })
                    .ToList();
                if (citations.Count == 0)
                    continue;

                var key = new StationRecordKey(id, stationId);
                var general = _records.TryGetRecord<GeneralStationRecord>(key, out var live)
                    ? live
                    : record.GeneralRecordSnapshot;
                people.Add(new CitationPaymentPerson(id, general?.Name ?? _records.RecordName(key),
                    general?.JobTitle ?? Loc.GetString("suit-sensor-component-unknown-job"), citations));
            }
        }

        people.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
        _ui.SetUiState(ent.Owner, CitationPaymentConsoleUiKey.Key, new CitationPaymentConsoleState(people, station != null));
    }
}
