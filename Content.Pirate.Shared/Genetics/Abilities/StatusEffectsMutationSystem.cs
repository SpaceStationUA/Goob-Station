// SPDX-License-Identifier: AGPL-3.0-or-later

// Pirate: explicit imports supplied by Trauma global usings.
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Shared.StatusEffectNew;

using Content.Pirate.Shared.Genetics.Mutations;

namespace Content.Pirate.Shared.Genetics.Abilities;

public sealed partial class StatusEffectsMutationSystem : EntitySystem
{
    [Dependency] private StatusEffectsSystem _status = default!;

    [SubscribeLocalEvent]
    private void OnAdded(Entity<StatusEffectsMutationComponent> ent, ref MutationAddedEvent args)
    {
        foreach (var effect in ent.Comp.StatusEffects)
            _status.TrySetStatusEffectDuration(args.Target, effect);
    }

    [SubscribeLocalEvent]
    private void OnRemoved(Entity<StatusEffectsMutationComponent> ent, ref MutationRemovedEvent args)
    {
        foreach (var effect in ent.Comp.StatusEffects)
            _status.TryRemoveStatusEffect(args.Target, effect);
    }
}
