// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._Pirate.ListeningPost;

public sealed class LongRangeCrewMonitorSystem : EntitySystem
{
    public EntityUid? FindLargestStationGridInMap(MapId map)
    {
        (EntityUid?, int) biggestGrid = (null, 0);
        var query = EntityQueryEnumerator<StationMemberComponent, MapGridComponent, TransformComponent>();
        while (query.MoveNext(out var grid, out _, out var mapGrid, out var xform))
        {
            if (xform.MapID == map && mapGrid.ChunkCount > biggestGrid.Item2)
                biggestGrid = (grid, mapGrid.ChunkCount);
        }

        return biggestGrid.Item1;
    }
}
