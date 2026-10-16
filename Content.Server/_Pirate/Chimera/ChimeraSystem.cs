// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Mind.Components;
using Content.Server.Roles;
using Robust.Shared.Prototypes;
using Content.Shared.Mobs;
using Content.Server.Mind;

namespace Content.Server._Pirate.Chimera;

public sealed class ChimeraSystem : EntitySystem
{
    [Dependency] private readonly RoleSystem _role = default!;
    [Dependency] private readonly MindSystem _mind = default!;

    private static readonly EntProtoId ChimeraMindRole = "MindRoleChimera";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChimeraComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<ChimeraComponent, MindRemovedMessage>(OnMindRemoved);

        SubscribeLocalEvent<ChimeraComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMindAdded(Entity<ChimeraComponent> ent, ref MindAddedMessage args)
    {
        if (!_role.MindHasRole<MindRoleChimeraComponent>(args.Mind))
            _role.MindAddRole(args.Mind, ChimeraMindRole, mind: args.Mind.Comp);
    }

    private void OnMindRemoved(Entity<ChimeraComponent> ent, ref MindRemovedMessage args)
    {
        _role.MindRemoveRole<MindRoleChimeraComponent>((args.Mind.Owner, args.Mind.Comp));
    }

    private void OnMobStateChanged(Entity<ChimeraComponent> ent, ref MobStateChangedEvent args)
    {
        if (!_mind.TryGetMind(ent, out var mindId, out var mind))
            return;

        // Pirate: role APIs take the mind entity rather than its current body.
        if (args.NewMobState != MobState.Dead && !_role.MindHasRole<MindRoleChimeraComponent>(mindId))
        {
            _role.MindAddRole(mindId, ChimeraMindRole, mind: mind);
            return;
        }

        if (args.NewMobState != MobState.Alive && _role.MindHasRole<MindRoleChimeraComponent>(mindId))
        {
            _role.MindRemoveRole<MindRoleChimeraComponent>((mindId, mind));
        }
    }
}
