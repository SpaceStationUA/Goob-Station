// SPDX-FileCopyrightText: 2026 Pirate
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Silvia;
using Content.Shared.Alert;
using Content.Shared.Chemistry.EntitySystems;

namespace Content.Server._Pirate.Silvia;

public sealed class SilviaOmnizineSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SilviaOmnizineComponent, MapInitEvent>(OnMapInit,
            after: [typeof(SharedSolutionContainerSystem)]);
        SubscribeLocalEvent<SilviaOmnizineComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SilviaOmnizineComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SilviaOmnizineComponent, SolutionContainerChangedEvent>(OnSolutionChanged);
    }

    private void OnMapInit(Entity<SilviaOmnizineComponent> ent, ref MapInitEvent args)
    {
        UpdateAmount(ent);
    }

    private void OnStartup(Entity<SilviaOmnizineComponent> ent, ref ComponentStartup args)
    {
        UpdateAmount(ent);
    }

    private void OnShutdown(Entity<SilviaOmnizineComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(ent.Owner, ent.Comp.Alert);
    }

    private void OnSolutionChanged(Entity<SilviaOmnizineComponent> ent, ref SolutionContainerChangedEvent args)
    {
        if (args.SolutionId != ent.Comp.Solution)
            return;

        UpdateAmount(ent);
    }

    private void UpdateAmount(Entity<SilviaOmnizineComponent> ent)
    {
        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var solution))
            return;

        var amount = solution.GetTotalPrototypeQuantity(ent.Comp.Reagent);
        if (amount != ent.Comp.Amount)
        {
            ent.Comp.Amount = amount;
            Dirty(ent);
        }

        _alerts.ShowAlert(ent.Owner, ent.Comp.Alert);
    }
}
