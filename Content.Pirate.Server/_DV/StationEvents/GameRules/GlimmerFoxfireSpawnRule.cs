using System.Numerics;
using Content.Server._DV.StationEvents.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Nyanotrasen.Research.SophicScribe;
using Content.Server.Research.Oracle;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server._DV.StationEvents.Events;

public sealed class GlimmerFoxfireSpawnRule : StationEventSystem<GlimmerFoxfireSpawnRuleComponent>
{
    [Dependency] private readonly SharedPointLightSystem _light = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    protected override void Started(EntityUid uid,
        GlimmerFoxfireSpawnRuleComponent comp,
        GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        var locations = new List<EntityCoordinates>();
        var queryScribe = EntityQueryEnumerator<SophicScribeComponent, TransformComponent>();
        var queryOracle = EntityQueryEnumerator<OracleComponent, TransformComponent>();

        while (queryScribe.MoveNext(out _, out var transform))
        {
            locations.Add(transform.Coordinates);
        }

        while (queryOracle.MoveNext(out _, out var transform))
        {
            locations.Add(transform.Coordinates);
        }

        if (locations.Count == 0)
            return;

        var selectedLocation = RobustRandom.Pick(locations);

        var amountToSpawn = RobustRandom.Next(comp.MinimumSpawned, comp.MaximumSpawned);
        for (var i = 0; i < amountToSpawn; i++)
        {
            var spawnLocation = TryGetValidSpawnLocation(selectedLocation, comp.SpawnRange, comp.MaxPlacementAttempts);
            if (spawnLocation == null)
                continue;

            var color = Color.GhostWhite;
            if (comp.RandomColorList != null && comp.RandomColorList.Count != 0)
                color = RobustRandom.Pick(comp.RandomColorList);

            var fireEnt = Spawn(comp.FoxfirePrototype, spawnLocation.Value);
            _light.SetColor(fireEnt, color);
        }
    }

    /// <summary>
    /// Picks a random tile offset from <paramref name="origin"/> that is not inside a wall.
    /// Returns null if no valid tile is found within <paramref name="maxAttempts"/> tries.
    /// </summary>
    private EntityCoordinates? TryGetValidSpawnLocation(EntityCoordinates origin, int range, int maxAttempts)
    {
        // Resolve the grid the origin sits on so we can do tile lookups.
        var gridUid = origin.GetGridUid(EntityManager);
        if (gridUid == null || !TryComp<MapGridComponent>(gridUid, out var gridComp))
        {
            // No grid — fall back to the raw offset without validation.
            return origin.Offset(new Vector2(
                RobustRandom.Next(-range, range),
                RobustRandom.Next(-range, range)));
        }

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var candidate = origin.Offset(new Vector2(
                RobustRandom.Next(-range, range),
                RobustRandom.Next(-range, range)));

            var tileRef = _map.GetTileRef((gridUid.Value, gridComp), candidate);

            // Skip empty/space tiles.
            if (tileRef.Tile.IsEmpty)
                continue;

            // Skip tiles blocked by walls (airtight structures).
            var tilePos = tileRef.GridIndices;
            if (_atmosphere.IsTileAirBlockedCached(gridUid.Value, tilePos))
                continue;

            return candidate;
        }

        return null;
    }
}
