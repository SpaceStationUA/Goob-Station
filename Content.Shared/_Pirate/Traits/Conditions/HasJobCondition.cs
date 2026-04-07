using System.Linq;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Traits.Conditions;

/// <summary>
/// Condition that checks if the player has a specific job.
/// Use Invert = true to check if the player does NOT have the job.
/// </summary>
[DataDefinition]
public sealed partial class HasJobCondition : BaseTraitCondition
{
    /// <summary>
    /// The job prototype IDs to check for.
    /// </summary>
    [DataField("jobs", required: true)]
    public List<ProtoId<JobPrototype>> Jobs = new();

    protected override bool EvaluateImplementation(TraitConditionContext ctx)
    {
        if (string.IsNullOrEmpty(ctx.JobId))
            return false;

        return Jobs.Any(j => j.Id.Equals(ctx.JobId, StringComparison.OrdinalIgnoreCase));
    }

    public override string GetTooltip(IPrototypeManager proto, ILocalizationManager loc)
    {
        var jobNames = new List<string>();
        var firstJobColor = "#ffffff";

        foreach (var job in Jobs)
        {
            if (proto.TryIndex(job, out var jobProto))
            {
                jobNames.Add(loc.GetString(jobProto.Name));

                if (firstJobColor == "#ffffff")
                {
                    foreach (var dept in proto.EnumeratePrototypes<DepartmentPrototype>())
                    {
                        if (dept.Roles.Contains(job))
                        {
                            firstJobColor = dept.Color.ToHex();
                            break;
                        }
                    }
                }
            }
            else
            {
                jobNames.Add(job.Id);
            }
        }

        var jobsList = string.Join(", ", jobNames);

        return Invert
            ? loc.GetString("trait-condition-job-not", ("job", jobsList), ("color", firstJobColor))
            : loc.GetString("trait-condition-job-is", ("job", jobsList), ("color", firstJobColor));
    }
}


