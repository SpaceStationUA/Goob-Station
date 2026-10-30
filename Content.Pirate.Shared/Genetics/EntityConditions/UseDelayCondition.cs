// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Shared.EntityConditions;
using Content.Shared.Timing;

namespace Content.Pirate.Shared.Genetics.EntityConditions;

/// <summary>
/// Condition that requires a use delay to not be active.
/// </summary>
public sealed partial class UseDelayCondition : EntityConditionBase<UseDelayCondition>
{
    /// <summary>
    /// The specific use delay to check.
    /// </summary>
    [DataField]
    public string DelayId = UseDelaySystem.DefaultId;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
        => Loc.GetString("entity-condition-guidebook-use-delay", ("id", DelayId));
}

public sealed partial class UseDelayConditionSystem : EntityConditionSystem<UseDelayComponent, UseDelayCondition>
{
    [Dependency] private UseDelaySystem _useDelay = default!;

    protected override void Condition(Entity<UseDelayComponent> ent, ref EntityConditionEvent<UseDelayCondition> args)
    {
        var id = args.Condition.DelayId;
        args.Result = !_useDelay.IsDelayed((ent, ent.Comp), id);
    }
}
