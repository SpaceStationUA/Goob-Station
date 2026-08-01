// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;

namespace Content.Client.Humanoid;

public sealed partial class SingleMarkingPicker
{
    /// <summary>When true, list markings from all species in the category (still sex/player filtered).</summary>
    public bool IgnoreSpecies;

    /// <summary>Sex used to filter markings when <see cref="IgnoreSpecies"/> is set.</summary>
    public Sex Sex = Sex.Unsexed;

    private IReadOnlyDictionary<string, MarkingPrototype> ResolveCategoryMarkings(string? ckey)
    {
        return IgnoreSpecies
            ? _markingManager.MarkingsByCategoryAndSex(Category, Sex, ckey)
            : _markingManager.MarkingsByCategoryAndSpecies(Category, _species!, ckey);
    }
}
