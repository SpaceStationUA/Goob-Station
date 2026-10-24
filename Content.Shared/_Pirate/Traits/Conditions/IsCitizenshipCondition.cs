using System.Linq;
using Content.Shared._Pirate.Origin;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Traits.Conditions;

[DataDefinition]
public sealed partial class IsCitizenshipCondition : BaseTraitCondition
{
    [DataField("citizenships", required: true)]
    public List<ProtoId<CitizenshipPrototype>> Citizenships = new();

    protected override bool EvaluateImplementation(TraitConditionContext ctx)
    {
        if (ctx.Profile == null || string.IsNullOrEmpty(ctx.Profile.Citizenship))
            return false;

        return Citizenships.Any(n => n.Id.Equals(ctx.Profile.Citizenship, StringComparison.OrdinalIgnoreCase));
    }

    public override string GetTooltip(IPrototypeManager proto, ILocalizationManager loc)
    {
        var citizenshipNames = new List<string>();

        foreach (var citizenship in Citizenships)
        {
            if (proto.TryIndex(citizenship, out var citizenshipProto))
                citizenshipNames.Add(loc.GetString(citizenshipProto.NameKey));
            else
                citizenshipNames.Add(citizenship.Id);
        }

        var citizenshipsList = string.Join(", ", citizenshipNames);

        return Invert
            ? loc.GetString("trait-condition-citizenship-not", ("citizenships", citizenshipsList))
            : loc.GetString("trait-condition-citizenship-is", ("citizenships", citizenshipsList));
    }
}
