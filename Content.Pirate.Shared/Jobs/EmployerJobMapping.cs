using System.Linq;
using Content.Shared._Pirate.Employment;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.Pirate.Jobs;

/// <summary>
/// Resolves employer job replacements and inherited base role IDs.
/// </summary>
public static class EmployerJobMapping
{
    public static ProtoId<JobPrototype> GetJob(
        IPrototypeManager prototypes,
        string? employerId,
        ProtoId<JobPrototype> baseJob)
    {
        if (employerId != null
            && prototypes.TryIndex<EmployerPrototype>(employerId, out var employer)
            && employer.JobReplacements.TryGetValue(baseJob, out var replacement))
        {
            return replacement;
        }

        return baseJob;
    }

    public static ProtoId<JobPrototype> GetBaseJob(IPrototypeManager prototypes, ProtoId<JobPrototype> jobId)
    {
        return EmployerJobReplacementHelper.GetBaseJob(prototypes, jobId);
    }

    public static bool MatchesBaseJob(
        IPrototypeManager prototypes,
        ProtoId<JobPrototype> firstJob,
        ProtoId<JobPrototype> secondJob)
    {
        return GetBaseJob(prototypes, firstJob) == GetBaseJob(prototypes, secondJob);
    }

    public static string GetSlotPoolId(IPrototypeManager prototypes, ProtoId<JobPrototype> jobId)
    {
        var baseJob = GetBaseJob(prototypes, jobId);
        foreach (var group in prototypes.EnumeratePrototypes<JobSlotGroupPrototype>())
        {
            if (group.Jobs.Any(member => GetBaseJob(prototypes, member) == baseJob))
                return $"group:{group.ID}";
        }

        return $"job:{baseJob.Id}";
    }

    public static Dictionary<ProtoId<JobPrototype>, int?> ExpandSlots(
        IPrototypeManager prototypes,
        IReadOnlyDictionary<ProtoId<JobPrototype>, int?> sourceSlots)
    {
        var result = new Dictionary<ProtoId<JobPrototype>, int?>();
        var slotsByPool = new Dictionary<string, int?>();
        foreach (var (job, slots) in sourceSlots)
        {
            var poolId = GetSlotPoolId(prototypes, job);
            if (!slotsByPool.TryGetValue(poolId, out var existing))
            {
                slotsByPool.Add(poolId, slots);
                continue;
            }

            slotsByPool[poolId] = existing == null || slots == null
                ? null
                : checked(existing.Value + slots.Value);
        }

        foreach (var job in prototypes.EnumeratePrototypes<JobPrototype>())
        {
            var jobId = new ProtoId<JobPrototype>(job.ID);
            if (!job.SetPreference && !sourceSlots.ContainsKey(jobId))
                continue;

            var poolId = GetSlotPoolId(prototypes, jobId);
            if (!slotsByPool.TryGetValue(poolId, out var slots))
                continue;

            result[jobId] = slots;
        }

        foreach (var (job, slots) in sourceSlots)
            result.TryAdd(job, slots);

        return result;
    }
}
