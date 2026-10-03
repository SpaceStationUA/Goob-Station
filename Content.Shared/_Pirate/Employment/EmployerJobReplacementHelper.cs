using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Employment;

public static class EmployerJobReplacementHelper
{
    public static ProtoId<JobPrototype> GetBaseJob(
        IPrototypeManager prototypes,
        ProtoId<JobPrototype> jobId)
    {
        foreach (var employer in prototypes.EnumeratePrototypes<EmployerPrototype>())
        {
            foreach (var (baseJob, replacement) in employer.JobReplacements)
            {
                if (replacement == jobId)
                    return baseJob;
            }
        }

        return jobId;
    }
}
