// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics; // Pirate: multiz
using Content.Server.Antag;
using Content.Server.Station.Components;
using Content.Server.StationEvents.Components;
using Content.Server._Pirate.ZLevels.Spawning; // Pirate: multiz
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components; // Pirate: multiz
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.StationEvents.Events;

/// <summary>
/// Station event component for spawning this rules antags in space around a station.
/// </summary>
public sealed class SpaceSpawnRule : StationEventSystem<SpaceSpawnRuleComponent>
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly CEZLevelFloorGridsSystem _zFloors = default!; // Pirate: multiz

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpaceSpawnRuleComponent, AntagSelectLocationEvent>(OnSelectLocation);
    }

    protected override void Added(EntityUid uid, SpaceSpawnRuleComponent comp, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, comp, gameRule, args);

        if (!TryGetRandomStation(out var station))
        {
            ForceEndSelf(uid, gameRule);
            return;
        }

        #region Pirate: multiz
        // Pick one floor for both the ghost-role preview and the real spawn.
        if (!TryComp<StationDataComponent>(station, out var stationData)
            || GetStationMainGrid(stationData) is not { } mainGrid)
        {
            Sawmill.Warning($"Chosen station has no main grid, cannot pick location for {ToPrettyString(uid):rule}");
            ForceEndSelf(uid, gameRule);
            return;
        }

        var floorGrid = _zFloors.GetRandomFloorGrid(mainGrid.Owner);
        if (!TryComp<MapGridComponent>(floorGrid, out var floorGridComp))
        {
            ForceEndSelf(uid, gameRule);
            return;
        }

        comp.Coords = GetSpaceLocationAround(floorGrid, floorGridComp, comp.SpawnDistance);
        Sawmill.Info($"Picked location {comp.Coords} for {ToPrettyString(uid):rule}");
        #endregion
    }

    #region Pirate: multiz
    private MapCoordinates GetSpaceLocationAround(EntityUid gridUid, MapGridComponent grid, float spawnDistance)
    {
        var size = grid.LocalAABB.Size.Length() / 2;
        var distance = size + spawnDistance;
        var angle = RobustRandom.NextAngle();
        var location = angle.ToVec() * distance;

        var xform = Transform(gridUid);
        // Use bounds center so the spawn ring stays symmetric around offset grids.
        var center = Vector2.Transform(grid.LocalAABB.Center, _transform.GetWorldMatrix(gridUid));
        var position = center + location;
        return new MapCoordinates(position, xform.MapID);
    }
    #endregion

    private void OnSelectLocation(Entity<SpaceSpawnRuleComponent> ent, ref AntagSelectLocationEvent args)
    {
        if (ent.Comp.Coords is {} coords)
            args.Coordinates.Add(coords);
    }
}
