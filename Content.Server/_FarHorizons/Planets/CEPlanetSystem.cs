/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Server.Atmos.Components; // Far Horizons: sky-layer atmosphere
using Content.Server.Atmos.EntitySystems; // Far Horizons: sky-layer atmosphere
using Content.Server.Parallax;
using Content.Server._Pirate.ZLevels.Core;
using Content.Shared._FarHorizons.Planets;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared.Atmos; // Far Horizons: sky-layer atmosphere
using Content.Shared.Parallax.Biomes;
using Content.Shared.Parallax.Biomes.Layers;
using Content.Shared.Physics;
using Content.Shared.Salvage;
using Robust.Server.GameObjects;
using Robust.Server.GameStates;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;

namespace Content.Server._FarHorizons.Planets;

/// <summary>
/// Server-side: keeps every planet globally PVS-overridden. A planet is visible across its whole
/// approach radius — including the far edge, which is nowhere near the planet's own coordinate — so
/// clients must always hold its state to render it in the background, rather than only receiving it
/// when they wander close to its actual position.
/// Also owns the runtime-generated descendable z-stack of each planet: a biome ground layer with a
/// breathable atmosphere plus empty sky layers above it.
/// </summary>
public sealed partial class CEPlanetSystem : EntitySystem
{
    [Dependency] private readonly PvsOverrideSystem _pvsOverride = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly CEZLevelsSystem _zLevels = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly ITileDefinitionManager _tiledef = default!; // Far Horizons: landing pads
    [Dependency] private readonly EntityLookupSystem _lookup = default!; // Far Horizons: landing pads
    [Dependency] private readonly AtmosphereSystem _atmos = default!; // Far Horizons: sky-layer atmosphere

    /// <summary>
    /// Number of sky layers in a planet stack, excluding the ground layer. Kept low for
    /// generated planets (procedural, nauvis, lavaland, nukie) so dormant worlds cost fewer
    /// maps; hand-mapped z-networks (e.g. station decks) define their own layouts elsewhere.
    /// The full stack height is <see cref="SkyLayerCount"/> + 1 (ground).
    /// </summary>
    public const int SkyLayerCount = 3;

    /// <summary>Depth of the clouds layer (ground is 0).</summary>
    public const int CloudsIndex = 2;

    /// <summary>The predetermined sprite planet the star system spawns alongside procedural ones.</summary>
    public const string NauvisEntProtoId = "CEPlanetNauvis";

    /// <summary>The volcanic world whose ground layer is the lavaland map (outpost, ruins, ores).</summary>
    public const string LavalandEntProtoId = "CEPlanetLavaland";

    /// <summary>The distant syndicate world whose ground layer is the nukie outpost map.</summary>
    public const string NukieEntProtoId = "CEPlanetNukie";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEPlanetComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CEPlanetComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<CEPlanetComponent> ent, ref ComponentStartup args)
    {
        _pvsOverride.AddGlobalOverride(ent.Owner);
    }

    private void OnShutdown(Entity<CEPlanetComponent> ent, ref ComponentShutdown args)
    {
        _pvsOverride.RemoveGlobalOverride(ent.Owner);
    }

    /// <summary>
    /// Resolves the space-side planet entity whose descendable z-network contains
    /// <paramref name="mapUid"/>. This is the reverse of
    /// <see cref="CEPlanetComponent.Network"/>: ground-side machinery (e.g. the shield
    /// generator) lives on a network map and needs the planet entity that owns it —
    /// planet-level state like the shield component hangs off the planet entity, never
    /// off the maps. Linear in the number of planets, which is fine: there are only
    /// ever a handful.
    /// </summary>
    public bool TryGetPlanetForMap(EntityUid mapUid, out Entity<CEPlanetComponent> planet)
    {
        planet = default;
        if (!TryComp<CEZLevelMapComponent>(mapUid, out var zMap) ||
            zMap.NetworkUid == EntityUid.Invalid)
            return false;

        var query = EntityQueryEnumerator<CEPlanetComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Network != zMap.NetworkUid)
                continue;

            planet = (uid, comp);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Generates the descendable z-stack for a star system planet: a biome ground layer
    /// (with gravity, breathable atmosphere and a restricted range) plus empty sky layers,
    /// all joined in a z-network. Gas and ice giants get no surface and stay unlandable.
    /// </summary>
    public bool CreatePlanetZStack(EntityUid planetUid, Planet starSystemPlanet, int seed)
    {
        if (!TryComp<CEPlanetComponent>(planetUid, out var comp) || comp.Network != null)
            return false;

        // Gas/ice giants have no surface to land on.
        if (!Planet.IsLandable(starSystemPlanet))
            return false;

        var biomeId = GetBiomeForPlanet(starSystemPlanet, seed);
        var landingRadius = CEPlanetRadii.LandingRadius(CEPlanetRadii.WorldRadius(starSystemPlanet));
        return BuildPlanetStack(planetUid, comp, starSystemPlanet.Name, biomeId, seed, landingRadius);
    }

    /// <summary>
    /// Generates a descendable z-stack for hand-placed (sprite) planets: a seed-derived biome
    /// surface plus the usual sky layers, so predetermined planets are landable like the
    /// procedural ones.
    /// </summary>
    public bool CreatePlanetZStack(EntityUid planetUid, int seed)
    {
        if (!TryComp<CEPlanetComponent>(planetUid, out var comp) || comp.Network != null)
            return false;

        var biomeId = (Math.Abs(seed) % 4) switch
        {
            0 => "Grasslands",
            1 => "LowDesert",
            2 => "Snow",
            _ => "Lava",
        };

        var landingRadius = MathF.Max(comp.LandingRadius, 32f);
        return BuildPlanetStack(planetUid, comp, MetaData(planetUid).EntityName, biomeId, seed, landingRadius);
    }

    private bool BuildPlanetStack(EntityUid planetUid, CEPlanetComponent comp, string displayName, string biomeId, int seed, float landingRadius)
    {
        // Ground layer (depth 0), biome-generated.
        var groundMap = _map.CreateMap(out var groundMapId, runMapInit: false);
        _meta.SetEntityName(groundMap, $"Surface: {displayName}");
        SetupPlanetSurface(groundMap, biomeId, seed, landingRadius);

        return BuildPlanetStackWithGround(planetUid, comp, displayName, groundMap);
    }

    /// <summary>
    /// Lazily creates the planet's descendable z-stack if it doesn't exist yet, so dormant
    /// worlds never pay for their ground/sky maps. Shader planets regenerate their biome
    /// surface from the star system data, sprite planets from the map's seed, and lavaland/
    /// nukie adopt their pre-built surface (<see cref="CEPlanetComponent.GroundMap"/>).
    /// </summary>
    public bool EnsurePlanetStack(EntityUid planetUid)
    {
        if (!TryComp<CEPlanetComponent>(planetUid, out var comp))
            return false;

        if (comp.Network != null)
            return true;

        if (comp.GroundMap is { } groundMap && Exists(groundMap))
            return CreatePlanetZStackWithGround(planetUid, groundMap, MetaData(planetUid).EntityName);

        var mapUid = Transform(planetUid).MapUid;
        if (mapUid == null ||
            !TryComp<StarSystemMapComponent>(mapUid, out var starSystem) ||
            starSystem.StarSystem == null)
            return false;

        var seed = starSystem.Seed ?? 0;

        if (comp.ShaderMode)
        {
            if (comp.PlanetIndex < 0 || comp.PlanetIndex >= starSystem.StarSystem.Planets.Count)
                return false;

            return CreatePlanetZStack(planetUid, starSystem.StarSystem.Planets[comp.PlanetIndex], seed ^ (comp.PlanetIndex * 1000003));
        }

        // Sprite planet: seed-derived biome surface, same derivation the eager path used.
        return CreatePlanetZStack(planetUid, seed ^ 0x5BD1E995);
    }

    /// <summary>
    /// Wraps a pre-built surface map (e.g. a lavaland planet) in the usual planet z-stack:
    /// marks it as the ground layer, adds empty sky layers above it and joins everything
    /// into one z-network. The ground map keeps whatever setup it already has (biome,
    /// atmosphere, outpost grids, weather) — only the ground marker and the sky levels
    /// are added.
    /// </summary>
    public bool CreatePlanetZStackWithGround(EntityUid planetUid, EntityUid groundMap, string displayName)
    {
        if (!TryComp<CEPlanetComponent>(planetUid, out var comp) || comp.Network != null)
            return false;

        if (!Exists(groundMap))
        {
            Log.Warning($"Cannot wrap {ToPrettyString(groundMap)} as the ground layer of {ToPrettyString(planetUid)}: it doesn't exist.");
            return false;
        }

        return BuildPlanetStackWithGround(planetUid, comp, displayName, groundMap);
    }

    private bool BuildPlanetStackWithGround(EntityUid planetUid, CEPlanetComponent comp, string displayName, EntityUid groundMap)
    {
        var network = _zLevels.CreateZNetwork();
        _meta.SetEntityName(network.Owner, $"Planet z-Network: {displayName}");

        var maps = new Dictionary<EntityUid, int> { [groundMap] = 0 };

        // This is the surface ships land on: the clouds overlay skips it, the descent
        // validation and shield machinery resolve it back to its planet.
        EnsureComp<CEZGroundLayerComponent>(groundMap);

        // Sky layers above the ground, clouds marker at CloudsIndex.
        for (var depth = 1; depth <= SkyLayerCount; depth++)
        {
            var skyMap = _map.CreateMap(out var skyMapId, runMapInit: false);
            _meta.SetEntityName(skyMap, $"Sky: {displayName} [{depth}]");
            if (depth == CloudsIndex)
                AddComp<CEZCloudLayerComponent>(skyMap);

            // Far Horizons: sky levels share the ground layer's breathable atmosphere — a
            // player stepping off a hovering shuttle over the clouds still has air. The sky
            // marker carries the ground layer's gravity for falls and the drift redirection.
            ApplySkyAtmosphere(skyMap, groundMap);
            var skyLayer = EnsureComp<CEZPlanetSkyLayerComponent>(skyMap);
            skyLayer.GroundMapUid = groundMap;
            Dirty(skyMap, skyLayer);
            maps[skyMap] = depth;
        }

        if (!_zLevels.TryAddMapsIntoZNetwork(network, maps))
        {
            Log.Error($"Failed to populate planet z-network for {ToPrettyString(planetUid)}; tearing it down.");
            QueueDel(network.Owner);
            foreach (var mapUid in maps.Keys)
                QueueDel(mapUid);
            return false;
        }

        // Init the maps now the network is linked. A pre-built ground map (lavaland) is
        // already initialized by its own setup — only the fresh sky maps need it.
        foreach (var (mapUid, _) in maps)
        {
            if (CompOrNull<MapComponent>(mapUid)?.MapInitialized == true)
                continue;

            _map.InitializeMap(mapUid);
        }

        comp.Network = network.Owner;
        Dirty(planetUid, comp);
        return true;
    }

    private void SetupPlanetSurface(EntityUid map, string biomeId, int seed, float landingRadius)
    {
        // EnsurePlanet handles the biome, a breathable atmosphere, inherent gravity and the map
        // light. The planet map itself is the surface grid, so ships land straight on its tiles.
        _biome.EnsurePlanet(map, _protoMan.Index<BiomeTemplatePrototype>(biomeId), seed, mapLight: GetMapLight(biomeId));

        // Keep the surface bounded around the landing zone.
        var restricted = EnsureComp<RestrictedRangeComponent>(map);
        restricted.Range = MathF.Max(landingRadius * 2f, 256f);

        // Ships can't fly on the ground layer.
        AddComp<CEZGroundLayerComponent>(map);
    }

    private static string GetBiomeForPlanet(Planet planet, int seed)
    {
        // Deterministic pick from the generation seed (no string hashing: GetHashCode is
        // not stable across runs). seed & 3 maps any sign to [0, 3] without Math.Abs.
        var biomeIndex = seed & 3;
        return (planet.Shader, biomeIndex) switch
        {
            ("RockyPlanet", 0) => "Grasslands",
            ("RockyPlanet", 1) => "LowDesert",
            ("RockyPlanet", 2) => "Snow",
            ("RockyPlanet", 3) => "Lava",
            _ => "Grasslands",
        };
    }

    private static Color? GetMapLight(string biomeId)
    {
        return biomeId switch
        {
            "Lava" => Color.FromHex("#FFB088"),
            "LowDesert" => Color.FromHex("#FFE8C8"),
            "Snow" => Color.FromHex("#E8F4FF"),
            _ => Color.FromHex("#C8FDFF"),
        };
    }

    /// <summary>
    /// Replaces obstacles (rocks, walls) in a disc around <paramref name="worldPos"/> on a planet
    /// surface with the biome's base fill floor — ships touch down on clear ground and players
    /// landing from the sky don't spawn inside walls. The biome never regenerates cleared tiles.
    /// </summary>
    public void ExcavateLandingPad(EntityUid mapUid, Vector2 worldPos, float radius)
    {
        if (!TryComp<MapGridComponent>(mapUid, out var mapGrid))
            return;

        var floor = GetBiomeFillFloor(mapUid);
        if (floor.IsEmpty)
            return;

        ExcavateTiles(mapUid, mapGrid, floor, Box2.CenteredAround(worldPos, new Vector2(radius, radius)));
    }

    /// <summary>Clears the footprint of <paramref name="gridUid"/> (plus a margin) down to the biome fill floor.</summary>
    public void ExcavateLandingPad(EntityUid mapUid, EntityUid gridUid, float margin = 2f)
    {
        if (!TryComp<MapGridComponent>(mapUid, out var mapGrid) ||
            !TryComp<MapGridComponent>(gridUid, out var deckGrid))
            return;

        var floor = GetBiomeFillFloor(mapUid);
        if (floor.IsEmpty)
            return;

        var bounds = Transform(gridUid).WorldMatrix.TransformBox(deckGrid.LocalAABB).Enlarged(margin);
        ExcavateTiles(mapUid, mapGrid, floor, bounds);
    }

    private void ExcavateTiles(EntityUid mapUid, MapGridComponent mapGrid, Tile floor, Box2 bounds)
    {
        var tiles = new List<(Vector2i GridIndices, Tile Tile)>();
        foreach (var tileRef in _map.GetLocalTilesIntersecting(mapUid, mapGrid, bounds, false))
        {
            if (tileRef.Tile.IsEmpty || tileRef.Tile.TypeId == floor.TypeId)
                continue;

            tiles.Add((tileRef.GridIndices, floor));
        }

        if (tiles.Count > 0)
            _map.SetTiles(mapUid, mapGrid, tiles);

        // Rocks and wall decor are anchored entities, not tiles — remove them too, otherwise
        // they stay standing inside a landed ship's hull or under a player's touchdown spot.
        var toDelete = new List<EntityUid>();
        foreach (var ent in _lookup.GetEntitiesIntersecting(Transform(mapUid).MapID, bounds, LookupFlags.Static))
        {
            // Only terrain decor anchored to the surface grid itself — never parts of the
            // landing ship (its own grid) or of a building standing on the surface (outpost).
            if (Transform(ent).GridUid != mapUid ||
                !TryComp<FixturesComponent>(ent, out var fixtures))
                continue;

            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (!fixture.Hard ||
                    (fixture.CollisionLayer & (int) (CollisionGroup.Impassable | CollisionGroup.HighImpassable)) == 0)
                    continue;

                toDelete.Add(ent);
                break;
            }
        }

        foreach (var ent in toDelete)
            QueueDel(ent);
    }

    /// <summary>The biome's lowest-threshold tile layer — the ground fill a cleared pad should become.</summary>
    private Tile GetBiomeFillFloor(EntityUid mapUid)
    {
        if (!TryComp<BiomeComponent>(mapUid, out var biome))
            return Tile.Empty;

        var floor = Tile.Empty;
        var floorThreshold = float.MaxValue;
        foreach (var layer in biome.Layers)
        {
            if (layer is not BiomeTileLayer tileLayer || tileLayer.Threshold >= floorThreshold)
                continue;

            floorThreshold = tileLayer.Threshold;
            floor = new Tile(_tiledef[tileLayer.Tile].TileId);
        }

        return floor;
    }

    /// <summary>
    /// Far Horizons: gives a sky layer the same breathable atmosphere as the ground layer below it
    /// (falling back to standard air for maps without one), so planet z-stacks are breathable
    /// top to bottom even when the sky maps are created lazily.
    /// </summary>
    private void ApplySkyAtmosphere(EntityUid skyMap, EntityUid groundMap)
    {
        GasMixture mixture;
        if (TryComp<MapAtmosphereComponent>(groundMap, out var groundAtmos))
        {
            mixture = new GasMixture(groundAtmos.Mixture);
        }
        else
        {
            var moles = new float[Atmospherics.AdjustedNumberOfGases];
            moles[(int) Gas.Oxygen] = 21.824779f;
            moles[(int) Gas.Nitrogen] = 82.10312f;
            mixture = new GasMixture(moles, Atmospherics.T20C);
        }

        _atmos.SetMapAtmosphere(skyMap, false, mixture);
    }
}
