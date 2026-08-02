// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Anomaly;
using Content.Server._Pirate.ZLevels.Spawning; // Pirate: multiz
using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;

namespace Content.Server.StationEvents.Events;

public sealed class AnomalySpawnRule : StationEventSystem<AnomalySpawnRuleComponent>
{
    [Dependency] private readonly AnomalySystem _anomaly = default!;
    [Dependency] private readonly CEZLevelFloorGridsSystem _zFloors = default!; // Pirate: multiz

    protected override void Added(EntityUid uid, AnomalySpawnRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        if (!TryComp<StationEventComponent>(uid, out var stationEvent))
            return;

        var str = Loc.GetString("anomaly-spawn-event-announcement",
            ("sighting", Loc.GetString($"anomaly-spawn-sighting-{RobustRandom.Next(1, 6)}")));
        stationEvent.StartAnnouncement = str;

        base.Added(uid, component, gameRule, args);
    }

    protected override void Started(EntityUid uid, AnomalySpawnRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!TryGetRandomStation(out var chosenStation))
            return;

        if (!TryComp<StationDataComponent>(chosenStation, out var stationData))
            return;

        #region Pirate: multiz
        if (GetStationMainGrid(stationData) is not { } mainGrid)
            return;
        #endregion

        var amountToSpawn = 1;
        for (var i = 0; i < amountToSpawn; i++)
        {
            #region Pirate: multiz
            var spawnGrid = _zFloors.GetRandomFloorGrid(mainGrid.Owner);
            _anomaly.SpawnOnRandomGridLocation(spawnGrid, component.AnomalySpawnerPrototype);
            #endregion
        }
    }
}
