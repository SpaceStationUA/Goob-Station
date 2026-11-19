// SPDX-License-Identifier: AGPL-3.0-or-later
// Attribution: AdventureTimeSS14/space_station_ADT PR #3338 (MIT).

namespace Content.Pirate.Shared.AlertLevel;

[DataDefinition]
public sealed partial class AlertLevelRequirement
{
    /// <summary>Ascending alert levels; keep this list aligned with the server-only stationAlerts prototype.</summary>
    /// <remarks>Include every stationAlerts level in prototype order; vending gates compare positions in this list.</remarks>
    [DataField]
    public List<string> Levels = new()
    {
        "green",
        "blue",
        "violet",
        "yellow",
        "orange",
        "omicron",
        "octarine",
        "honk",
        "white",
        "amber",
        "cyan",
        "red",
        "gamma",
        "delta",
        "epsilon",
    };

    /// <summary>Null means every listed level meets the requirement.</summary>
    [DataField]
    public string? Minimum;
}
