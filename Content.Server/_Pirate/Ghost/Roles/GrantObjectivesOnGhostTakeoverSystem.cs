// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.Ghost.Roles.Components;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Mind;

namespace Content.Server._Pirate.Ghost.Roles;

public sealed class GrantObjectivesOnGhostTakeoverSystem : EntitySystem
{
    [Dependency] private readonly MindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GrantObjectivesOnGhostTakeoverComponent, TakeGhostRoleEvent>(
            OnTakeGhostRole,
            after: [typeof(GhostRoleSystem)]);
    }

    private void OnTakeGhostRole(Entity<GrantObjectivesOnGhostTakeoverComponent> ent, ref TakeGhostRoleEvent args)
    {
        if (!args.TookRole || !_mind.TryGetMind(args.Player, out var mindId, out var mind))
            return;

        foreach (var objective in ent.Comp.Objectives)
        {
            if (_mind.TryFindObjective((mindId, mind), objective, out _))
                continue;

            _mind.TryAddObjective(mindId, mind, objective);
        }
    }
}
