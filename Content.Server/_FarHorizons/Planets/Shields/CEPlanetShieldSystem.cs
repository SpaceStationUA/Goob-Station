/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._FarHorizons.Planets;
using Content.Shared._FarHorizons.Planets.Shields;

namespace Content.Server._FarHorizons.Planets.Shields;

/// <summary>
/// Owns the planet-side shield flag: the single entry point for raising and lowering a
/// planet's field. The shuttle console's descent validation and the client visuals only
/// ever read <see cref="CEPlanetShieldComponent"/>; the ground-side generator (charge,
/// spinup, beam, EMP detonation) drives it exclusively through
/// <see cref="SetShieldActive"/> so state changes stay in one place.
/// </summary>
public sealed class CEPlanetShieldSystem : EntitySystem
{
    /// <summary>
    /// Raises or lowers the shield over <paramref name="planet"/>. Returns false if the
    /// entity isn't a planet. Idempotent: re-raising an already-active shield is a no-op.
    /// </summary>
    public bool SetShieldActive(EntityUid planet, bool active)
    {
        if (!HasComp<CEPlanetComponent>(planet))
            return false;

        var shield = EnsureComp<CEPlanetShieldComponent>(planet);
        if (shield.Active == active)
            return true;

        shield.Active = active;
        Dirty(planet, shield);
        return true;
    }

    /// <summary>Whether <paramref name="planet"/> currently has an active field.</summary>
    public bool IsShielded(EntityUid planet)
    {
        return TryComp<CEPlanetShieldComponent>(planet, out var shield) && shield.Active;
    }
}
