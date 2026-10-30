// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Pirate.Shared._Pirate.StationRecords;
using Content.Server._Pirate.StationRecords;
using Content.Server.CartridgeLoader;
using Content.Server.Station.Systems;
using Content.Server.StationRecords;
using Content.Server.StationRecords.Components;
using Content.Server.StationRecords.Systems;
using Content.Shared._Pirate.StationRecords;
using Content.Shared.Access.Systems;
using Content.Shared.CartridgeLoader;
using Content.Shared.CriminalRecords;
using Content.Shared.CriminalRecords.Components;
using Content.Shared.PsionicsRecords;
using Content.Shared.PsionicsRecords.Components;
using Content.Shared.StationRecords;
using Robust.Server.GameObjects;

namespace Content.Pirate.Server._Pirate.StationRecords;

public sealed class RecordsCartridgeSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly RecordLogSystem _logs = default!;
    [Dependency] private readonly StationRecordsSystem _records = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private bool _refresh;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RecordsCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<RecordsCartridgeComponent, CartridgeMessageEvent>(OnMessage);

        SubscribeLocalEvent<RecordModifiedEvent>(_ => _refresh = true);
        SubscribeLocalEvent<RecordRemovedEvent>(_ => _refresh = true);
        SubscribeLocalEvent<AfterGeneralRecordCreatedEvent>(_ => _refresh = true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_refresh)
            return;

        _refresh = false;
        var query = EntityQueryEnumerator<RecordsCartridgeComponent, CartridgeComponent>();
        while (query.MoveNext(out var uid, out var comp, out var cartridge))
        {
            if (cartridge.LoaderUid is not { } loader
                || !TryComp<CartridgeLoaderComponent>(loader, out var loaderComp)
                || loaderComp.ActiveProgram != uid
                || !_ui.IsUiOpen(loader, loaderComp.UiKey))
            {
                continue;
            }

            UpdateUiState((uid, comp), loader);
        }
    }

    private void OnUiReady(Entity<RecordsCartridgeComponent> ent, ref CartridgeUiReadyEvent args)
    {
        UpdateUiState(ent, args.Loader);
    }

    private void OnMessage(Entity<RecordsCartridgeComponent> ent, ref CartridgeMessageEvent args)
    {
        if (args is not RecordsCartridgeUiMessageEvent msg)
            return;

        ent.Comp.Tab = msg.Tab;

        // The client has all tabs; save this selection for the next opening.
        if (msg.Message is not { } message)
            return;

        if (IsAllowedMessage(message)
            && (message is SelectStationRecord || CanEdit(ent, msg.Tab, args.Actor)))
        {
            message.UiKey = GetConsoleKey(msg.Tab);
            message.Actor = args.Actor;
            message.Entity = GetNetEntity(ent);
            RaiseLocalEvent(ent, (object) message);
        }

        UpdateUiState(ent, GetEntity(args.LoaderUid));
    }

    // Only forward messages supported by the PDA.
    private static bool IsAllowedMessage(BoundUserInterfaceMessage message)
    {
        return message is SelectStationRecord or RecordLogAddNoteMessage or RecordLogDeleteMessage;
    }

    private static Enum GetConsoleKey(RecordsCartridgeTab tab)
    {
        return tab switch
        {
            RecordsCartridgeTab.Security => CriminalRecordsConsoleKey.Key,
            RecordsCartridgeTab.Psionics => PsionicsRecordsConsoleKey.Key,
            _ => GeneralStationRecordConsoleKey.Key,
        };
    }

    private static RecordLogBook GetBook(RecordsCartridgeTab tab)
    {
        return tab switch
        {
            RecordsCartridgeTab.Security => RecordLogBook.Security,
            RecordsCartridgeTab.Psionics => RecordLogBook.Psionics,
            _ => RecordLogBook.Crew,
        };
    }

    private bool CanEdit(Entity<RecordsCartridgeComponent> ent, RecordsCartridgeTab tab, EntityUid user)
    {
        if (!ent.Comp.Access.TryGetValue(tab, out var lists) || lists.Count == 0)
            return true;

        var tags = _access.FindAccessTags(user);
        return lists.Any(set => set.IsSubsetOf(tags));
    }

    private void UpdateUiState(Entity<RecordsCartridgeComponent> ent, EntityUid loader)
    {
        _cartridgeLoader.UpdateCartridgeUiState(loader, BuildState(ent, loader));
    }

    private RecordsCartridgeUiState BuildState(Entity<RecordsCartridgeComponent> ent, EntityUid loader)
    {
        var state = new RecordsCartridgeUiState { Tab = ent.Comp.Tab };

        EntityUid? actor = null;
        if (TryComp<CartridgeLoaderComponent>(loader, out var loaderComp)
            && _ui.GetActors(loader, loaderComp.UiKey).FirstOrDefault() is { Valid: true } user)
        {
            actor = user;
        }

        Entity<StationRecordsComponent>? station = null;
        if (_station.GetOwningStation(ent) is { } owner && TryComp<StationRecordsComponent>(owner, out var stationRecords))
            station = (owner, stationRecords);

        foreach (var tab in Enum.GetValues<RecordsCartridgeTab>())
        {
            var page = new RecordsCartridgePage
            {
                Tab = tab,
                CanEdit = actor is { } a && CanEdit(ent, tab, a),
            };

            if (station is { } s)
                FillPage(ent, s, page);

            state.Pages.Add(page);
        }

        return state;
    }

    private void FillPage(EntityUid uid, Entity<StationRecordsComponent> station, RecordsCartridgePage page)
    {
        uint? active;
        switch (page.Tab)
        {
            case RecordsCartridgeTab.Security:
                if (!TryComp<CriminalRecordsConsoleComponent>(uid, out var security))
                    return;

                page.SelectionRequest = security.SelectionRequest;
                page.RecordListing = SecurityListing(station);
                active = security.ActiveKey;
                break;
            case RecordsCartridgeTab.Psionics:
                if (!TryComp<PsionicsRecordsConsoleComponent>(uid, out var psionics))
                    return;

                page.SelectionRequest = psionics.SelectionRequest;
                page.RecordListing = PsionicsListing(station);
                active = psionics.ActiveKey;
                break;
            default:
                if (!TryComp<GeneralStationRecordConsoleComponent>(uid, out var crew))
                    return;

                page.SelectionRequest = crew.SelectionRequest;
                page.RecordListing = _records.BuildListing(station, null);
                active = crew.ActiveKey;
                break;
        }

        if (active is not { } id || !page.RecordListing.TryGetValue(id, out var name))
            return;

        page.SelectedKey = id;
        page.Name = name;
        page.Log = _logs.GetLog(new StationRecordKey(id, station), GetBook(page.Tab));
    }

    private Dictionary<uint, string> SecurityListing(Entity<StationRecordsComponent> station)
    {
        var listing = new Dictionary<uint, string>();
        foreach (var (id, criminal) in _records.GetRecordsOfType<CriminalRecord>(station, station.Comp))
        {
            // Security records can outlive their general records.
            if (_records.TryGetRecord<GeneralStationRecord>(new StationRecordKey(id, station), out var general, station.Comp))
                listing[id] = general.Name;
            else if (criminal.GeneralRecordSnapshot is { } snapshot)
                listing[id] = snapshot.Name;
        }

        return listing;
    }

    private Dictionary<uint, string> PsionicsListing(Entity<StationRecordsComponent> station)
    {
        var listing = new Dictionary<uint, string>();

        foreach (var (id, general) in _records.GetRecordsOfType<GeneralStationRecord>(station, station.Comp))
        {
            if (_records.TryGetRecord<PsionicsRecord>(new StationRecordKey(id, station), out _, station.Comp))
                listing[id] = general.Name;
        }

        return listing;
    }
}
