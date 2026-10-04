using System.Linq;
using Content.Server.Station.Components;
using Content.Shared.GameTicking;
using Content.Shared.Pirate.Jobs;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server.Station.Systems;

public sealed partial class StationJobsSystem
{
    /// <inheritdoc cref="TryAdjustJobSlot(EntityUid,string,int,bool,bool,StationJobsComponent?)"/>
    private bool TryAdjustJobSlotPirate(
        EntityUid station,
        string jobPrototypeId,
        int amount,
        bool createSlot = false,
        bool clamp = false,
        StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs))
            throw new ArgumentException("Tried to use a non-station entity as a station!", nameof(station));

        if (amount == 0)
            return true;

        var jobList = stationJobs.JobList;
        var jobId = new ProtoId<JobPrototype>(jobPrototypeId);
        var baseJob = EmployerJobMapping.GetBaseJob(_prototypeManager, jobId);
        var slotKeys = jobList.Keys
            .Where(key => EmployerJobMapping.GetBaseJob(_prototypeManager, key) == baseJob)
            .OrderBy(key => key == jobId ? 0 : 1)
            .ToList();

        if (amount < 0)
        {
            if (slotKeys.Count == 0)
                return false;

            if (slotKeys.Any(key => jobList[key] == null))
                return true;

            long remaining = -(long) amount;
            if (!clamp && slotKeys.Sum(key => (long) jobList[key]!.Value) < remaining)
            {
                return false;
            }

            foreach (var key in slotKeys)
            {
                var available = jobList[key];
                if (available == null)
                    return true;

                var used = (int) Math.Min(available.Value, remaining);
                jobList[key] = available.Value - used;
                remaining -= used;
                if (remaining == 0)
                    break;
            }

            if (remaining > 0 && clamp)
            {
                foreach (var key in slotKeys)
                    jobList[key] = 0;
            }

            stationJobs.TotalJobs = jobList.Values.Sum(value => value ?? 0);
            UpdateJobsAvailable();
            return true;
        }

        if (slotKeys.Any(key => jobList[key] == null))
            return true;

        var addKey = slotKeys.FirstOrDefault();
        if (slotKeys.Count == 0)
        {
            if (!createSlot)
                return false;

            addKey = EmployerJobMapping.GetBaseJob(_prototypeManager, jobId);
        }

        var current = jobList.GetValueOrDefault(addKey) ?? 0;
        jobList[addKey] = current + amount;
        stationJobs.TotalJobs = jobList.Values.Sum(value => value ?? 0);
        UpdateJobsAvailable();
        return true;
    }

    /// <inheritdoc cref="TrySetJobSlot(EntityUid,string,int,bool,StationJobsComponent?)"/>
    private bool TrySetJobSlotPirate(
        EntityUid station,
        string jobPrototypeId,
        int amount,
        bool createSlot = false,
        StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs))
            throw new ArgumentException("Tried to use a non-station entity as a station!", nameof(station));
        if (amount < 0)
            throw new ArgumentException("Tried to set a job to have a negative number of slots!", nameof(amount));

        var jobList = stationJobs.JobList;
        var jobId = new ProtoId<JobPrototype>(jobPrototypeId);
        var baseJob = EmployerJobMapping.GetBaseJob(_prototypeManager, jobId);
        var poolKeys = jobList.Keys
            .Where(key => EmployerJobMapping.GetBaseJob(_prototypeManager, key) == baseJob)
            .ToArray();

        if (poolKeys.Length == 0 && !createSlot)
            return false;

        var target = poolKeys.FirstOrDefault();
        if (poolKeys.Length == 0)
            target = EmployerJobMapping.GetBaseJob(_prototypeManager, jobId);

        stationJobs.TotalJobs += amount - poolKeys.Sum(key => jobList[key] ?? 0);
        foreach (var key in poolKeys)
        {
            if (key != target)
                jobList.Remove(key);
        }

        jobList[target] = amount;
        UpdateJobsAvailable();
        return true;
    }

    /// <inheritdoc cref="MakeJobUnlimited(EntityUid,string,StationJobsComponent?)"/>
    private void MakeJobUnlimitedPirate(EntityUid station, string jobPrototypeId, StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs))
            throw new ArgumentException("Tried to use a non-station entity as a station!", nameof(station));

        var jobId = new ProtoId<JobPrototype>(jobPrototypeId);
        var baseJob = EmployerJobMapping.GetBaseJob(_prototypeManager, jobId);
        var jobList = stationJobs.JobList;
        var poolKeys = jobList.Keys
            .Where(key => EmployerJobMapping.GetBaseJob(_prototypeManager, key) == baseJob)
            .ToArray();
        var target = poolKeys.FirstOrDefault();
        if (poolKeys.Length == 0)
            target = EmployerJobMapping.GetBaseJob(_prototypeManager, jobId);

        stationJobs.TotalJobs -= poolKeys.Sum(key => jobList[key] ?? 0);
        foreach (var key in poolKeys)
        {
            if (key != target)
                jobList.Remove(key);
        }

        jobList[target] = null;
        UpdateJobsAvailable();
    }

    /// <inheritdoc cref="TryGetJobSlot(EntityUid,string,out int?,StationJobsComponent?)"/>
    private bool TryGetJobSlotPirate(EntityUid station, string jobPrototypeId, out int? slots, StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs))
            throw new ArgumentException("Tried to use a non-station entity as a station!", nameof(station));

        var baseJob = EmployerJobMapping.GetBaseJob(_prototypeManager, new ProtoId<JobPrototype>(jobPrototypeId));
        var matching = stationJobs.JobList
            .Where(pair => EmployerJobMapping.GetBaseJob(_prototypeManager, pair.Key) == baseJob)
            .Select(pair => pair.Value)
            .ToArray();
        if (matching.Length == 0)
        {
            slots = null;
            return false;
        }

        slots = matching.Any(value => value == null) ? null : matching.Sum(value => value!.Value);
        return true;
    }

    /// <inheritdoc cref="GetAvailableJobs(EntityUid,StationJobsComponent?)"/>
    private IEnumerable<ProtoId<JobPrototype>> GetAvailableJobsPirate(EntityUid station, StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs))
            throw new ArgumentException("Tried to use a non-station entity as a station!", nameof(station));

        return EmployerJobMapping.ExpandSlots(_prototypeManager, stationJobs.JobList)
            .Where(pair => pair.Value != 0)
            .Select(pair => pair.Key);
    }

    /// <inheritdoc cref="GetOverflowJobs(EntityUid,StationJobsComponent?)"/>
    private IReadOnlySet<ProtoId<JobPrototype>> GetOverflowJobsPirate(EntityUid station, StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs))
            throw new ArgumentException("Tried to use a non-station entity as a station!", nameof(station));

        var overflowSlots = stationJobs.OverflowJobs.ToDictionary(job => job, _ => (int?) null);
        return EmployerJobMapping.ExpandSlots(_prototypeManager, overflowSlots)
            .Keys
            .ToHashSet();
    }

    /// <inheritdoc cref="GetRoundStartJobs(EntityUid,StationJobsComponent?)"/>
    private Dictionary<ProtoId<JobPrototype>, int?> GetRoundStartJobsPirate(EntityUid station, StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs))
            throw new ArgumentException("Tried to use a non-station entity as a station!", nameof(station));

        return EmployerJobMapping.ExpandSlots(_prototypeManager, stationJobs.SetupAvailableJobs.ToDictionary(
            pair => pair.Key,
            pair => (int?)(pair.Value[0] < 0 ? null : pair.Value[0])));
    }

    private TickerJobsAvailableEvent GenerateJobsAvailableEventPirate()
    {
        if (_gameTicker.DisallowLateJoin)
            return new TickerJobsAvailableEvent(new(), new());

        var jobs = new Dictionary<NetEntity, Dictionary<ProtoId<JobPrototype>, int?>>();
        var stationNames = new Dictionary<NetEntity, string>();
        var query = EntityQueryEnumerator<StationJobsComponent>();

        while (query.MoveNext(out var station, out var comp))
        {
            var netStation = GetNetEntity(station);
            jobs.Add(netStation, EmployerJobMapping.ExpandSlots(_prototypeManager, comp.JobList));
            stationNames.Add(netStation, Name(station));
        }

        return new TickerJobsAvailableEvent(stationNames, jobs);
    }

    private int CountSlotsByBaseJob(IReadOnlyDictionary<ProtoId<JobPrototype>, int?> jobs)
    {
        var seenBaseJobs = new HashSet<ProtoId<JobPrototype>>();
        var count = 0;
        foreach (var (job, slots) in jobs)
        {
            if (seenBaseJobs.Add(EmployerJobMapping.GetBaseJob(_prototypeManager, job)))
                count += slots ?? 1;
        }

        return count;
    }

    private void DecrementBaseJobSlots(
        Dictionary<ProtoId<JobPrototype>, int?> slots,
        ProtoId<JobPrototype> selectedJob)
    {
        var baseJob = EmployerJobMapping.GetBaseJob(_prototypeManager, selectedJob);
        foreach (var job in slots.Keys.ToArray())
        {
            if (EmployerJobMapping.GetBaseJob(_prototypeManager, job) != baseJob || slots[job] is not { } remaining)
                continue;

            slots[job] = Math.Max(remaining - 1, 0);
        }
    }
}
