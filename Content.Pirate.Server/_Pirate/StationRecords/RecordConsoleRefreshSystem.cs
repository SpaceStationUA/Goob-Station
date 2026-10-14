// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.StationRecords.Components;
using Content.Server.StationRecords.Systems;
using Content.Shared.CriminalRecords;
using Content.Shared.CriminalRecords.Components;
using Content.Shared.PsionicsRecords;
using Content.Shared.PsionicsRecords.Components;
using Content.Shared.StationRecords;
using Robust.Server.GameObjects;

namespace Content.Pirate.Server._Pirate.StationRecords;

// Console subscriptions are directed; relay record changes to open consoles.
public sealed class RecordConsoleRefreshSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RecordModifiedEvent>(OnRecordModified);
    }

    private void OnRecordModified(RecordModifiedEvent ev)
    {
        Relay<CriminalRecordsConsoleComponent>(ev, CriminalRecordsConsoleKey.Key);
        Relay<GeneralStationRecordConsoleComponent>(ev, GeneralStationRecordConsoleKey.Key);
        Relay<PsionicsRecordsConsoleComponent>(ev, PsionicsRecordsConsoleKey.Key);
    }

    private void Relay<T>(RecordModifiedEvent ev, Enum key) where T : IComponent
    {
        var query = EntityQueryEnumerator<T>();
        while (query.MoveNext(out var uid, out _))
        {
            if (_ui.IsUiOpen(uid, key))
                RaiseLocalEvent(uid, ev);
        }
    }
}
