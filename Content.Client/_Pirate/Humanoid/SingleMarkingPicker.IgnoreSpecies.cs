// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;

namespace Content.Client.Humanoid;

public sealed partial class SingleMarkingPicker
{
    /// <summary>Lists category markings for all species.</summary>
    public bool IgnoreSpecies;

    /// <summary>Sex filter for all-species markings.</summary>
    public Sex Sex = Sex.Unsexed;

    private IReadOnlyDictionary<string, MarkingPrototype> ResolveCategoryMarkings(string? ckey)
    {
        return IgnoreSpecies
            ? _markingManager.MarkingsByCategoryAndSex(Category, Sex, ckey)
            : _markingManager.MarkingsByCategoryAndSpecies(Category, _species!, ckey);
    }
}
