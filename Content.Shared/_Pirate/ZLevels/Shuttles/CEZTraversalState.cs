using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.ZLevels.Shuttles;

/// <summary>
/// Lifecycle of a shuttle z-level traversal (fly up / fly down). Mirrors the FTL phases but does
/// not involve hyperspace - the shuttle's linked decks are simply relocated to the adjacent z-map.
/// </summary>
[Serializable, NetSerializable]
public enum CEZTraversalState : byte
{
    /// <summary>No traversal in progress; controls are available.</summary>
    Available = 0,

    /// <summary>Startup cooldown before the decks are moved.</summary>
    Starting,

    /// <summary>Exit cooldown after the decks have been moved.</summary>
    Cooldown,
}
