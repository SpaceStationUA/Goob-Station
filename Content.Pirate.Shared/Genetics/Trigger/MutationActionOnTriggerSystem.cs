// SPDX-License-Identifier: AGPL-3.0-or-later

// Pirate: explicit imports supplied by Trauma global usings.
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Shared.Actions;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Systems;
using Content.Pirate.Shared.Genetics.Mutations;

namespace Content.Pirate.Shared.Genetics.Trigger;

public sealed partial class MutationActionOnTriggerSystem : XOnTriggerSystem<MutationActionOnTriggerComponent>
{
    [Dependency] private ActionMutationSystem _actionMutation = default!;
    [Dependency] private MutationSystem _mutation = default!;
    [Dependency] private SharedActionsSystem _actions = default!;

    protected override void OnTrigger(Entity<MutationActionOnTriggerComponent> ent, EntityUid target, ref TriggerEvent args)
    {
        if (_mutation.GetMutationTarget(ent.Owner) is not {} user || _actionMutation.GetAction(ent.Owner) is not {} action)
            return;

        _actions.PerformAction(user, action);
        args.Handled = true;
    }
}
