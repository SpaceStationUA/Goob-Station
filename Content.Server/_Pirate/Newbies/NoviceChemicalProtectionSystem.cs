// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Server.Fluids.EntitySystems;
using Content.Shared._Pirate.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Fluids.Components;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Robust.Shared.Player;

namespace Content.Server._Pirate.Newbies;

/// <summary>Prevents inexperienced players from mixing water with sodium or potassium.</summary>
public sealed class NoviceChemicalProtectionSystem : EntitySystem
{
    private static readonly TimeSpan RequiredPlaytime = TimeSpan.FromHours(24);

    [Dependency] private readonly NovicePlaytimeSystem _playtime = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly PuddleSystem _puddles = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ActorComponent, ChemicalMixAttemptEvent>(OnMixAttempt);
        SubscribeLocalEvent<ActorComponent, ChemicalSpillAttemptEvent>(OnSpillAttempt);
        SubscribeLocalEvent<ActorComponent, BeforeThrowEvent>(OnBeforeThrow);
        SubscribeLocalEvent<ActorComponent, ChemicalProcessAttemptEvent>(OnProcessAttempt);
    }

    private void OnMixAttempt(Entity<ActorComponent> ent, ref ChemicalMixAttemptEvent args)
    {
        if (args.Cancelled ||
            (!HasWater(args.Source) && !HasWater(args.Target)) ||
            (!HasReactiveMetal(args.Source) && !HasReactiveMetal(args.Target)) ||
            !_playtime.IsRestricted(ent.Comp.PlayerSession, RequiredPlaytime))
            return;

        args.Cancelled = true;
        _popup.PopupEntity(Loc.GetString("novice-protection-reactive-chemical-mixing"), ent, ent);
    }

    private void OnSpillAttempt(Entity<ActorComponent> ent, ref ChemicalSpillAttemptEvent args)
    {
        if (args.Cancelled || !_playtime.IsRestricted(ent.Comp.PlayerSession, RequiredPlaytime))
            return;

        // Do not allow placing the metal first and adding water later.
        var dangerous = HasReactiveMetal(args.Source);
        if (!dangerous && HasWater(args.Source))
        {
            var coordinates = _transform.ToMapCoordinates(args.Coordinates);
            dangerous = _map.TryFindGridAt(coordinates, out var gridUid, out var grid) &&
                _puddles.TryGetPuddle(_map.GetTileRef(gridUid, grid, coordinates), out var puddle) &&
                TryComp<PuddleComponent>(puddle, out var component) &&
                _solutions.TryGetSolution(puddle, component.SolutionName, out _, out var solution) &&
                HasReactiveMetal(solution);
        }

        if (!dangerous)
            return;

        args.Cancelled = true;
        _popup.PopupEntity(Loc.GetString("novice-protection-reactive-chemical-spilling"), ent, ent);
    }

    private void OnBeforeThrow(Entity<ActorComponent> ent, ref BeforeThrowEvent args)
    {
        // Stop the throw before a glass container can break or spill on landing.
        if (args.Cancelled ||
            !_solutions.TryGetDrainableSolution(args.ItemUid, out _, out var solution) ||
            !HasReactiveMetal(solution) ||
            !_playtime.IsRestricted(ent.Comp.PlayerSession, RequiredPlaytime))
            return;

        args.Cancelled = true;
        _popup.PopupEntity(Loc.GetString("novice-protection-reactive-chemical-spilling"), ent, ent);
    }

    private void OnProcessAttempt(Entity<ActorComponent> ent, ref ChemicalProcessAttemptEvent args)
    {
        // TableSaltBreakdown produces sodium inside the same solution during electrolysis.
        if (args.Cancelled || !HasWater(args.Source) ||
            args.Source.GetTotalPrototypeQuantity("TableSalt") <= FixedPoint2.Zero ||
            !TryComp<ReactionMixerComponent>(args.Mixer, out var mixer) ||
            !mixer.ReactionTypes.Contains("Electrolysis") ||
            !_playtime.IsRestricted(ent.Comp.PlayerSession, RequiredPlaytime))
            return;

        args.Cancelled = true;
        _popup.PopupEntity(Loc.GetString("novice-protection-reactive-chemical-mixing"), ent, ent);
    }

    private static bool HasWater(Solution solution) =>
        solution.GetTotalPrototypeQuantity("Water") > FixedPoint2.Zero;

    private static bool HasReactiveMetal(Solution solution) =>
        solution.GetTotalPrototypeQuantity("Sodium", "Potassium") > FixedPoint2.Zero;
}
