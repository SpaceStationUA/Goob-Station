using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;

namespace Content.Pirate.Shared.Psionics;

public sealed class PiratePsionicBloodstreamSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;

    public bool TryAddToBloodstream(Entity<BloodstreamComponent?> ent, Solution solution)
    {
        if (!Resolve(ent, ref ent.Comp, logMissing: false)
            || !_solutionContainer.ResolveSolution(ent.Owner, ent.Comp.BloodSolutionName, ref ent.Comp.BloodSolution))
            return false;

        return _solutionContainer.TryAddSolution(ent.Comp.BloodSolution.Value, solution);
    }
}
