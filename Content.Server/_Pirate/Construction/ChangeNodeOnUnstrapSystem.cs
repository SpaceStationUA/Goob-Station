// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Construction;
using Content.Shared._Pirate.Buckle;
using Content.Shared.Buckle.Components;

namespace Content.Server._Pirate.Construction;

public sealed class ChangeNodeOnUnstrapSystem : EntitySystem
{
    [Dependency] private readonly ConstructionSystem _construction = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChangeNodeOnUnstrapComponent, UnstrappedEvent>(OnUnstrapped);
    }

    private void OnUnstrapped(Entity<ChangeNodeOnUnstrapComponent> ent, ref UnstrappedEvent args)
    {
        _construction.ChangeNode(ent, null, ent.Comp.Node);
    }
}
