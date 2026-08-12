using Robust.Shared.Serialization;

namespace Content.Shared._FarHorizons.Planets.Descent;

/// <summary>
/// Lifecycle of a shuttle's planet descent drive, shown on the shuttle console.
/// </summary>
[Serializable, NetSerializable]
public enum CEDescentConsoleState : byte
{
    /// <summary>No descent in progress; controls are available.</summary>
    Available = 0,

    /// <summary>Spinup theatre running; the ship is committed.</summary>
    Spinup,

    /// <summary>Mid-descent/ascent; the ship is on the pseudo-map.</summary>
    Descending,

    /// <summary>Drive discharged; respooling before another attempt.</summary>
    Stunned,
}
