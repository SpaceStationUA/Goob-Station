// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.StationRecords;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Pirate.StationRecords;

public static class RecordFilters
{
    public const StationRecordFilterType Default = StationRecordFilterType.All;

    public static void Populate(OptionButton button)
    {
        button.AddItem(Label(StationRecordFilterType.All), (int) StationRecordFilterType.All);
        foreach (var type in Enum.GetValues<StationRecordFilterType>())
        {
            if (type != StationRecordFilterType.All)
                button.AddItem(Label(type), (int) type);
        }
    }

    public static string Label(StationRecordFilterType type)
    {
        return Loc.GetString($"records-filter-{type.ToString().ToLowerInvariant()}");
    }
}
