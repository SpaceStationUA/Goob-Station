using System.Linq;
using Content.Shared._Pirate.Contractors.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Traits.Conditions;

[DataDefinition]
public sealed partial class IsNationalityCondition : BaseTraitCondition
{
    [DataField("nationalities", required: true)]
    public List<ProtoId<NationalityPrototype>> Nationalities = new();

    protected override bool EvaluateImplementation(TraitConditionContext ctx)
    {
        if (ctx.Profile == null || string.IsNullOrEmpty(ctx.Profile.Nationality))
            return false;

        return Nationalities.Any(n => n.Id.Equals(ctx.Profile.Nationality, StringComparison.OrdinalIgnoreCase));
    }

    public override string GetTooltip(IPrototypeManager proto, ILocalizationManager loc)
    {
        var nationalityNames = new List<string>();

        foreach (var nationality in Nationalities)
        {
            if (proto.TryIndex(nationality, out var nationalityProto))
                nationalityNames.Add(loc.GetString(nationalityProto.NameKey));
            else
                nationalityNames.Add(nationality.Id);
        }

        var nationalitiesList = string.Join(", ", nationalityNames);

        return Invert
            ? loc.GetString("trait-condition-nationality-not", ("nationalities", nationalitiesList))
            : loc.GetString("trait-condition-nationality-is", ("nationalities", nationalitiesList));
    }
}
