/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.GameStates;

namespace Content.Shared._FarHorizons.Planets.Descent;

/// <summary>
/// A descent pseudo-map: the transient, bare map that carries a ship while it descends
/// toward a planet (Stage 1–1.5 of the sequence). Deliberately OUTSIDE the z machinery —
/// no network membership — so the client renders it as a plain map with parallax.
///
/// Everything the client renderer needs lives here, on the map entity (always networked),
/// so no PVS gymnastics are required for the planet or the origin map.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEDescentMapComponent : Component
{
    /// <summary>
    /// The map the ship departed from. Bystanders on this map render the pseudo-map as a
    /// synthetic below-pass — the ship shrinking away beneath them.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? OriginMap;

    /// <summary>The lead grid riding this pseudo-map.</summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Grid;

    /// <summary>Mirror of the lead grid's <see cref="CEDescentComponent.Stage"/>.</summary>
    [ViewVariables, AutoNetworkedField]
    public CEDescentStage Stage = CEDescentStage.Descending;

    /// <summary>
    /// Mirror of the lead grid's <see cref="CEDescentComponent.Ascent"/>. On an ascent
    /// the "origin" is the top z-level the ship breached away from: bystanders there
    /// watch the hull shrink upward instead of downward.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool Ascent;

    /// <summary>Server curtime when <see cref="Stage"/> was entered.</summary>
    [ViewVariables, AutoNetworkedField]
    public TimeSpan StageStart;
}
