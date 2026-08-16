/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.GameStates;

namespace Content.Shared._FarHorizons.Planets;

/// <summary>
/// Far Horizons: marks a planet z-stack's sky layer and points at the stack's ground layer, so
/// shared movement logic can apply the ground's gravity (falls work even over empty void) and the
/// server drift can redirect landings away from buildings on the surface.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEZPlanetSkyLayerComponent : Component
{
    /// <summary>The planet z-stack's ground layer map (depth 0).</summary>
    [DataField, AutoNetworkedField]
    public EntityUid GroundMapUid = EntityUid.Invalid;
}
