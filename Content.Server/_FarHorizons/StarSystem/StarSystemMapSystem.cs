using System.Numerics;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Components;
using Content.Server._FarHorizons.Planets;
using Content.Server._Lavaland.Procedural.Systems;
using Content.Shared._FarHorizons.Planets;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Content.Shared.GameTicking;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._FarHorizons.StarSystem;

public sealed partial class StarSystemMapSystem : SharedStarSystemMapSystem
{

    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly CEPlanetSystem _planetSystem = default!;
    [Dependency] private readonly LavalandSystem _lavaland = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PostGameMapLoad>(OnPostMapLoad);
        SubscribeLocalEvent<RuleLoadedMapEvent>(OnRuleLoadedMap); // Far Horizons: nukie outpost adoption
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _nukieSurface = null;
    }

    /// <summary>Map/grid paths that get wrapped as the nukie planet's ground layer.</summary>
    private static readonly ResPath[] NukieMapPaths =
    {
        new("/Maps/_Goobstation/Nonstations/nukieplanet.yml"),
        new("/Maps/_Goobstation/Nonstations/nukieplanet-honkops.yml"),
    };

    /// <summary>How far out the nukie world sits at round start — a real flight, not a hop.</summary>
    private static readonly Vector2 NukieDistanceFromStation = new(0f, 3500f);

    /// <summary>
    /// Mid-round nukie worlds (an addgamerule mid-shift) park further out so the new planet
    /// doesn't pop into view near the station — it only grows on screen as you fly out to it.
    /// </summary>
    private static readonly Vector2 MidroundNukieDistanceFromStation = new(0f, 5000f);

    /// <summary>The star sits well away from the station so the sun never looms over the map origin.</summary>
    private static readonly Vector2 StarDistanceFromStation = new(4000f, 0f);

    /// <summary>The nukie outpost map loaded by its game rule this round, waiting to become a planet.</summary>
    private (MapId Map, IReadOnlyList<EntityUid> Grids)? _nukieSurface;

    private void OnRuleLoadedMap(RuleLoadedMapEvent ev)
    {
        if (ev.MapPath is not { } path || !NukieMapPaths.Contains(path))
            return;

        // Mid-round rule: the space map already exists, so wrap the fresh outpost surface as a
        // planet right now — parked far out. Round start (rules load before the space map) the
        // surface is stashed and PostGameMapLoad wraps it at the usual distance.
        if (TryGetSpaceMap(out var spaceMap))
        {
            if (_map.TryGetMap(ev.MapId, out var mapUid) && mapUid != null)
                SpawnNukiePlanet((spaceMap.Owner, spaceMap.Comp), mapUid.Value, TryGetStationGrid(spaceMap.Owner), MidroundNukieDistanceFromStation);
            return;
        }

        _nukieSurface = (ev.MapId, ev.Grids);
    }

    /// <summary>The round's star-system map (the space map with the sky), if loaded.</summary>
    private bool TryGetSpaceMap(out Entity<StarSystemMapComponent> spaceMap)
    {
        var query = EntityQueryEnumerator<StarSystemMapComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            spaceMap = (uid, comp);
            return true;
        }

        spaceMap = default;
        return false;
    }

    /// <summary>The station grid on <paramref name="mapUid"/>, if any.</summary>
    private EntityUid? TryGetStationGrid(EntityUid mapUid)
    {
        var query = EntityQueryEnumerator<BecomesStationComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid == mapUid)
                return uid;
        }

        return null;
    }

    private void OnPostMapLoad(PostGameMapLoad ev)
    {
        if (!ev.GameMap.GenerateStarSystem) return; // Far Horizons: star system only renders on space maps
        if (!_map.TryGetMap(ev.Map, out var mapUid)) return;
        var comp = EnsureComp<StarSystemMapComponent>(mapUid.Value);
        SetSeed((mapUid.Value, comp), _ticker.RoundId, false);

        // Find the station grid — the anchor for the star, lavaland and nukie placement.
        EntityUid? stationGrid = null;
        foreach (var grid in ev.Grids)
        {
            if (!HasComp<BecomesStationComponent>(grid))
                continue;

            stationGrid = grid;
            break;
        }

        // The star sits well away from the station so the sun never looms over the map origin.
        if (stationGrid is { } station && comp.StarSystem != null)
        {
            var stationPos = _transform.GetMapCoordinates(station).Position;
            comp.StarOffset = stationPos - comp.StarSystem.Star.Position - StarDistanceFromStation;
            Dirty<StarSystemMapComponent>((mapUid.Value, comp));
        }

        SpawnEntities((mapUid.Value, comp));

        // The lavaland surface generated for this game map becomes a planet in the sky:
        // sprite, approach zone, descent z-stack with the lavaland map as its ground layer.
        if (_lavaland.GetLavalandForGameMap(ev.GameMap.ID) is { } lavalandUid)
        {
            SpawnLavalandPlanet((mapUid.Value, comp), lavalandUid, stationGrid);
        }
        else if (ev.GameMap.Planets.Count > 0)
        {
            Log.Warning($"Game map {ev.GameMap.ID} configures {ev.GameMap.Planets.Count} lavaland planet(s) but none were generated (lavaland.enabled off or generation failed); skipping the lavaland planet in the star system.");
        }

        // The nukie outpost, loaded by its rule this round, becomes a distant shielded world
        // that only syndicate ships can descend onto.
        if (_nukieSurface is { } nukie &&
            _map.TryGetMap(nukie.Map, out var nukieMapUid) &&
            nukieMapUid != null)
        {
            SpawnNukiePlanet((mapUid.Value, comp), nukieMapUid.Value, stationGrid, NukieDistanceFromStation);
            _nukieSurface = null;
        }
    }

    public void SetSeed(Entity<StarSystemMapComponent> ent, int seed, bool sync = true)
    {
        ent.Comp.Seed = HashSeed(seed);
        // Assign before the conditional Dirty so a sync always carries the fresh system.
        ent.Comp.StarSystem = MakePlanetarySystem(ent);
        if (sync)
            Dirty(ent);
    }

    // Shuffle bits around to create entropy
    private static int HashSeed(int input)
    {
        var x = (uint)input;
        x ^= x >> 16;
        x *= 0x7feb352du;
        x ^= x >> 15;
        x *= 0x846ca68bu;
        x ^= x >> 16;
        
        return (int)x; 
    }

    private void SpawnEntities(Entity<StarSystemMapComponent> ent)
    {
        if (ent.Comp.StarSystem == null)
            return;

        if (_protoMan.TryIndex<EntityPrototype>(Star.STAR_ENTITY, out var starEnt))
        {
            var coords = new EntityCoordinates(ent, ent.Comp.StarSystem.Star.Position + ent.Comp.StarOffset);
            var spawned = SpawnAtPosition(starEnt.ID, coords);
            var name = Loc.GetString("space-star-warp-name", ("star", ent.Comp.StarSystem.Star.Name));
            _metadata.SetEntityName(spawned, name);
        }

        if (_protoMan.TryIndex<EntityPrototype>(Planet.PLANET_ENTITY, out var planetEnt))
        {
            var index = 0;
            foreach (var planet in ent.Comp.StarSystem.Planets)
            {
                var planetCoords = new EntityCoordinates(ent, planet.Position + ent.Comp.StarOffset);
                var spawnedPlanet = SpawnAtPosition(planetEnt.ID, planetCoords);
                var name = Loc.GetString("space-planet-warp-name", ("planet", planet.Name));
                _metadata.SetEntityName(spawnedPlanet, name);

                // Far Horizons: planets become approachable sky bodies with approach zones.
                // The client renders them via the parallax planet overlay; shuttle consoles
                // draw the approach rings around them. Descent into the planet comes later.
                // MaxScale is a fraction of the real world radius — rendering at full size
                // would make a gas giant's disc fill the whole viewport.
                var planetComp = EnsureComp<CEPlanetComponent>(spawnedPlanet);
                planetComp.ShaderMode = true;
                planetComp.PlanetIndex = index;
                var worldRadius = CEPlanetRadii.WorldRadius(planet);
                planetComp.WorldRadius = worldRadius;
                planetComp.ApproachRadius = CEPlanetRadii.ApproachRadius(worldRadius);
                planetComp.ZoneRadius = CEPlanetRadii.ZoneRadius(worldRadius);
                planetComp.MinScaleRadius = CEPlanetRadii.MinScaleRadius(worldRadius);
                planetComp.LandingRadius = CEPlanetRadii.LandingRadius(worldRadius);
                planetComp.MinScale = CEPlanetRadii.MinScale(worldRadius);
                planetComp.MaxScale = CEPlanetRadii.MaxScale(worldRadius);
                Dirty(spawnedPlanet, planetComp);

                // Far Horizons: the descendable z-stack (biome ground layer + sky layers) is
                // created lazily on first approach (CEPlanetSystem.EnsurePlanetStack) — dormant
                // planets cost no maps. Gas and ice giants get no surface and stay unlandable.
                index++;
            }
        }

        // Far Horizons: a predetermined, non-random planet in the sky alongside the procedural
        // ones — the CE author's nauvis sprite. Position is deterministic per round seed, and it
        // gets a landable surface just like the procedural planets.
        if (_protoMan.TryIndex<EntityPrototype>(CEPlanetSystem.NauvisEntProtoId, out var nauvisProto))
        {
            var star = ent.Comp.StarSystem.Star;
            var angle = ((ent.Comp.Seed ?? 0) % 360) * MathF.PI / 180f;
            var nauvisPos = star.Position + ent.Comp.StarOffset +
                            new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 3000f;

            var spawnedNauvis = SpawnAtPosition(nauvisProto.ID, new EntityCoordinates(ent, nauvisPos));

            // Sprite planet: stamp the same CEPlanetRadii-derived fields as the procedural
            // bodies so the client render and the descent system agree everywhere.
            var nauvisComp = EnsureComp<CEPlanetComponent>(spawnedNauvis);
            var nauvisWorldRadius = 10f;
            nauvisComp.WorldRadius = nauvisWorldRadius;
            nauvisComp.ApproachRadius = CEPlanetRadii.ApproachRadius(nauvisWorldRadius);
            nauvisComp.ZoneRadius = CEPlanetRadii.ZoneRadius(nauvisWorldRadius);
            nauvisComp.MinScaleRadius = CEPlanetRadii.MinScaleRadius(nauvisWorldRadius);
            nauvisComp.LandingRadius = CEPlanetRadii.LandingRadius(nauvisWorldRadius);
            nauvisComp.MinScale = CEPlanetRadii.MinScale(nauvisWorldRadius);
            nauvisComp.MaxScale = CEPlanetRadii.MaxScale(nauvisWorldRadius);
            Dirty(spawnedNauvis, nauvisComp);

            // Lazy: the z-stack is created on first approach (EnsurePlanetStack).
        }
    }

    /// <summary>How far from the station the lavaland world sits, so it's a short hop rather than a system crossing.</summary>
    private static readonly Vector2 LavalandStationOffset = new(0f, -750f);

    /// <summary>
    /// Wraps the round's lavaland surface (outpost, ruins, ores — everything the lavaland
    /// system generated for this game map) as a sprite planet in the star system, parked right
    /// next to the station so both the mining bus and a direct shuttle flight are practical.
    /// The lavaland map becomes the ground layer of its descendable z-stack.
    /// </summary>
    private void SpawnLavalandPlanet(Entity<StarSystemMapComponent> ent, EntityUid lavalandMap, EntityUid? stationGrid)
    {
        if (ent.Comp.StarSystem == null ||
            !_protoMan.TryIndex<EntityPrototype>(CEPlanetSystem.LavalandEntProtoId, out var lavalandProto))
            return;

        // The recorded lavaland surface must still exist and be unwired — a surface can only
        // live in one z-network, and a stale/duplicate entry must not crash the round.
        if (!Exists(lavalandMap))
        {
            Log.Warning($"Lavaland surface for {ToPrettyString(ent.Owner)} no longer exists; skipping the lavaland planet.");
            return;
        }

        if (HasComp<CEZLevelMapComponent>(lavalandMap))
            return;

        // Park the planet just off the station; without one (admin maps) it falls back to a
        // deterministic seed slot like the other preset worlds.
        Vector2 lavalandPos;
        if (stationGrid is { } station &&
            TryComp<TransformComponent>(station, out var stationXform) &&
            stationXform.MapUid == ent.Owner)
        {
            lavalandPos = _transform.GetMapCoordinates(station).Position + LavalandStationOffset;
        }
        else
        {
            var star = ent.Comp.StarSystem.Star;
            // 137° off from nauvis' own deterministic slot so the two preset worlds don't overlap.
            var angle = (((ent.Comp.Seed ?? 0) % 360) + 137) % 360 * MathF.PI / 180f;
            lavalandPos = star.Position + ent.Comp.StarOffset +
                          new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 3000f;
        }

        var spawned = SpawnAtPosition(lavalandProto.ID, new EntityCoordinates(ent, lavalandPos));

        // Sprite planet: stamp the same CEPlanetRadii-derived fields as the procedural bodies
        // so the client render and the descent system agree everywhere.
        var planetComp = EnsureComp<CEPlanetComponent>(spawned);
        var worldRadius = 10f;
        planetComp.WorldRadius = worldRadius;
        planetComp.ApproachRadius = CEPlanetRadii.ApproachRadius(worldRadius);
        planetComp.ZoneRadius = CEPlanetRadii.ZoneRadius(worldRadius);
        planetComp.MinScaleRadius = CEPlanetRadii.MinScaleRadius(worldRadius);
        // Descending ships scatter within this disc — around the outpost at the map origin.
        planetComp.LandingRadius = 64f;
        planetComp.MinScale = CEPlanetRadii.MinScale(worldRadius);
        planetComp.MaxScale = CEPlanetRadii.MaxScale(worldRadius);
        Dirty(spawned, planetComp);

        // Lazy: the lavaland surface becomes the ground layer when a ship first approaches.
        planetComp.GroundMap = lavalandMap;
        Dirty(spawned, planetComp);
    }

    /// <summary>
    /// Wraps the nukie outpost surface (loaded by the nukeops game rule) as a distant,
    /// red-shielded planet: deterministic position far from the station, the syndicate-only
    /// field already up, and the outpost map as the ground layer of its descendable z-stack.
    /// </summary>
    private void SpawnNukiePlanet(Entity<StarSystemMapComponent> ent, EntityUid nukieMap, EntityUid? stationGrid, Vector2 stationDistance)
    {
        if (ent.Comp.StarSystem == null ||
            !_protoMan.TryIndex<EntityPrototype>(CEPlanetSystem.NukieEntProtoId, out var nukieProto))
            return;

        if (!Exists(nukieMap))
        {
            Log.Warning($"Nukie surface for {ToPrettyString(ent.Owner)} no longer exists; skipping the nukie planet.");
            return;
        }

        if (HasComp<CEZLevelMapComponent>(nukieMap))
            return;

        // Far out from the station — a real flight for the syndies, a raid target for everyone else.
        Vector2 nukiePos;
        if (stationGrid is { } station &&
            TryComp<TransformComponent>(station, out var stationXform) &&
            stationXform.MapUid == ent.Owner)
        {
            nukiePos = _transform.GetMapCoordinates(station).Position + stationDistance;
        }
        else
        {
            var star = ent.Comp.StarSystem.Star;
            var angle = (((ent.Comp.Seed ?? 0) % 360) + 251) % 360 * MathF.PI / 180f;
            nukiePos = star.Position + ent.Comp.StarOffset +
                       new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 4000f;
        }

        var spawned = SpawnAtPosition(nukieProto.ID, new EntityCoordinates(ent, nukiePos));

        // Sprite planet: same CEPlanetRadii stamping as the other preset worlds. The shield
        // (red, syndicate-only) comes from the prototype and stays up permanently.
        var planetComp = EnsureComp<CEPlanetComponent>(spawned);
        var worldRadius = 10f;
        planetComp.WorldRadius = worldRadius;
        planetComp.ApproachRadius = CEPlanetRadii.ApproachRadius(worldRadius);
        planetComp.ZoneRadius = CEPlanetRadii.ZoneRadius(worldRadius);
        planetComp.MinScaleRadius = CEPlanetRadii.MinScaleRadius(worldRadius);
        planetComp.LandingRadius = 128f; // syndicate arrivals scatter wider around their base
        planetComp.MinScale = CEPlanetRadii.MinScale(worldRadius);
        planetComp.MaxScale = CEPlanetRadii.MaxScale(worldRadius);
        Dirty(spawned, planetComp);

        // Lazy: the nukie outpost becomes the ground layer when a ship first approaches.
        planetComp.GroundMap = nukieMap;
        Dirty(spawned, planetComp);
    }
}
