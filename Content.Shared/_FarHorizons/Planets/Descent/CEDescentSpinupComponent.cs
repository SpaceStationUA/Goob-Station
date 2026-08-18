using Robust.Shared.GameStates;

namespace Content.Shared._FarHorizons.Planets.Descent;

/// <summary>
/// Console-driven spinup theatre before a planet descent. Lives on the (lead) grid from
/// the pilot's confirmation until <see cref="CEDescentComponent"/> takes over. Networked
/// so the console can render a progress state around the target planet.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEDescentSpinupComponent : Component
{
    /// <summary>The planet the ship will descend onto once spinup completes.</summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid Planet;

    /// <summary>Server curtime when the spinup began.</summary>
    [ViewVariables, AutoNetworkedField]
    public TimeSpan Start;

    /// <summary>Server curtime when the actual descent kicks off.</summary>
    [ViewVariables, AutoNetworkedField]
    public TimeSpan End;

    /// <summary>
    /// Grids we slapped <see cref="Content.Shared.Shuttles.Components.PreventPilotComponent"/>
    /// onto for the duration of the charge, so the shutdown unlock only removes what we
    /// added (an arrivals shuttle keeps its permanent lock). Server bookkeeping only.
    /// </summary>
    [ViewVariables]
    public HashSet<EntityUid> PilotLocked = new();
}
