using System.Numerics;
using Content.Shared._FarHorizons.Planets;
using Content.Shared._FarHorizons.Planets.Shields;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Content.Shared.Timing;
using Robust.Client.Graphics;

namespace Content.Client.Shuttles.UI;

public sealed partial class ShuttleMapControl
{
    /// <summary>A world-space CE planet (sprite/shader body) the map draws and can target for descent.</summary>
    private readonly record struct CEPlanetEntry(NetEntity Net, Vector2 WorldPos, float ZoneRadius, bool Shielded);

    // Rebuilt every draw, reused by the mouse handlers for hit-testing.
    private readonly List<CEPlanetEntry> _cePlanets = new();

    private NetEntity _hoveredPlanet;
    private NetEntity _lastClickedPlanet;
    private NetEntity _denyPlanet;
    private NetEntity _descentPlanet;
    private StartEndTime _descentTime;
    private string? _denyReason;
    private TimeSpan _denyUntil;

    /// <summary>Raised when the pilot clicks a planet's zone circle on the map — starts a descent.</summary>
    public event Action<NetEntity>? RequestPlanetDescend; // Far Horizons

    /// <summary>Far Horizons: pushes the console's descent state so the map can animate the charge and refusals.</summary>
    public void SetDescentState(NetEntity planet, StartEndTime time, string? denyReason, TimeSpan denyUntil)
    {
        _descentPlanet = planet;
        _descentTime = time;
        _denyReason = denyReason;
        _denyUntil = denyUntil;

        // The server refuses descents without echoing which planet — anchor the feedback to
        // the zone the pilot actually clicked.
        if (denyReason != null && _denyPlanet != _lastClickedPlanet)
            _denyPlanet = _lastClickedPlanet;
    }

    /// <summary>Far Horizons: tracks which planet's zone the cursor is over (called from MouseMove).</summary>
    public void UpdatePlanetHover(Vector2 pixelPos)
    {
        _hoveredPlanet = default;
        var mapPos = InverseMapPosition(pixelPos);

        foreach (var planet in _cePlanets)
        {
            if ((mapPos - planet.WorldPos).LengthSquared() > planet.ZoneRadius * planet.ZoneRadius)
                continue;

            _hoveredPlanet = planet.Net;
            return;
        }
    }

    /// <summary>Far Horizons: a click on a planet's zone circle starts a descent (called from KeyBindUp).</summary>
    public bool HandlePlanetZoneClick(Vector2 pixelPos)
    {
        UpdatePlanetHover(pixelPos);
        if (_hoveredPlanet == default)
            return false;

        _lastClickedPlanet = _hoveredPlanet;
        RequestPlanetDescend?.Invoke(_hoveredPlanet);
        return true;
    }

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
        // of different planets don't pile up on top of each other. Single pass, squared distances.
        var shuttlePos = _xformSystem.GetWorldPosition(_shuttleEntity.Value);
        Planet? nearest = null;
        var nearestDistSq = float.MaxValue;
        foreach (var candidate in starSystem.StarSystem.Planets)
        {
            var distSq = (candidate.Position + starSystem.StarOffset - shuttlePos).LengthSquared();
            if (distSq >= nearestDistSq)
                continue;

            nearestDistSq = distSq;
            nearest = candidate;
        }

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

        // Far Horizons: the preset sprite worlds (lavaland) are CE planet entities, not
        // star-system data — draw them with clickable zone circles, shield rings and the
        // descent charge animation.
        DrawCePlanets(handle, matty, shuttleTransform.MapUid.Value);
    }

    private void DrawCePlanets(DrawingHandleScreen handle, Matrix3x2 matty, EntityUid mapUid)
    {
        var now = _timing.CurTime;

        _cePlanets.Clear();
        var query = EntManager.EntityQueryEnumerator<CEPlanetComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var planet, out var xform))
        {
            if (xform.MapUid != mapUid)
                continue;

            // Secret worlds stay off the long-range map — no marker, no circle, no spoilers.
            if (planet.HideFromMaps)
                continue;

            var shielded = EntManager.TryGetComponent<CEPlanetShieldComponent>(uid, out var shield) && shield.Active;
            var net = EntManager.GetNetEntity(uid);
            var worldPos = _xformSystem.GetWorldPosition(xform);
            _cePlanets.Add(new CEPlanetEntry(net, worldPos, planet.ZoneRadius, shielded));
            var screenPos = ScalePosition(ToMapScreen(Vector2.Transform(worldPos, matty)));

            // The grey zone circle: where descents start and what the pilot clicks. Highlighted
            // on hover, red while a descent was just refused.
            var zoneRadius = planet.ZoneRadius * MinimapScale;
            var denied = _denyReason != null && now < _denyUntil && _denyPlanet == net;
            var hovered = _hoveredPlanet == net;
            var zoneColor = denied
                ? Color.FromHex("#F9301C")
                : hovered ? Color.White.WithAlpha(0.7f) : Color.White.WithAlpha(0.35f);

            handle.DrawCircle(screenPos, zoneRadius, zoneColor.WithAlpha(zoneColor.A * 0.12f));
            handle.DrawCircle(screenPos, zoneRadius, zoneColor, filled: false);
            if (hovered)
                handle.DrawCircle(screenPos, zoneRadius + 2f, Color.White.WithAlpha(0.5f), filled: false);

            // Active shield: an outline around the zone, tinted per-planet (red for the
            // nukie field, default blue elsewhere).
            if (shielded)
                handle.DrawCircle(screenPos, zoneRadius + 3f, shield!.ShieldColor.WithAlpha(0.8f), filled: false);

            // Marker + name. Larger than a plain data planet so the preset worlds read properly.
            var markerRadius = MathF.Max(planet.WorldRadius * MinimapScale * 3.6f, 4.8f);
            handle.DrawCircle(screenPos, markerRadius, Color.Gray);
            handle.DrawString(Font, screenPos + new Vector2(markerRadius + 3f, -8f),
                EntManager.GetComponent<MetaDataComponent>(uid).EntityName, Color.White);

            if (denied && _denyReason != null)
                handle.DrawString(Font, screenPos + new Vector2(0f, zoneRadius + 16f), Loc.GetString(_denyReason), Color.FromHex("#F9301C"));

            // Descent charge: a blue ring sweeping around the zone circle while the drive
            // spins up or falls.
            if (_descentPlanet == net && _descentTime.Start != _descentTime.End)
            {
                var progress = _descentTime.ProgressAt(now);
                if (float.IsFinite(progress))
                    DrawChargeRing(handle, screenPos, zoneRadius + 4f, Math.Clamp(progress, 0f, 1f));
            }
        }
    }

    /// <summary>Reused vertices for the charge ring (no per-frame allocation on the draw path).</summary>
    private readonly List<Vector2> _chargeRingVerts = new(66);

    /// <summary>
    /// Draws a thin ring segment sweeping clockwise from 12 o'clock over
    /// <paramref name="progress"/> of the circle — the descent drive's charge indicator.
    /// </summary>
    private void DrawChargeRing(DrawingHandleScreen handle, Vector2 center, float radius, float progress)
    {
        const int segments = 32;
        const float band = 3f;
        var sweep = progress * MathF.Tau;

        _chargeRingVerts.Clear();
        for (var i = 0; i <= segments; i++)
        {
            var angle = -MathF.PI / 2f + sweep * i / segments;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            _chargeRingVerts.Add(center + dir * (radius - band));
            _chargeRingVerts.Add(center + dir * (radius + band));
        }

        // Descent drive blue, matching the descending status colour on the console.
        var color = Color.FromHex("#169C9C");
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, _chargeRingVerts, color.WithAlpha(0.9f));
    }

    private static Vector2 ToMapScreen(Vector2 worldPos) => worldPos with { Y = -worldPos.Y };

    private static bool IsPlanetLandable(Planet planet) => Planet.IsLandable(planet);

    private static string GetPlanetType(Planet planet) => planet.Shader switch
    {
        "GasGiant" => "ce-planet-type-gas-giant",
        "IceGiant" => "ce-planet-type-ice-giant",
        _ => "ce-planet-type-rocky",
    };
}
