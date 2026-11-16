// SPDX-License-Identifier: AGPL-3.0-or-later
// Attribution: AdventureTimeSS14/space_station_ADT PR #3338 (MIT).

namespace Content.Pirate.Shared.AlertLevel;

[DataDefinition]
public sealed partial class AlertLevelRequirement
{
    /// <summary>Ascending alert levels; keep this list aligned with the server-only stationAlerts prototype.</summary>
    [DataField]
    public List<string> Levels = new()
    {
        "green",
        "blue",
        "violet",
        "yellow",
        "red",
        "gamma",
        "delta",
        "epsilon",
    };

    /// <summary>Null means every listed level meets the requirement.</summary>
    [DataField]
    public string? Minimum;
}
