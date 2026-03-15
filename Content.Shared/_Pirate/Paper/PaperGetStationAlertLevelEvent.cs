using Robust.Shared.GameObjects;

namespace Content.Shared._Pirate.Paper;

/// <summary>
/// Requests the current station alert level key (e.g. "green", "blue", "red").
/// </summary>
[ByRefEvent]
public record struct PaperGetStationAlertLevelEvent(string? AlertLevel = null);
