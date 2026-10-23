// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Shared.EntityConditions;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;

namespace Content.Pirate.Shared.Genetics.EntityConditions;

/// <summary>
/// Requires that the target entity is holding an item.
/// </summary>
public sealed partial class HoldingItemCondition : EntityConditionBase<HoldingItemCondition>
{
    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
        => Loc.GetString("entity-condition-guidebook-holding-item");
}

public sealed partial class HoldingItemConditionSystem : EntityConditionSystem<HandsComponent, HoldingItemCondition>
{
    [Dependency] private SharedHandsSystem _hands = default!;

    protected override void Condition(Entity<HandsComponent> ent, ref EntityConditionEvent<HoldingItemCondition> args)
    {
        foreach (var item in _hands.EnumerateHeld((ent, ent.Comp)))
        {
            args.Result = true;
            return;
        }
    }
}
