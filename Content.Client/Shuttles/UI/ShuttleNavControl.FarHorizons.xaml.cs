using System.Linq;
using System.Numerics;
using Content.Shared._FarHorizons.Planets;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Robust.Client.Graphics;

namespace Content.Client.Shuttles.UI;

public sealed partial class ShuttleNavControl
{
    private void DrawStarSystem(DrawingHandleScreen handle, Matrix3x2 worldToShuttle, Matrix3x2 shuttleToView, EntityUid? mapUid)
    {
        if (!EntManager.TryGetComponent<StarSystemMapComponent>(mapUid, out var starSystem) ||
            starSystem.StarSystem == null)
            return;

        var worldToView = worldToShuttle * shuttleToView;
        var viewScale = MathF.Sqrt((worldToView.M11 * worldToView.M11) + (worldToView.M12 * worldToView.M12));

        var starPos = Vector2.Transform(starSystem.StarSystem.Star.Position + starSystem.StarOffset, worldToView);
        var starRadius = Star.NAV_PIXEL_SIZE * starSystem.StarSystem.Star.Radius * viewScale;

        handle.DrawCircle(starPos, starRadius, starSystem.StarSystem.Star.Color.WithAlpha(0.5f));
        handle.DrawString(Font, starPos + new Vector2(starRadius + 3f, -8f), starSystem.StarSystem.Star.Name, Color.White);

        // Far Horizons: the asteroid belt as a band outline around the star.
        if (starSystem.StarSystem.AsteroidBelt is { } belt)
        {
            var beltPos = Vector2.Transform(belt.Position + starSystem.StarOffset, worldToView);
            handle.DrawCircle(beltPos, belt.RadialSize.X * viewScale, Color.White.WithAlpha(0.06f), filled: false);
            handle.DrawCircle(beltPos, belt.RadialSize.Y * viewScale, Color.White.WithAlpha(0.06f), filled: false);
        }

        // Approach rings are drawn only around the nearest planet to the shuttle, so the zones
        // of different planets don't pile up on top of each other.
        var shuttlePos = EntManager.System<SharedTransformSystem>().GetWorldPosition(_coordinates!.Value.EntityId);
        var nearest = starSystem.StarSystem.Planets
            .OrderBy(p => (p.Position + starSystem.StarOffset - shuttlePos).Length())
            .FirstOrDefault();

        // First pass: markers + type labels for every planet.
        foreach (var planet in starSystem.StarSystem.Planets)
        {
            var planetPos = Vector2.Transform(planet.Position + starSystem.StarOffset, worldToView);
            var planetRadius = Planet.NAV_PIXEL_SIZE * planet.Radius * viewScale;
            handle.DrawCircle(planetPos, planetRadius, Color.Gray.WithAlpha(0.5f));

            // Far Horizons: planet type label — green means you can descend onto it, gray means
            // a gas/ice giant with no surface.
            var landable = IsPlanetLandable(planet);
            var labelColor = landable ? Color.FromHex("#80C71F").WithAlpha(0.9f) : Color.Gray;
            handle.DrawString(Font, planetPos + new Vector2(planetRadius + 3f, -8f),
                $"{planet.Name} ({Loc.GetString(GetPlanetType(planet))})", labelColor);
        }

        // Second pass: the nearest planet's approach zones, drawn last so they never hide
        // another planet's marker.
        if (nearest != null)
        {
            var planetPos = Vector2.Transform(nearest.Position + starSystem.StarOffset, worldToView);

            var worldRadius = CEPlanetRadii.WorldRadius(nearest);
            handle.DrawCircle(planetPos, CEPlanetRadii.ApproachRadius(worldRadius) * viewScale, Color.White.WithAlpha(0.1f), filled: false);
            handle.DrawCircle(planetPos, CEPlanetRadii.ZoneRadius(worldRadius) * viewScale, Color.White.WithAlpha(0.25f), filled: false);
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
