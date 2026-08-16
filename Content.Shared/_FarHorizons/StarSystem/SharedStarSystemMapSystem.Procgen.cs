using System.Linq;
using System.Numerics;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Content.Shared._FarHorizons.StarSystem.Prototypes;
using Robust.Shared.Random;
namespace Content.Shared._FarHorizons.StarSystem;

public abstract partial class SharedStarSystemMapSystem
{
    public const float MIN_STAR_PADDING = 500f;
    public const float BASE_ORBIT_DISTANCE = 1000f;
    public const float ORBIT_SPACING_FACTOR = 1.4f;
    public const float ORBIT_SECTOR_ARC = MathF.PI / 6f; // 30 degrees
    public const float ORBIT_JITTER_PERCENT = 0.5f;

    public PlanetarySystem? MakePlanetarySystem(Entity<StarSystemMapComponent> ent)
    {
        if (ent.Comp.Seed == null) return null;
        var rand = new System.Random(ent.Comp.Seed.Value);

        // The home system is a curated blend: the ringed Kyphrus star with its named worlds
        // (Fervidus, Merak, Asclepiu, Aerumna, Thrascias) at fixed orbits, plus a few
        // randomly generated planets for flavour. Falls back to pure procgen without the data.
        if (_protoMan.TryIndex<CuratedSystemPrototype>("SystemKyphrus", out var curated) &&
            _protoMan.TryIndex(curated.Star, out var curatedStar))
        {
            var star = new Star(curatedStar, rand, _protoMan);
            if (string.IsNullOrEmpty(star.Name))
                star.GenerateName(rand);

            var planets = new List<Planet>();
            foreach (var entry in curated.Planets)
            {
                var planetProto = _protoMan.Index(entry.Planet);
                var position = star.Position + new Vector2(MathF.Cos(entry.Angle), MathF.Sin(entry.Angle)) * entry.Distance;
                planets.Add(BuildCuratedPlanet(planetProto, rand, position));
            }

            // Flavour: a handful of random inner worlds from the star type's orbit slots.
            // Only unhabitable types — the curated Asclepiu stays the one habitable world.
            AsteroidBelt? belt = null;
            planets.AddRange(ResolvePlanets(rand, star, curatedStar, Vector2.Zero, ref belt, excludeHabitable: true));

            return new PlanetarySystem(star, planets, belt);
        }

        var stars = _protoMan.EnumeratePrototypes<StarTypePrototype>().OrderBy(p => p.ID).ToList();
        if (stars.Count == 0)
        {
            Log.Warning("No star type prototypes available, cannot generate a star system for map {0}.", ent.Owner);
            return null;
        }

        var pickedStar = rand.Pick(stars);

        var solarMass = rand.NextFloat(pickedStar.SolarMass.Min, pickedStar.SolarMass.Max);
        var fallbackStar = new Star(solarMass, pickedStar.Color, pickedStar.Shader);
        fallbackStar.GenerateName(rand);

        // Symmetric [-1, 1) so the system can shift in any direction from the map center.
        var orbitOffset = new Vector2(rand.NextFloat() * 2f - 1f, rand.NextFloat() * 2f - 1f);

        AsteroidBelt? asteroidBelt = null;

        var fallbackPlanets = ResolvePlanets(rand, fallbackStar, pickedStar, orbitOffset, ref asteroidBelt);

        return new PlanetarySystem(fallbackStar, fallbackPlanets, asteroidBelt);
    }

    /// <summary>Builds a named, fixed-value planet from a curated prototype.</summary>
    private Planet BuildCuratedPlanet(CuratedPlanetPrototype proto, System.Random rand, Vector2 position)
    {
        PlanetaryAtmosphere? atmosphere = null;
        if (proto.Atmosphere is { } atmosphereId)
            atmosphere = new PlanetaryAtmosphere(rand, _protoMan, atmosphereId);

        PlanetaryLiquid? liquid = null;
        if (proto.Liquid is { } liquidId)
            liquid = new PlanetaryLiquid(rand, _protoMan, liquidId);

        PlanetaryRings? rings = null;
        if (proto.Rings is { } ringsId)
            rings = new PlanetaryRings(rand, _protoMan, ringsId);

        var customData = new PlanetCustomValues();
        foreach (var (key, value) in proto.CustomFloats)
            customData.Floats[key] = value;

        return new Planet(
            position,
            proto.Name,
            proto.EarthMass,
            proto.Rotation,
            atmosphere,
            liquid,
            proto.Palette,
            proto.Shader,
            proto.HueShift,
            proto.SaturationShift,
            customData,
            rings,
            proto.BasePrettiness,
            proto.Landable
        );
    }

    private List<Planet> ResolvePlanets(System.Random rand, Star star, StarTypePrototype starProto, Vector2 orbitOffset, ref AsteroidBelt? belt, bool excludeHabitable = false)
    {
        var output = new List<Planet>();

        var sectorAngle = rand.NextFloat(0f, MathF.Tau);

        var systemCenterOffset = orbitOffset * star.Radius * Star.NAV_PIXEL_SIZE * 0.5f;

        var slotId = 0;
        var nameId = 0;
        foreach (var orbit in starProto.Orbits)
        {
            if (orbit.Prob < 1f && rand.NextFloat() >= orbit.Prob)
                continue;

            slotId++;
            
            // Representative planet mass for belt slot sizing. The belt slot itself
            // has no planet; the value only scales the clearance term.
            const float beltSlotEarthMass = 100f;

            if (orbit.Type == OrbitType.Belt &&
                starProto.AsteroidBelts.Any())
            {
                var currentDist = SlotDistance(slotId, star, beltSlotEarthMass);
                var prevDist = SlotDistance(slotId - 1, star, beltSlotEarthMass);
                var nextDist = SlotDistance(slotId + 1, star, beltSlotEarthMass);

                var innerHalf = (currentDist - prevDist) * 0.5f * 0.75f;
                var outerHalf = (nextDist - currentDist) * 0.5f * 0.75f;

                var innerRadius = currentDist - innerHalf;
                var outerRadius = currentDist + outerHalf;

                var radialSize = new Vector2(innerRadius, outerRadius);

                if (belt == null)
                {
                    var beltProto = _protoMan.Index(rand.Pick(starProto.AsteroidBelts));
                    var beltPalette = rand.Pick(beltProto.Palettes);
                    belt = new AsteroidBelt(systemCenterOffset, radialSize, beltProto.Shader, beltPalette);
                }
                else
                    belt.Expand(radialSize);
            }

            var planets = _protoMan.EnumeratePrototypes<PlanetTypePrototype>()
                .Where(p => p.Orbit.Contains(orbit.Type) && (!excludeHabitable || !p.Habitable))
                .OrderBy(p => p.ID)
                .ToList();

            if (!planets.Any()) continue;

            var planet = rand.Pick(planets);

            var palettes = planet.Palettes;
            if (!palettes.Any()) continue;

            var palette = rand.Pick(palettes);

            var earthMass = planet.EarthMass.RollValue(rand);
            var rotation = rand.NextFloat(0f, MathF.Tau);

            var hasAtmosphere = rand.NextFloat() < planet.AtmosphereProbability;
            PlanetaryAtmosphere? atmosphere = null;
            if (hasAtmosphere && planet.Atmospheres.Any())
            {
                var atmosProto = rand.Pick(planet.Atmospheres);
                atmosphere = new PlanetaryAtmosphere(rand, _protoMan, atmosProto);
            }

            var hasLiquid = rand.NextFloat() < planet.LiquidProbability;
            PlanetaryLiquid? liquid = null;
            if (hasLiquid && planet.Liquids.Any())
            {
                var liquidProto = rand.Pick(planet.Liquids);
                liquid = new PlanetaryLiquid(rand, _protoMan, liquidProto);
            }

            PlanetaryRings? rings = null;
            var ringRoll = rand.NextFloat();
            if (planet.Rings.Any() && planet.RingProbability > 0f && ringRoll < planet.RingProbability)
            {
                var ringType = rand.Pick(planet.Rings);
                rings = new PlanetaryRings(rand, _protoMan, ringType);
            }

            var position = ResolvePlanetPosition(rand, slotId, star, earthMass, sectorAngle, systemCenterOffset);
            
            var hueShift = rand.NextFloat();
            var saturationShift = rand.NextFloat();

            var customData = new PlanetCustomValues(rand, planet);

            output.Add(new Planet(
                position,
                star.GetPlanetName(nameId),
                earthMass, 
                rotation, 
                atmosphere, 
                liquid, 
                palette, 
                planet.Shader, 
                hueShift, 
                saturationShift, 
                customData, 
                rings,
                planet.BasePrettiness,
                planet.Landable
            ));
            
            nameId++;
        }

        return output;
    }

    private static float SlotDistance(int slotId, Star star, float mass)
    {
        var spacing = MathF.Pow(ORBIT_SPACING_FACTOR, slotId);

        var starRadiusPx = star.Radius * Star.NAV_PIXEL_SIZE;
        var planetRadiusPx = Planet.GetRadius(mass) * Planet.NAV_PIXEL_SIZE;

        var minClearance = starRadiusPx + planetRadiusPx + MIN_STAR_PADDING;

        return minClearance + (BASE_ORBIT_DISTANCE * spacing);
    }

    private static Vector2 ResolvePlanetPosition(System.Random rand, int slotId, Star star, float mass, float sectorAngle, Vector2 systemOffset)
    {
        var baseDistance = SlotDistance(slotId, star, mass);

        var planetRadiusPx = Planet.GetRadius(mass) * Planet.NAV_PIXEL_SIZE;
        var maxJitter = planetRadiusPx * ORBIT_JITTER_PERCENT;
        var distanceVariation = rand.NextFloat(-maxJitter, maxJitter);

        var starRadiusPx = star.Radius * Star.NAV_PIXEL_SIZE;
        var minSafeDistance = starRadiusPx + planetRadiusPx + MIN_STAR_PADDING;
        var actualDistance = MathF.Max(minSafeDistance, baseDistance + distanceVariation);

        var minAngle = sectorAngle - (ORBIT_SECTOR_ARC / 2f);
        var maxAngle = sectorAngle + (ORBIT_SECTOR_ARC / 2f);
        var angle = rand.NextFloat(minAngle, maxAngle);

        var x = actualDistance * MathF.Cos(angle);
        var y = actualDistance * MathF.Sin(angle);

        return new Vector2(x, y) + systemOffset;
    }
}
