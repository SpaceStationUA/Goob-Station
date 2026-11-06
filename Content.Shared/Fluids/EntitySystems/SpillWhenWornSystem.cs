// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Chemistry.EntitySystems;
using Content.Shared._Pirate.Chemistry; // Pirate
using Content.Shared.Clothing;
using Content.Shared.Fluids.Components;

namespace Content.Shared.Fluids.EntitySystems;

/// <inheritdoc cref="SpillWhenWornComponent"/>
public sealed class SpillWhenWornSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly SharedPuddleSystem _puddle = default!;
    [Dependency] private readonly ChemicalMixingSystem _chemicalMixing = default!; // Pirate

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpillWhenWornComponent, ClothingGotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<SpillWhenWornComponent, ClothingGotUnequippedEvent>(OnGotUnequipped);
        SubscribeLocalEvent<SpillWhenWornComponent, SolutionAccessAttemptEvent>(OnSolutionAccessAttempt);
    }

    private void OnGotEquipped(Entity<SpillWhenWornComponent> ent, ref ClothingGotEquippedEvent args)
    {
        if (_solutionContainer.TryGetSolution(ent.Owner, ent.Comp.Solution, out var soln, out var solution)
            && solution.Volume > 0
            && _chemicalMixing.CanSpill(args.Wearer, solution, Transform(args.Wearer).Coordinates)) // Pirate
        {
            // Spill all solution on the player
            var drainedSolution = _solutionContainer.Drain(ent.Owner, soln.Value, solution.Volume);
            _puddle.TrySplashSpillAt(ent.Owner, Transform(args.Wearer).Coordinates, drainedSolution, out _);
        }

        // Flag as worn after draining, otherwise we'll block ourself from accessing!
        ent.Comp.IsWorn = true;
        Dirty(ent);
    }

    private void OnGotUnequipped(Entity<SpillWhenWornComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        ent.Comp.IsWorn = false;
        Dirty(ent);
    }

    private void OnSolutionAccessAttempt(Entity<SpillWhenWornComponent> ent, ref SolutionAccessAttemptEvent args)
    {
        // If we're not being worn right now, we don't care
        if (!ent.Comp.IsWorn)
            return;

        // Make sure it's the right solution
        if (ent.Comp.Solution != args.SolutionName)
            return;

        args.Cancelled = true;
    }
}
