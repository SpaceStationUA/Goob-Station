// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Chemistry.Components;
using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.Map;

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

    public bool CanSpill(EntityUid user, Solution source, EntityCoordinates coordinates)
    {
        var attempt = new ChemicalSpillAttemptEvent(source, coordinates);
        RaiseLocalEvent(user, ref attempt);
        return !attempt.Cancelled;
    }

    public bool CanProcess(EntityUid user, Solution source, EntityUid mixer)
    {
        var attempt = new ChemicalProcessAttemptEvent(source, mixer);
        RaiseLocalEvent(user, ref attempt);
        return !attempt.Cancelled;
    }
}

/// <summary>Raised on the user before either solution is changed.</summary>
[ByRefEvent]
public record struct ChemicalMixAttemptEvent(Solution Source, Solution Target, bool Cancelled = false);

/// <summary>Raised on the user before a solution is removed for a spill.</summary>
[ByRefEvent]
public record struct ChemicalSpillAttemptEvent(Solution Source, EntityCoordinates Coordinates, bool Cancelled = false);

/// <summary>Raised on the user before starting a machine reaction.</summary>
[ByRefEvent]
public record struct ChemicalProcessAttemptEvent(Solution Source, EntityUid Mixer, bool Cancelled = false);
