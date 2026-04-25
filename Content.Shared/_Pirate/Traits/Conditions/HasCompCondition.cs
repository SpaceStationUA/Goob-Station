using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Pirate.Traits.Conditions;

/// <summary>
/// Condition that checks if the player has a specific component.
/// Use Invert = true to check if the player does NOT have the component.
/// </summary>
[DataDefinition]
public sealed partial class HasCompCondition : BaseTraitCondition
{
    /// <summary>
    /// The component name to check for (e.g., "Pacifism").
    /// </summary>
    [DataField("component", required: true, customTypeSerializer: typeof(ComponentNameSerializer))]
    public string Component = string.Empty;

    /// <summary>
    /// The tooltip text to display, if any.
    /// </summary>
    [DataField("tooltip")]
    public LocId? Tooltip;

    protected override bool EvaluateImplementation(TraitConditionContext ctx)
    {
        if (string.IsNullOrEmpty(Component))
            return false;

        try
        {
            var compType = ctx.CompFactory.GetRegistration(Component).Type;
            return ctx.EntMan.HasComponent(ctx.Player, compType);
        }
        catch (Exception)
        {
            ctx.LogMan.GetSawmill("traits").Error($"Failed to get component registration for '{Component}'");
            return false;
        }
    }

    public override string GetTooltip(IPrototypeManager proto, ILocalizationManager loc)
    {
        if (Tooltip is not null)
            return Loc.GetString(Tooltip);

        return string.Empty;
    }
}


