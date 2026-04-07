using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Traits.Conditions;

/// <summary>
/// Condition that passes if ANY of the child conditions pass.
/// Use this to create "must meet at least one of these requirements" checks.
/// </summary>
[DataDefinition]
public sealed partial class AnyOfCondition : BaseTraitCondition
{
    /// <summary>
    /// List of conditions to check. Passes if any condition evaluates to true.
    /// </summary>
    [DataField("conditions", required: true)]
    public List<BaseTraitCondition> Conditions = new();

    protected override bool EvaluateImplementation(TraitConditionContext ctx)
    {
        if (Invert)
        {
            throw new InvalidOperationException(
                "AnyOfCondition does not support Invert. To require none of the conditions, " +
                "invert the individual child conditions instead.");
        }

        if (Conditions.Count == 0)
            return false;

        foreach (var condition in Conditions)
        {
            if (condition.Evaluate(ctx))
                return true;
        }

        return false;
    }

    public override string GetTooltip(IPrototypeManager proto, ILocalizationManager loc)
    {
        if (Conditions.Count == 0)
            return string.Empty;

        var requirements = new List<string>();

        foreach (var condition in Conditions)
        {
            var tooltip = condition.GetTooltip(proto, loc);
            if (!string.IsNullOrEmpty(tooltip))
                requirements.Add(tooltip);
        }

        if (requirements.Count == 0)
            return string.Empty;

        var joinedRequirements = string.Join("\nâ€˘ ", requirements);

        return Loc.GetString("trait-condition-any-of", ("requirements", joinedRequirements));
    }
}


