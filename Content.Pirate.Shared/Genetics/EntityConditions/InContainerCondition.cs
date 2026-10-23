// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Shared.EntityConditions;
using Robust.Shared.Containers;

namespace Content.Pirate.Shared.Genetics.EntityConditions;

/// <summary>
/// Requires that the target entity is inside of any container.
/// </summary>
public sealed partial class InContainerCondition : EntityConditionBase<InContainerCondition>
{
    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
        => Loc.GetString("entity-condition-guidebook-standing");
}

public sealed partial class InContainerConditionSystem : EntityConditionSystem<MetaDataComponent, InContainerCondition>
{
    [Dependency] private SharedContainerSystem _container = default!;

    protected override void Condition(Entity<MetaDataComponent> ent, ref EntityConditionEvent<InContainerCondition> args)
    {
        args.Result = _container.TryGetContainingContainer((ent, null, ent.Comp), out _);
    }
}
