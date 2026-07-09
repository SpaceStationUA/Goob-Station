using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Whitelist;
using Robust.Shared.Physics.Events;

namespace Content.Pirate.Shared.TerrorSpider.EntitySystems;

public sealed class SharedOnCollideSystem : EntitySystem
{
    [Dependency] private readonly ReactiveSystem _reactive = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InjectOnCollideComponent, StartCollideEvent>(OnCollide);
    }

    private void OnCollide(Entity<InjectOnCollideComponent> ent, ref StartCollideEvent args)
    {
        var target = args.OtherEntity;
        if (!_whitelist.CheckBoth(target, ent.Comp.Blacklist, ent.Comp.Whitelist)
            || !_solutions.TryGetInjectableSolution(target, out var targetSolution, out _))
        {
            return;
        }

        var solution = new Solution(ent.Comp.Reagents);

        foreach (var reagent in ent.Comp.Reagents)
        {
            if (ent.Comp.ReagentLimit != null
                && _solutions.GetTotalPrototypeQuantity(target, reagent.Reagent.ToString()) >= FixedPoint2.New(ent.Comp.ReagentLimit.Value))
            {
                return;
            }
        }

        _reactive.DoEntityReaction(target, solution, ReactionMethod.Injection);
        _solutions.TryAddSolution(targetSolution.Value, solution);
    }
}
