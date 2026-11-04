// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using System.Numerics;

namespace Content.Pirate.Shared.Chimera;

public sealed class ActionGunSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActionGunComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ActionGunComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ActionGunComponent, ActionGunShootEvent>(OnShoot);
    }

    private void OnMapInit(Entity<ActionGunComponent> ent, ref MapInitEvent args)
    {
        if (string.IsNullOrEmpty(ent.Comp.Action))
            return;

        if (!_actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action))
            return;

        // Pirate: keep firing and recharge sounds attached to the owner; Gun is networked for prediction.
        ent.Comp.Gun = SpawnAttachedTo(ent.Comp.GunProto, new EntityCoordinates(ent, Vector2.Zero));
        Dirty(ent);
    }

    private void OnShutdown(Entity<ActionGunComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.ActionEntity is { } action)
        {
            _actions.RemoveAction(action);
            PredictedQueueDel(action);
            ent.Comp.ActionEntity = null;
        }

        if (ent.Comp.Gun is { } gun)
        {
            PredictedQueueDel(gun); // Trauma - predicted
            ent.Comp.Gun = null;
        }
        _actions.RemoveAction(ent.Comp.ActionEntity); // Pirate: remove the granted action with its gun.
    }

    private void OnShoot(Entity<ActionGunComponent> ent, ref ActionGunShootEvent args)
    {
        if (TryComp<GunComponent>(ent.Comp.Gun, out var gun))
            _gun.AttemptShoot(ent, (ent.Comp.Gun.Value, gun), args.Target);

        args.Handled = true; // Omu - needs to be set for the systems that call this event
    }
}
