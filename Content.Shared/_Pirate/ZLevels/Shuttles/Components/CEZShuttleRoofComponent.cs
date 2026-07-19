using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.ZLevels.Shuttles.Components;

/// <summary>
/// Marks a grid as a runtime roof owned by <see cref="Shuttles.CEZShuttleRoofSystem"/>. Never authored or saved.
/// </summary>
[RegisterComponent, NetworkedComponent, UnsavedComponent]
public sealed partial class CEZShuttleRoofComponent : Component
{
    /// <summary>Shuttle root grid this roof belongs to.</summary>
    [DataField]
    public EntityUid Shuttle;

    /// <summary>Topmost shuttle grid whose tile silhouette was copied.</summary>
    [DataField]
    public EntityUid SourceGrid;
}
