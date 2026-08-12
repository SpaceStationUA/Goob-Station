using System.Linq;
using System.Numerics;
using Content.Shared._FarHorizons.Planets;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Robust.Client.Graphics;

namespace Content.Client.Shuttles.UI;

public sealed partial class ShuttleMapControl
{
    private void DrawStarSystem(DrawingHandleScreen handle, Matrix3x2 matty)
    {
        if (!EntManager.TryGetComponent<TransformComponent>(_shuttleEntity, out var shuttleTransform) ||
            shuttleTransform.MapUid == null ||
            !EntManager.TryGetComponent<StarSystemMapComponent>(shuttleTransform.MapUid.Value, out var starSystem) ||
            starSystem.StarSystem == null)
            return;

        var starPos = Vector2.Transform(starSystem.StarSystem.Star.Position + starSystem.StarOffset, matty);
        starPos = starPos with { Y = -starPos.Y };
        starPos = ScalePosition(starPos);
        var starRadius = Star.MAP_PIXEL_SIZE * starSystem.StarSystem.Star.Radius * MinimapScale;

        handle.DrawCircle(starPos, starRadius, starSystem.StarSystem.Star.Color);
        handle.DrawString(Font, starPos + new Vector2(starRadius + 3f, -8f), starSystem.StarSystem.Star.Name, Color.White);

        // Far Horizons: the asteroid belt as a band outline around the star — inner and outer
        // boundary rings, so you can see where the belt sits on the map.
        if (starSystem.StarSystem.AsteroidBelt is { } belt)
        {
            var beltPos = Vector2.Transform(belt.Position + starSystem.StarOffset, matty);
            beltPos = beltPos with { Y = -beltPos.Y };
            beltPos = ScalePosition(beltPos);
            handle.DrawCircle(beltPos, belt.RadialSize.X * MinimapScale, Color.White.WithAlpha(0.08f), filled: false);
            handle.DrawCircle(beltPos, belt.RadialSize.Y * MinimapScale, Color.White.WithAlpha(0.08f), filled: false);
        }

        // Approach rings are drawn only around the nearest planet to the shuttle, so the zones
        // of different planets don't pile up on top of each other.
        var shuttlePos = EntManager.System<SharedTransformSystem>().GetWorldPosition(_shuttleEntity.Value);
        var nearest = starSystem.StarSystem.Planets
            .OrderBy(p => (p.Position + starSystem.StarOffset - shuttlePos).Length())
            .FirstOrDefault();

        // First pass: markers + type labels for every planet.
        foreach (var planet in starSystem.StarSystem.Planets)
        {
            var planetPos = Vector2.Transform(planet.Position + starSystem.StarOffset, matty);
            planetPos = planetPos with { Y = -planetPos.Y };
            planetPos = ScalePosition(planetPos);
            var planetRadius = Planet.MAP_PIXEL_SIZE * planet.Radius * MinimapScale;
            handle.DrawCircle(planetPos, planetRadius, Color.Gray);

            // Far Horizons: planet type label — green means you can descend onto it, gray means
            // a gas/ice giant with no surface.
            var landable = IsPlanetLandable(planet);
            var labelColor = landable ? Color.FromHex("#80C71F") : Color.Gray;
            handle.DrawString(Font, planetPos + new Vector2(planetRadius + 3f, -8f),
                $"{planet.Name} ({Loc.GetString(GetPlanetType(planet))})", labelColor);
        }

        // Second pass: the nearest planet's approach zones, drawn last so they never hide
        // another planet's marker.
        if (nearest != null)
        {
            var planetPos = Vector2.Transform(nearest.Position + starSystem.StarOffset, matty);
            planetPos = planetPos with { Y = -planetPos.Y };
            planetPos = ScalePosition(planetPos);

            var worldRadius = CEPlanetRadii.WorldRadius(nearest);
            handle.DrawCircle(planetPos, CEPlanetRadii.ApproachRadius(worldRadius) * MinimapScale, Color.White.WithAlpha(0.15f), filled: false);
            handle.DrawCircle(planetPos, CEPlanetRadii.ZoneRadius(worldRadius) * MinimapScale, Color.White.WithAlpha(0.35f), filled: false);
        }
    }

    private static bool IsPlanetLandable(Planet planet) =>
        planet.Shader is not ("GasGiant" or "IceGiant");

    private static string GetPlanetType(Planet planet) => planet.Shader switch
    {
        "GasGiant" => "ce-planet-type-gas-giant",
        "IceGiant" => "ce-planet-type-ice-giant",
        _ => "ce-planet-type-rocky",
    };
}
