// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Pirate.CustomMarkings;
using Robust.Shared.Configuration;

namespace Content.Shared.Preferences;

public sealed partial class HumanoidCharacterProfile
{
    /// <summary>The custom markings this character wears, lowest first.</summary>
    [DataField]
    public List<CustomMarking> CustomMarkings { get; set; } = new();

    public HumanoidCharacterProfile WithCustomMarkings(IEnumerable<CustomMarking> markings)
    {
        return new(this) { CustomMarkings = markings.ToList() };
    }

    private bool CustomMarkingsEqual(HumanoidCharacterProfile other)
    {
        return CustomMarkings.SequenceEqual(other.CustomMarkings);
    }

    private void EnsureCustomMarkingsValid(IConfigurationManager cfg)
    {
        // Crafted network data can deliver null.
        CustomMarkings = CustomMarkingRules.Clean(CustomMarkings, cfg.GetCVar(CustomMarkingCVars.MaxWorn));
    }
}
