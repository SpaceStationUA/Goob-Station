using Content.Server._Pirate.Shuttles.Systems;
using Robust.Shared.Utility;

namespace Content.Server._Pirate.Shuttles.Components;

/// <summary>
/// Added to station entity to load the syndie jail in its centcomm map.
/// Without this syndie fultons won't work.
/// </summary>
[RegisterComponent, Access(typeof(SyndieJailSystem))]
public sealed partial class SyndieJailComponent : Component
{
    /// <summary>
    /// The grid to load.
    /// </summary>
    [DataField]
    public ResPath Path = new ResPath("/Maps/_Pirate/Nonstations/syndie_jail.yml");

    /// <summary>
    /// Minimum distance to load the grid at.
    /// </summary>
    [DataField]
    public float MinRange = 800f;

    /// <summary>
    /// Maximum distance to load the grid at.
    /// </summary>
    [DataField]
    public float MaxRange = 1000f;
}
