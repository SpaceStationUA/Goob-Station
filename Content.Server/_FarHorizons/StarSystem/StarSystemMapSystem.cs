using System.Linq;
using System.Numerics;
using Content.Server.GameTicking;
using Content.Server.Station.Components;
using Content.Server._FarHorizons.Planets;
using Content.Shared._FarHorizons.Planets;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._FarHorizons.StarSystem;

public sealed partial class StarSystemMapSystem : SharedStarSystemMapSystem
{

    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly IRobustRandom _rand = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly CEPlanetSystem _planetSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PostGameMapLoad>(OnPostMapLoad);
    }

    private void OnPostMapLoad(PostGameMapLoad ev)
    {
        if (!ev.GameMap.GenerateStarSystem) return; // Far Horizons: star system only renders on space maps
        if (!_map.TryGetMap(ev.Map, out var mapUid)) return;
        var comp = EnsureComp<StarSystemMapComponent>(mapUid.Value);
        SetSeed((mapUid.Value, comp), _ticker.RoundId, false);

        EntityUid? station = null;
        foreach (var grid in ev.Grids)
        {
            if (!HasComp<BecomesStationComponent>(grid))
                continue;
            
            station = grid;
            break;
        }

        if (station == null) return;

        var prettyPlanets = GetPrettyPlanets((mapUid.Value, comp));

        if (!prettyPlanets.Any()) return;

        var orbitPos = prettyPlanets.First().GetPointOnOrbit(_rand);
        var stationPos = _transform.GetMapCoordinates(station.Value).Position;
        var delta = stationPos - orbitPos;

        comp.StarOffset = delta;
        Dirty<StarSystemMapComponent>((mapUid.Value, comp));

        SpawnEntities((mapUid.Value, comp));
    }

    public void SetSeed(Entity<StarSystemMapComponent> ent, int seed, bool sync = true)
    {
        ent.Comp.Seed = HashSeed(seed);
        if (sync)
            Dirty(ent);
        ent.Comp.StarSystem = MakePlanetarySystem(ent);
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
                var worldRadius = CEPlanetRadii.WorldRadius(planet);
                planetComp.WorldRadius = worldRadius;
                planetComp.ApproachRadius = CEPlanetRadii.ApproachRadius(worldRadius);
                planetComp.ZoneRadius = CEPlanetRadii.ZoneRadius(worldRadius);
                planetComp.MinScaleRadius = CEPlanetRadii.MinScaleRadius(worldRadius);
                planetComp.LandingRadius = CEPlanetRadii.LandingRadius(worldRadius);
                planetComp.MinScale = CEPlanetRadii.MinScale(worldRadius);
                planetComp.MaxScale = CEPlanetRadii.MaxScale(worldRadius);
                Dirty(spawnedPlanet, planetComp);

                // Far Horizons: generate the planet's descendable surface (biome ground layer
                // + sky layers). Gas and ice giants get no surface and stay unlandable.
                var surfaceSeed = (ent.Comp.Seed ?? 0) ^ (index * 1000003);
                _planetSystem.CreatePlanetZStack(spawnedPlanet, planet, surfaceSeed);
                index++;
            }
        }

        // Far Horizons: a predetermined, non-random planet in the sky alongside the procedural
        // ones — the CE author's nauvis sprite. Position is deterministic per round seed, and it
        // gets a landable surface just like the procedural planets.
        if (_protoMan.TryIndex<EntityPrototype>("CEPlanetNauvis", out var nauvisProto))
        {
            var star = ent.Comp.StarSystem.Star;
            var angle = ((ent.Comp.Seed ?? 0) % 360) * MathF.PI / 180f;
            var nauvisPos = star.Position + ent.Comp.StarOffset +
                            new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 3000f;

            var spawnedNauvis = SpawnAtPosition(nauvisProto.ID, new EntityCoordinates(ent, nauvisPos));

            var nauvisComp = EnsureComp<CEPlanetComponent>(spawnedNauvis);
            nauvisComp.WorldRadius = 10f;
            Dirty(spawnedNauvis, nauvisComp);

            _planetSystem.CreatePlanetZStack(spawnedNauvis, (ent.Comp.Seed ?? 0) ^ 0x5BD1E995);
        }
    }
}
