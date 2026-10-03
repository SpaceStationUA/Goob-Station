using Content.Pirate.Common.AlternativeJobs;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.Preferences;

public sealed partial class HumanoidCharacterProfile
{
    [DataField]
    private Dictionary<ProtoId<JobPrototype>, ProtoId<AlternativeJobPrototype>> _jobAlternatives = new();

    public IReadOnlyDictionary<ProtoId<JobPrototype>, ProtoId<AlternativeJobPrototype>> JobAlternatives => _jobAlternatives;

    public HumanoidCharacterProfile WithJobAlternative(
        KeyValuePair<ProtoId<JobPrototype>, ProtoId<AlternativeJobPrototype>> jobAlternative)
    {
        var alternatives = new Dictionary<ProtoId<JobPrototype>, ProtoId<AlternativeJobPrototype>>(_jobAlternatives)
        {
            [jobAlternative.Key] = jobAlternative.Value,
        };

        return new(this) { _jobAlternatives = alternatives };
    }

    private bool JobAlternativesEqual(HumanoidCharacterProfile other)
    {
        if (_jobAlternatives.Count != other._jobAlternatives.Count)
            return false;

        foreach (var (job, alternative) in _jobAlternatives)
        {
            if (!other._jobAlternatives.TryGetValue(job, out var otherAlternative) || alternative != otherAlternative)
                return false;
        }

        return true;
    }

    private void AddJobAlternativesHash(ref HashCode hashCode)
    {
        var alternativesHash = 0;
        foreach (var (job, alternative) in _jobAlternatives)
            alternativesHash ^= HashCode.Combine(job, alternative);

        hashCode.Add(alternativesHash);
    }

    private void EnsureJobAlternativesValid(IPrototypeManager prototypes)
    {
        var validAlternatives = new Dictionary<ProtoId<JobPrototype>, ProtoId<AlternativeJobPrototype>>();
        foreach (var (jobId, alternativeId) in _jobAlternatives)
        {
            if (!prototypes.TryIndex(jobId, out JobPrototype? job) || !job.SetPreference)
                continue;

            if (alternativeId.Id == jobId.Id
                || prototypes.TryIndex(alternativeId, out AlternativeJobPrototype? alternative)
                && alternative.ParentJobId == jobId.Id)
                validAlternatives[jobId] = alternativeId;
        }

        _jobAlternatives = validAlternatives;
    }
}
