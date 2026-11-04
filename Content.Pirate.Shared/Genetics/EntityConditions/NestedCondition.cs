using Content.Shared.EntityConditions;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Genetics.EntityConditions;

/// <summary>
/// Uses the conditions of an <see cref="EntityConditionPrototype"/>.
/// </summary>
public sealed partial class NestedCondition : EntityConditionBase<NestedCondition>
{
    /// <summary>
    /// The condition prototype to use.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<EntityConditionPrototype> Proto;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
        => prototype.Index(Proto).Condition.EntityConditionGuidebookText(prototype);
}

/// <summary>
/// Handles <see cref="NestedCondition"/>.
/// </summary>
public sealed partial class NestedConditionSystem : EntityConditionSystem<TransformComponent, NestedCondition>
{
    [Dependency] private SharedEntityConditionsSystem _conditions = default!;

    protected override void Condition(Entity<TransformComponent> ent, ref EntityConditionEvent<NestedCondition> args)
    {
        args.Result = _conditions.TryCondition(ent, ProtoMan.Index(args.Condition.Proto).Condition); // Trauma - pass soruceEnt
    }
}

// Pirate: the target does not yet have named entity conditions.
[Prototype]
public sealed partial class EntityConditionPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public EntityCondition Condition = default!;
}
