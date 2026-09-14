using System.Linq;
using Content.Shared.Traits;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Traits.Conditions;

[DataDefinition]
public sealed partial class HasTraitsCondition : BaseTraitCondition
{
    [DataField("traits", required: true)]
    public List<ProtoId<TraitPrototype>> Traits = new();

    protected override bool EvaluateImplementation(TraitConditionContext ctx)
    {
        if (ctx.Profile is null || Traits.Count == 0)
            return false;

        return Traits.Any(trait => ctx.Profile.TraitPreferences.Contains(trait));
    }

    public override string GetTooltip(IPrototypeManager proto, ILocalizationManager loc)
    {
        var names = Traits.Select(trait => proto.TryIndex(trait, out var prototype)
                ? loc.GetString(prototype.Name)
                : trait.Id)
            .ToList();

        return loc.GetString(Invert ? "trait-condition-trait-not" : "trait-condition-trait-is",
            ("traits", string.Join(", ", names)));
    }
}
