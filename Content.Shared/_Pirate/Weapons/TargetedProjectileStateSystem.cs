// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Weapons;

public sealed class TargetedProjectileStateSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<TargetedProjectileComponent, ComponentGetState>(OnTargetedGetState);
        SubscribeLocalEvent<TargetedProjectileComponent, ComponentHandleState>(OnTargetedHandleState);
    }

    private void OnTargetedGetState(Entity<TargetedProjectileComponent> ent, ref ComponentGetState args)
    {
        TryGetNetEntity(ent.Comp.Target, out var target);
        args.State = new TargetedProjectileState(target);
    }

    private void OnTargetedHandleState(Entity<TargetedProjectileComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is TargetedProjectileState state)
            ent.Comp.Target = EnsureEntity<TargetedProjectileComponent>(state.Target, ent.Owner);
    }
}
