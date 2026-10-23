// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Shared.EntityConditions;
using Content.Shared.Standing;

namespace Content.Pirate.Shared.Genetics.EntityConditions;

/// <summary>
/// Requires that the target entity is standing and not downed/crawling.
/// Always fails for entities that don't have <see cref="StandingStateComponent"/>.
/// </summary>
public sealed partial class StandingCondition : EntityConditionBase<StandingCondition>
{
    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
        => Loc.GetString("entity-condition-guidebook-standing");
}

public sealed partial class StandingConditionSystem : EntityConditionSystem<StandingStateComponent, StandingCondition>
{
    protected override void Condition(Entity<StandingStateComponent> ent, ref EntityConditionEvent<StandingCondition> args)
    {
        args.Result = ent.Comp.Standing;
    }
}
