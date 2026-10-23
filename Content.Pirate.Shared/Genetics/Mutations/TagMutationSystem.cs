// SPDX-License-Identifier: AGPL-3.0-or-later

// Pirate: explicit imports supplied by Trauma global usings.
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Shared.Tag;

namespace Content.Pirate.Shared.Genetics.Mutations;

public sealed partial class TagMutationSystem : EntitySystem
{
    [Dependency] private TagSystem _tag = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TagMutationComponent, MutationAddedEvent>(OnAdded);
        SubscribeLocalEvent<TagMutationComponent, MutationRemovedEvent>(OnRemoved);
    }

    private void OnAdded(Entity<TagMutationComponent> ent, ref MutationAddedEvent args)
    {
        _tag.AddTags(ent, ent.Comp.Added);
        _tag.RemoveTags(ent, ent.Comp.Removed);
    }

    private void OnRemoved(Entity<TagMutationComponent> ent, ref MutationRemovedEvent args)
    {
        _tag.AddTags(ent, ent.Comp.Removed);
        _tag.RemoveTags(ent, ent.Comp.Added);
    }
}
