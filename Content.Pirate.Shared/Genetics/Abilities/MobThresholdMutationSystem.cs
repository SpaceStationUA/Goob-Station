// SPDX-License-Identifier: AGPL-3.0-or-later

// Pirate: explicit imports supplied by Trauma global usings.
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Pirate.Shared.Genetics.Mutations;

namespace Content.Pirate.Shared.Genetics.Abilties;

public sealed partial class MobThresholdMutationSystem : EntitySystem
{
    [Dependency] private MobThresholdSystem _threshold = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobThresholdMutationComponent, MutationAddedEvent>(OnAdded);
        SubscribeLocalEvent<MobThresholdMutationComponent, MutationRemovedEvent>(OnRemoved);
    }

    private void OnAdded(Entity<MobThresholdMutationComponent> ent, ref MutationAddedEvent args)
    {
        var target = args.Target;
        if (!TryComp<MobStateComponent>(target, out var mob))
            return;

        var states = mob.AllowedStates;
        var state = ent.Comp.Removed;
        if (!states.Contains(state))
            return;

        states.Remove(state);
        Dirty(target, mob);

        if (!TryComp<MobThresholdsComponent>(target, out var thresholds))
            return;

        var threshold = _threshold.GetThresholdForState(target, state, thresholds);
        if (threshold == FixedPoint2.Zero)
            return;

        var dict = thresholds.Thresholds;
        dict.Remove(threshold);
        Dirty(target, thresholds);

        ent.Comp.OldThreshold = threshold;
        Dirty(ent);
    }

    private void OnRemoved(Entity<MobThresholdMutationComponent> ent, ref MutationRemovedEvent args)
    {
        if (ent.Comp.OldThreshold is not {} threshold)
            return;

        ent.Comp.OldThreshold = null;
        Dirty(ent);

        var target = args.Target;
        if (!TryComp<MobStateComponent>(target, out var mob))
            return;

        var states = mob.AllowedStates;
        var state = ent.Comp.Removed;
        states.Add(state);
        Dirty(target, mob);

        if (!TryComp<MobThresholdsComponent>(target, out var thresholds))
            return;

        var dict = thresholds.Thresholds;
        dict.Add(threshold, state);
        Dirty(target, thresholds);
    }
}
