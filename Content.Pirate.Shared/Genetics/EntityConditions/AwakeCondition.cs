// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Bed.Sleep;
using Content.Shared.EntityConditions;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Genetics.EntityConditions;

/// <summary>
/// Pirate: equivalent of Trauma's AwakeMob marker using our existing mob state and sleep systems.
/// </summary>
public sealed partial class AwakeCondition : EntityConditionBase<AwakeCondition>
{
    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
        => Loc.GetString("entity-condition-guidebook-is-awake");
}

public sealed partial class AwakeConditionSystem : EntityConditionSystem<MobStateComponent, AwakeCondition>
{
    protected override void Condition(Entity<MobStateComponent> ent, ref EntityConditionEvent<AwakeCondition> args)
    {
        args.Result = ent.Comp.CurrentState == MobState.Alive && !HasComp<SleepingComponent>(ent);
    }
}
