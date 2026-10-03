using System.Linq;
using Content.Shared._Pirate.Employment;
using Robust.Shared.Prototypes;

namespace Content.Shared.Roles;

public abstract partial class SharedRoleSystem
{
    private HashSet<JobRequirement>? GetPirateRoleRequirements(JobPrototype job)
    {
        var baseJobId = EmployerJobReplacementHelper.GetBaseJob(_prototypes, new ProtoId<JobPrototype>(job.ID));
        if (baseJobId.Id == job.ID || !_prototypes.TryIndex(baseJobId, out JobPrototype? baseJob))
            return null;

        var requirements = job.Requirements == null
            ? new HashSet<JobRequirement>()
            : new HashSet<JobRequirement>(job.Requirements);

        if (baseJob.Requirements != null)
            requirements.UnionWith(baseJob.Requirements.OfType<DepartmentTimeRequirement>());

        return requirements;
    }
}
