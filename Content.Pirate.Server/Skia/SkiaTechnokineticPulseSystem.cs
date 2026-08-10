// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Emp;
using Content.Pirate.Shared.Skia;
using Content.Shared.Actions;

namespace Content.Pirate.Server.Skia;

public sealed class SkiaTechnokineticPulseSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EmpSystem _emp = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SkiaTechnokineticPulseComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SkiaTechnokineticPulseComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SkiaTechnokineticPulseComponent, SkiaTechnokineticPulseActionEvent>(OnPulse);
    }

    private void OnMapInit(Entity<SkiaTechnokineticPulseComponent> entity, ref MapInitEvent args)
    {
        _actions.AddAction(entity, ref entity.Comp.ActionEntity, entity.Comp.ActionId);
        _actions.StartUseDelay(entity.Comp.ActionEntity);
    }

    private void OnShutdown(Entity<SkiaTechnokineticPulseComponent> entity, ref ComponentShutdown args)
    {
        _actions.RemoveAction(entity.Owner, entity.Comp.ActionEntity);
    }

    private void OnPulse(Entity<SkiaTechnokineticPulseComponent> entity, ref SkiaTechnokineticPulseActionEvent args)
    {
        if (args.Handled)
            return;

        _emp.EmpPulse(
            _transform.GetMapCoordinates(entity),
            entity.Comp.Range,
            entity.Comp.EnergyConsumption,
            entity.Comp.DisableDuration);
        args.Handled = true;
    }
}
