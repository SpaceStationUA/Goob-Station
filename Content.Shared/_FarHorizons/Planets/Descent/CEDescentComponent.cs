/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.GameStates;

namespace Content.Shared._FarHorizons.Planets.Descent;

/// <summary>
/// The stages of a planet descent sequence. Stage 2 ("WARP!") is an
/// edge, not a state: it happens in the single tick transitioning Vanishing → Arriving.
/// There is no chargeup stage — spinup theatre belongs to the shuttle console, and the
/// sequence starts already falling.
/// </summary>
public enum CEDescentStage : byte
{
    /// <summary>
    /// Stage 1, the actual descent: the ship rides its pseudo-map, sinking away from the
    /// origin plane. Riders see the world zoom out; bystanders see the grid shrink.
    /// </summary>
    Descending = 1,

    /// <summary>
    /// Stage 1.5: clouds swallow the riders' viewport; bystanders see the grid fade to
    /// nothing. Ends with the warp.
    /// </summary>
    Vanishing = 2,

    /// <summary>Stage 2.5: post-warp, over the target z-network; viewport un-obscures.</summary>
    Arriving = 3,
}

/// <summary>
/// A grid running the planet descent sequence. Lives on the (lead) grid from descent
/// start to arrival; the render-facing state is mirrored onto the pseudo-map's
/// <see cref="CEDescentMapComponent"/> once that exists.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEDescentComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public CEDescentStage Stage = CEDescentStage.Descending;

    /// <summary>Server curtime when <see cref="Stage"/> was entered.</summary>
    [ViewVariables, AutoNetworkedField]
    public TimeSpan StageStart;

    /// <summary>The planet being descended onto, if the sequence targets one.</summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Planet;

    /// <summary>
    /// Direction of travel: false = descending onto the planet, true = ascending off it
    /// (breaching orbit from the open-sky top of the planet's z-stack). The theatre is
    /// the same machine run in reverse — only the warp target differs (space map instead
    /// of the network's top level) and the client flips the zoom/swell curves.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool Ascent;

    /// <summary>The z-map network the ship is descending into.</summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Network;

    /// <summary>The pseudo-map carrying the ship during Descending/Vanishing.</summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? DescentMap;

    /// <summary>
    /// The docked set moving together, captured at descent start. Server bookkeeping only —
    /// deliberately not networked.
    /// </summary>
    [ViewVariables]
    public HashSet<EntityUid> GridSet = new();
}
