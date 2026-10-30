// SPDX-License-Identifier: AGPL-3.0-or-later

// Pirate: explicit imports supplied by Trauma global usings.
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Shared.Humanoid;
using Content.Pirate.Shared.Genetics.Abilities;
using Content.Pirate.Shared.Genetics.Mutations;
using Content.Server.Polymorph.Systems;

namespace Content.Pirate.Server.Genetics.Abilities;

public sealed partial class PolymorphMutationSystem : EntitySystem
{
    [Dependency] private PolymorphSystem _polymorph = default!;
    [Dependency] private EntityQuery<HumanoidAppearanceComponent> _humanoidQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PolymorphMutationComponent, MutationAddedEvent>(OnMutationAdded);
        SubscribeLocalEvent<PolymorphMutationComponent, MutationRemovedEvent>(OnMutationRemoved);
    }

    private void OnMutationAdded(Entity<PolymorphMutationComponent> ent, ref MutationAddedEvent args)
    {
        // polymorph automatically moves mutations so do nothing or it would be in some kind of hell
        if (args.Automatic)
            return;

        var target = args.Target.Owner;
        if (!_humanoidQuery.TryComp(target, out var humanoid) ||
            !ent.Comp.Prototypes.TryGetValue(humanoid.Species, out var proto))
            return; // people/monkeys/kobolds only!

        if (_polymorph.PolymorphEntity(target, proto) == null)
            return;

        ent.Comp.Worked = true;
    }

    private void OnMutationRemoved(Entity<PolymorphMutationComponent> ent, ref MutationRemovedEvent args)
    {
        if (args.Automatic)
            return;

        var target = args.Target.Owner;
        if (ent.Comp.Worked)
            _polymorph.Revert(target);
        else if (_humanoidQuery.TryComp(target, out var humanoid) && ent.Comp.Reverts.TryGetValue(humanoid.Species, out var revert))
            _polymorph.PolymorphEntity(target, revert);
        else if (ent.Comp.Fallback is {} fallback)
            _polymorph.PolymorphEntity(target, fallback);
    }
}
