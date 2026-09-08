// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.Objectives.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;

namespace Content.Server._Pirate.Objectives.Systems;

public sealed class TargetObjectiveImmuneSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TargetObjectiveImmuneComponent, ComponentStartup>(OnImmuneStartup);
        SubscribeLocalEvent<TargetObjectiveImmuneComponent, MindAddedMessage>(OnMindAdded);
    }

    private void OnImmuneStartup(Entity<TargetObjectiveImmuneComponent> ent, ref ComponentStartup args)
    {
        if (_mind.TryGetMind(ent.Owner, out var mindId, out _))
            EnsureComp<TargetObjectiveImmuneComponent>(mindId);
    }

    private void OnMindAdded(Entity<TargetObjectiveImmuneComponent> ent, ref MindAddedMessage args)
    {
        EnsureComp<TargetObjectiveImmuneComponent>(args.Mind.Owner);
    }
}
