using System.Linq;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Chemistry.EntitySystems;
using Robust.Shared.Analyzers;
using Robust.Shared.Timing;
using Content.Server.Body.Systems;

namespace Content.Pirate.Server.Blood;

public sealed class TastyBloodSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TastyBloodComponent, MapInitEvent>(OnInitTastyBlood);
    }

    private void OnInitTastyBlood(EntityUid uid, TastyBloodComponent component, ref MapInitEvent args)
    {
        MarkTastyBlood(uid);
    }

    private void MarkTastyBlood(EntityUid uid)
    {
        if (!TryComp<BloodstreamComponent>(uid, out var bloodstream))
            return;

        if (!_solutionContainer.ResolveSolution(uid, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var solution))
            return;

        foreach (var reagent in solution.Contents)
        {
            foreach (var dnaData in reagent.Reagent.EnsureReagentData().OfType<DnaData>())
            {
                dnaData.TastyBlood = true;
            }
        }
    }
}
