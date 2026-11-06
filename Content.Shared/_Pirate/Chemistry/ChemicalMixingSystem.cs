// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Chemistry.Components;
using Content.Goobstation.Maths.FixedPoint;

namespace Content.Shared._Pirate.Chemistry;

/// <summary>Checks player restrictions before an interaction mixes two solutions.</summary>
public sealed class ChemicalMixingSystem : EntitySystem
{
    public bool CanMix(EntityUid user, Solution source, Solution target)
    {
        var attempt = new ChemicalMixAttemptEvent(source, target);
        RaiseLocalEvent(user, ref attempt);
        return !attempt.Cancelled;
    }

    public bool CanMixRecipe(EntityUid user, Dictionary<string, FixedPoint2> recipe, Solution target)
    {
        var source = new Solution();
        foreach (var (reagent, quantity) in recipe)
            source.AddReagent(reagent, quantity);

        return CanMix(user, source, target);
    }
}

/// <summary>Raised on the user before either solution is changed.</summary>
[ByRefEvent]
public record struct ChemicalMixAttemptEvent(Solution Source, Solution Target, bool Cancelled = false);
