namespace Content.Shared._DV.Psionics.Events;

/// <summary>
/// Raised on a psionic entity after <c>InitializePowerComponents</c> finishes the
/// base power setup (action button, PsionicComponent registration, pool merges,
/// init feedback). Specialized power systems subscribe to this for post-init work
/// that only matters when powers are added at runtime (e.g. PsionicEruption opens
/// its EUI and sets the initial annoyance timer).
/// </summary>
public sealed class PsionicPowerPostInitializedEvent;
