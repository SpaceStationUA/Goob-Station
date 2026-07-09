using JetBrains.Annotations;

namespace Content.Pirate.Shared.TerrorSpider.Evolving.Conditions;

[ImplicitDataDefinitionForInheritors]
[MeansImplicitUse]
public abstract partial class EvolvingCondition
{
    public abstract EvolveType Type { get; }

    public abstract bool Condition(EvolvingConditionArgs args);

    public abstract int GetTarget();
}

public readonly record struct EvolvingConditionArgs(EntityUid Owner, EntityUid? TargetEventsEntity, IEntityManager EntityManager);
