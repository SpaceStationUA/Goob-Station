using Content.Goobstation.Maths.FixedPoint;
using Content.Pirate.Server.Alchemy.Components;
using Content.Pirate.Server.EntityEffects;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Interaction;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Pirate.Server.Alchemy.Systems;

public sealed class AlchemistDirectApplicationSystem : EntitySystem
{
    private const float DirectAcidDamage = 8f;

    [Dependency] private readonly PirateEntityEffectSystem _effects = default!;
    [Dependency] private readonly ReactiveSystem _reactive = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;

    private static readonly FixedPoint2 ApplicationAmount = FixedPoint2.New(1);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AlchemistDirectApplicationComponent, AfterInteractEvent>(OnAfterInteract, after: [typeof(SolutionTransferSystem)]);
        SubscribeLocalEvent<AlchemistDirectApplicationComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnAfterInteract(Entity<AlchemistDirectApplicationComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target is not { } target)
            return;

        // Non-solution targets should still accept the splash.
        if (args.Handled &&
            (HasComp<RefillableSolutionComponent>(target) || HasComp<DrainableSolutionComponent>(target)))
            return;

        if (!TryComp<SolutionTransferComponent>(ent.Owner, out _))
            return;

        if (!_solutions.TryGetDrainableSolution(ent.Owner, out var drainable, out var solution))
            return;

        if (!TryComp<DrainableSolutionComponent>(ent.Owner, out var drainableComp))
            return;

        if (TryApplySolutionPortion(target, (ent.Owner, drainableComp), drainable.Value))
        {
            args.Handled = true;
        }
    }

    private void OnMeleeHit(Entity<AlchemistDirectApplicationComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        if (!TryComp<DrainableSolutionComponent>(ent.Owner, out var drainableComp)
            || !_solutions.TryGetDrainableSolution(ent.Owner, out var drainable, out _))
        {
            return;
        }

        TryApplySolutionPortion(args.HitEntities[0], (ent.Owner, drainableComp), drainable.Value);
    }

    private bool TryApplySolutionPortion(EntityUid target,
        Entity<DrainableSolutionComponent?> drainableComponent,
        Entity<SolutionComponent> drainableSolution)
    {
        var solution = drainableSolution.Comp.Solution;
        if (solution.Volume <= FixedPoint2.Zero)
            return false;

        if (solution.GetReagentQuantity(new ReagentId("AlchemistAcid", null)) >= ApplicationAmount &&
            _effects.TryApplyDirectAcid(target, DirectAcidDamage))
        {
            _solutions.Drain(drainableComponent, drainableSolution, ApplicationAmount);
            return true;
        }

        if (solution.GetReagentQuantity(new ReagentId("AlchemistPhilosopherStone", null)) >= ApplicationAmount &&
            _effects.TryApplyDirectPhilosopherStone(target))
        {
            _solutions.Drain(drainableComponent, drainableSolution, ApplicationAmount);
            return true;
        }

        var removedSolution = _solutions.Drain(drainableComponent, drainableSolution, ApplicationAmount);
        if (removedSolution.Volume <= FixedPoint2.Zero)
            return false;

        if (_solutions.TryGetInjectableSolution(target, out var injectable, out _))
        {
            _reactive.DoEntityReaction(target, removedSolution, ReactionMethod.Injection);
            _solutions.Inject(target, injectable.Value, removedSolution);
            return true;
        }

        _reactive.DoEntityReaction(target, removedSolution, ReactionMethod.Touch);
        return true;
    }
}
