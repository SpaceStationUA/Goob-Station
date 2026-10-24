using System.Linq;
using Content.Shared._Pirate.Employment;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Traits.Conditions;

[DataDefinition]
public sealed partial class IsEmployerCondition : BaseTraitCondition
{
    [DataField("employers", required: true)]
    public List<ProtoId<EmployerPrototype>> Employers = new();

    protected override bool EvaluateImplementation(TraitConditionContext ctx)
    {
        if (ctx.Profile == null || string.IsNullOrEmpty(ctx.Profile.Employer))
            return false;

        return Employers.Any(employer => employer.Id.Equals(ctx.Profile.Employer, StringComparison.OrdinalIgnoreCase));
    }

    public override string GetTooltip(IPrototypeManager proto, ILocalizationManager loc)
    {
        var employerNames = new List<string>();
        foreach (var employer in Employers)
        {
            if (proto.TryIndex(employer, out var employerPrototype))
                employerNames.Add(loc.GetString(employerPrototype.NameKey));
            else
                employerNames.Add(employer.Id);
        }

        var employers = string.Join(", ", employerNames);
        return Invert
            ? loc.GetString("trait-condition-employer-not", ("employers", employers))
            : loc.GetString("trait-condition-employer-is", ("employers", employers));
    }
}
