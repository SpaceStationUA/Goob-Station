// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Chemistry;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;

namespace Content.Shared.Nutrition.EntitySystems;

public sealed partial class IngestionSystem
{
    [Dependency] private readonly ChemicalMixingSystem _chemicalMixing = default!;

    private bool CanIngestChemicals(EntityUid user, EntityUid target, Solution source)
    {
        if (!_chemicalMixing.CanIngest(user, source))
            return false;

        if (!_body.TryGetBodyOrganEntityComps<StomachComponent>(target, out var stomachs))
            return false;

        // Check every stomach and its digestion destination, including chemicals swallowed earlier.
        foreach (var stomach in stomachs)
        {
            if (_solutionContainer.TryGetSolution(stomach.Owner, StomachSystem.DefaultSolutionName,
                    out _, out var contents) && !_chemicalMixing.CanMix(user, source, contents))
                return false;

            if (_solutionContainer.TryGetSolution(target, stomach.Comp1.BodySolutionName,
                    out _, out var bloodstream) && !_chemicalMixing.CanMix(user, source, bloodstream))
                return false;
        }

        return true;
    }
}
