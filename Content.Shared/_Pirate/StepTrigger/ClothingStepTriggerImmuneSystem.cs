// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Inventory;
using Content.Shared.StepTrigger.Components;
using Content.Shared.StepTrigger.Systems;

namespace Content.Shared._Pirate.StepTrigger;

/// <summary>
/// Applies step-trigger immunity from worn clothing to its wearer.
/// </summary>
public sealed class ClothingStepTriggerImmuneSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StepTriggerImmuneComponent, InventoryRelayedEvent<StepTriggerAttemptEvent>>(OnStepTriggerAttempt);
    }

    private void OnStepTriggerAttempt(Entity<StepTriggerImmuneComponent> ent, ref InventoryRelayedEvent<StepTriggerAttemptEvent> args)
    {
        if (TryComp<StepTriggerComponent>(args.Args.Source, out var trigger)
            && trigger.TriggerGroups?.IsValid(ent.Comp) == true)
        {
            args.Args.Cancelled = true;
        }
    }
}
