// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Wraith.Components;
using Content.Shared.Actions.Components;
using Robust.Shared.Player;

namespace Content.Pirate.Server.Wraith;

/// <summary>
/// Resynchronizes Wraith actions when a player takes over an existing Wraith entity.
/// </summary>
public sealed class WraithActionResyncSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WraithComponent, PlayerAttachedEvent>(OnPlayerAttached);
    }

    private void OnPlayerAttached(Entity<WraithComponent> ent, ref PlayerAttachedEvent args)
    {
        if (!TryComp<ActionsComponent>(ent, out var actions))
            return;

        Dirty(ent.Owner, actions);

        foreach (var actionUid in actions.Actions)
        {
            if (TryComp<ActionComponent>(actionUid, out var action))
                Dirty(actionUid, action);
        }
    }
}
