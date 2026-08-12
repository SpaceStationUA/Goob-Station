using Content.Shared._FarHorizons.StarSystem.Helpers;

namespace Content.Shared._FarHorizons.Planets;

/// <summary>
/// Shared approach-zone math for planets. Used both when the server stamps a planet entity
/// with its <see cref="CEPlanetComponent"/> values and when the shuttle console draws the
/// approach rings, so the two always agree. Radii are clamped so a planet's zone stays a
/// local gameplay area instead of sprawling across the whole map.
/// </summary>
public static class CEPlanetRadii
{
    /// <summary>World-space radius of a star system planet.</summary>
    public static float WorldRadius(Planet planet) => planet.Radius * Planet.NAV_PIXEL_SIZE;

    /// <summary>
    /// Outer edge of the approach zone: the planet renders (as a cheap LOD disc far away) from
    /// anywhere inside and grows toward the zone as you fly in.
    /// </summary>
    public static float ApproachRadius(float worldRadius) => Math.Clamp(worldRadius * 6f, 192f, 800f);

    /// <summary>Inner "you are at the planet" zone — full shader detail and where descents start.</summary>
    public static float ZoneRadius(float worldRadius) => Math.Clamp(worldRadius * 1.5f, 32f, 128f);

    /// <summary>
    /// Distance at which the planet has shrunk to <see cref="CEPlanetComponent.MinScale"/> —
    /// roughly the approach radius, so the growth spans the whole approach band.
    /// </summary>
    public static float MinScaleRadius(float worldRadius) => Math.Clamp(worldRadius * 6f, 192f, 720f);

    /// <summary>
    /// Apparent size at the planet (inside its zone). Every body — small rocky, gas giant, the
    /// sun — renders at roughly the same apparent size, so a giant never dominates the view.
    /// </summary>
    public static float MaxScale(float worldRadius) => Math.Clamp(2f / worldRadius, 0.004f, 0.5f);

    /// <summary>
    /// Apparent size when far away — a small uniform ball for every body, gas giants included,
    /// so distant planets all read at the size of a regular small planet.
    /// </summary>
    public static float MinScale(float worldRadius) => Math.Clamp(0.15f / worldRadius, 0.0008f, 0.04f);

    /// <summary>Drop radius for a future descent (tiles around the surface origin).</summary>
    public static float LandingRadius(float worldRadius) => worldRadius;
}
