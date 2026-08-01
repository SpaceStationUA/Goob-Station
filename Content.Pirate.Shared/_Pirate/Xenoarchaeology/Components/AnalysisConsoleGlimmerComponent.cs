using Content.Shared._Pirate.Xenoarchaeology.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Xenoarchaeology.Components;

/// <summary>
/// Pirate - Component for handling multipliers and Glimmer production when players generate
/// research via an analysis console.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedGlimmerResearchSystem))]
public sealed partial class AnalysisConsoleGlimmerComponent : Component
{
    /// <summary>
    /// How much research is required to generate a single point of Glimmer.
    /// </summary>
    [DataField]
    public float ResearchPerGlimmer = 750;
}
