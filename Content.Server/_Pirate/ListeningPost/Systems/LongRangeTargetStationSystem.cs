// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.ZLevels.Spawning;
using Content.Shared.Station.Components;
using Content.Shared.Station;
using Robust.Shared.Map;

namespace Content.Server._Pirate.ListeningPost.Systems;

public sealed class LongRangeTargetStationSystem : EntitySystem
{
    [Dependency] private readonly CEZLevelFloorGridsSystem _zFloors = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;

    public (EntityUid Station, EntityUid Grid)? ResolveTargetStation(MapId? preferredMap = null)
    {
        EntityUid? station = null;

        if (preferredMap is { } map && map != MapId.Nullspace)
            station = _station.GetStationInMap(map);

        if (station == null)
        {
            var stations = EntityQueryEnumerator<StationDataComponent>();
            while (stations.MoveNext(out var uid, out var data))
            {
                if (data.Grids.Count == 0)
                    continue;

                station = uid;
                break;
            }
        }

        if (station == null || _zFloors.GetStationDefaultGrid(station.Value) is not { } grid)
            return null;

        return (station.Value, grid);
    }
}
