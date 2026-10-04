// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._Pirate.Station.Events; // Pirate - automatic acting captain procedure
using Content.Server.GameTicking;
using Content.Server.Station.Components;
using Content.Shared.CCVar;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using JetBrains.Annotations;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Station.Systems;

/// <summary>
/// Manages job slots for stations.
/// </summary>
[PublicAPI]
public sealed partial class StationJobsSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<StationInitializedEvent>(OnStationInitialized);
        SubscribeLocalEvent<StationJobsComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<StationJobsComponent, StationRenamedEvent>(OnStationRenamed);
        SubscribeLocalEvent<StationJobsComponent, ComponentShutdown>(OnStationDeletion);
        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
        Subs.CVar(_configurationManager, CCVars.GameDisallowLateJoins, _ => UpdateJobsAvailable(), true);
    }

    private void OnInit(Entity<StationJobsComponent> ent, ref ComponentInit args)
    {
        ent.Comp.MidRoundTotalJobs = ent.Comp.SetupAvailableJobs.Values
            .Select(x => Math.Max(x[1], 0))
            .Sum();

        ent.Comp.OverflowJobs = ent.Comp.SetupAvailableJobs
            .Where(x => x.Value[0] < 0)
            .Select(x => x.Key)
            .ToHashSet();
    }

    public override void Update(float _)
    {
        if (_availableJobsDirty)
        {
            _cachedAvailableJobs = GenerateJobsAvailableEvent();
            RaiseNetworkEvent(_cachedAvailableJobs, Filter.Empty().AddPlayers(_player.Sessions));
            _availableJobsDirty = false;
        }
    }

    private void OnStationDeletion(EntityUid uid, StationJobsComponent component, ComponentShutdown args)
    {
        UpdateJobsAvailable(); // we no longer exist so the jobs list is changed.
    }

    private void OnStationInitialized(StationInitializedEvent msg)
    {
        if (!TryComp<StationJobsComponent>(msg.Station, out var stationJobs))
            return;

        stationJobs.JobList = stationJobs.SetupAvailableJobs.ToDictionary(
            x => x.Key,
            x=> (int?)(x.Value[1] < 0 ? null : x.Value[1]));

        stationJobs.TotalJobs = stationJobs.JobList.Values.Select(x => x ?? 0).Sum();

        UpdateJobsAvailable();
    }

    #region Public API

    /// <inheritdoc cref="TryAssignJob(Robust.Shared.GameObjects.EntityUid,string,NetUserId,Content.Server.Station.Components.StationJobsComponent?)"/>
    /// <param name="station">Station to assign a job on.</param>
    /// <param name="job">Job to assign.</param>
    /// <param name="netUserId">The net user ID of the player we're assigning this job to.</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    public bool TryAssignJob(EntityUid station, JobPrototype job, NetUserId netUserId, StationJobsComponent? stationJobs = null)
    {
        return TryAssignJob(station, job.ID, netUserId, stationJobs);
    }

    /// <summary>
    /// Attempts to assign the given job once. (essentially, it decrements the slot if possible).
    /// </summary>
    /// <param name="station">Station to assign a job on.</param>
    /// <param name="jobPrototypeId">Job prototype ID to assign.</param>
    /// <param name="netUserId">The net user ID of the player we're assigning this job to.</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    /// <returns>Whether or not assignment was a success.</returns>
    /// <exception cref="ArgumentException">Thrown when the given station is not a station.</exception>
    public bool TryAssignJob(EntityUid station, string jobPrototypeId, NetUserId netUserId, StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs, false))
            return false;

        if (!TryAdjustJobSlot(station, jobPrototypeId, -1, false, false, stationJobs))
            return false;

        var playerJobAdded = new PlayerJobAddedEvent(netUserId, jobPrototypeId);
        RaiseLocalEvent(station, ref playerJobAdded, false); // Pirate - notify station systems about captain changes
        stationJobs.PlayerJobs.TryAdd(netUserId, new());
        stationJobs.PlayerJobs[netUserId].Add(jobPrototypeId);
        return true;
    }

    /// <inheritdoc cref="TryAdjustJobSlot(Robust.Shared.GameObjects.EntityUid,string,int,bool,bool,Content.Server.Station.Components.StationJobsComponent?)"/>
    /// <param name="station">Station to adjust the job slot on.</param>
    /// <param name="job">Job to adjust.</param>
    /// <param name="amount">Amount to adjust by.</param>
    /// <param name="createSlot">Whether or not it should create the slot if it doesn't exist.</param>
    /// <param name="clamp">Whether or not to clamp to zero if you'd remove more jobs than are available.</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    public bool TryAdjustJobSlot(EntityUid station, JobPrototype job, int amount, bool createSlot = false, bool clamp = false,
        StationJobsComponent? stationJobs = null)
    {
        return TryAdjustJobSlot(station, job.ID, amount, createSlot, clamp, stationJobs);
    }

    /// <summary>
    /// Attempts to adjust the given job slot by the amount provided.
    /// </summary>
    /// <param name="station">Station to adjust the job slot on.</param>
    /// <param name="jobPrototypeId">Job prototype ID to adjust.</param>
    /// <param name="amount">Amount to adjust by.</param>
    /// <param name="createSlot">Whether or not it should create the slot if it doesn't exist.</param>
    /// <param name="clamp">Whether or not to clamp to zero if you'd remove more jobs than are available.</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    /// <returns>Whether or not slot adjustment was a success.</returns>
    /// <exception cref="ArgumentException">Thrown when the given station is not a station.</exception>
    public bool TryAdjustJobSlot(EntityUid station,
        string jobPrototypeId,
        int amount,
        bool createSlot = false,
        bool clamp = false,
        StationJobsComponent? stationJobs = null)
    {
        // Pirate: shared-slot implementation is isolated in StationJobsSystem.Pirate.cs.
        return TryAdjustJobSlotPirate(station, jobPrototypeId, amount, createSlot, clamp, stationJobs);
    }

    public bool TryGetPlayerJobs(EntityUid station,
        NetUserId userId,
        [NotNullWhen(true)] out List<ProtoId<JobPrototype>>? jobs,
        StationJobsComponent? jobsComponent = null)
    {
        jobs = null;
        if (!Resolve(station, ref jobsComponent, false))
            return false;

        return jobsComponent.PlayerJobs.TryGetValue(userId, out jobs);
    }

    public bool TryRemovePlayerJobs(EntityUid station,
        NetUserId userId,
        StationJobsComponent? jobsComponent = null)
    {
        if (!Resolve(station, ref jobsComponent, false))
            return false;

        // Pirate - notify station systems after the manifest entry is removed.
        if (!jobsComponent.PlayerJobs.Remove(userId, out var playerJobs))
            return false;

        var playerJobsRemoved = new PlayerJobsRemovedEvent(userId, playerJobs);
        RaiseLocalEvent(station, ref playerJobsRemoved, false);
        return true;
    }

    /// <inheritdoc cref="TrySetJobSlot(Robust.Shared.GameObjects.EntityUid,string,int,bool,Content.Server.Station.Components.StationJobsComponent?)"/>
    /// <param name="station">Station to adjust the job slot on.</param>
    /// <param name="jobPrototype">Job prototype to adjust.</param>
    /// <param name="amount">Amount to set to.</param>
    /// <param name="createSlot">Whether or not it should create the slot if it doesn't exist.</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    /// <returns></returns>
    public bool TrySetJobSlot(EntityUid station, JobPrototype jobPrototype, int amount, bool createSlot = false,
        StationJobsComponent? stationJobs = null)
    {
        return TrySetJobSlot(station, jobPrototype.ID, amount, createSlot, stationJobs);
    }

    /// <summary>
    /// Attempts to set the given job slot to the amount provided.
    /// </summary>
    /// <param name="station">Station to adjust the job slot on.</param>
    /// <param name="jobPrototypeId">Job prototype ID to adjust.</param>
    /// <param name="amount">Amount to set to.</param>
    /// <param name="createSlot">Whether or not it should create the slot if it doesn't exist.</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    /// <returns>Whether or not setting the value succeeded.</returns>
    /// <exception cref="ArgumentException">Thrown when the given station is not a station.</exception>
    public bool TrySetJobSlot(EntityUid station,
        string jobPrototypeId,
        int amount,
        bool createSlot = false,
        StationJobsComponent? stationJobs = null)
    {
        // Pirate: variants map back to their source map job.
        return TrySetJobSlotPirate(station, jobPrototypeId, amount, createSlot, stationJobs);
    }

    /// <inheritdoc cref="MakeJobUnlimited(Robust.Shared.GameObjects.EntityUid,string,Content.Server.Station.Components.StationJobsComponent?)"/>
    /// <param name="station">Station to make a job unlimited on.</param>
    /// <param name="job">Job to make unlimited.</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    public void MakeJobUnlimited(EntityUid station, JobPrototype job, StationJobsComponent? stationJobs = null)
    {
        MakeJobUnlimited(station, job.ID, stationJobs);
    }

    /// <summary>
    /// Makes the given job have unlimited slots.
    /// </summary>
    /// <param name="station">Station to make a job unlimited on.</param>
    /// <param name="jobPrototypeId">Job prototype ID to make unlimited.</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    /// <exception cref="ArgumentException">Thrown when the given station is not a station.</exception>
    public void MakeJobUnlimited(EntityUid station, string jobPrototypeId, StationJobsComponent? stationJobs = null)
    {
        // Pirate: variants map back to their source map job.
        MakeJobUnlimitedPirate(station, jobPrototypeId, stationJobs);
    }

    /// <inheritdoc cref="IsJobUnlimited(Robust.Shared.GameObjects.EntityUid,string,Content.Server.Station.Components.StationJobsComponent?)"/>
    /// <param name="station">Station to check.</param>
    /// <param name="job">Job to check.</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    public bool IsJobUnlimited(EntityUid station, JobPrototype job, StationJobsComponent? stationJobs = null)
    {
        return IsJobUnlimited(station, job.ID, stationJobs);
    }

    /// <summary>
    /// Checks if the given job is unlimited.
    /// </summary>
    /// <param name="station">Station to check.</param>
    /// <param name="jobPrototypeId">Job prototype ID to check.</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    /// <returns>Returns if the given slot is unlimited.</returns>
    /// <exception cref="ArgumentException">Thrown when the given station is not a station.</exception>
    public bool IsJobUnlimited(EntityUid station, string jobPrototypeId, StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs))
            throw new ArgumentException("Tried to use a non-station entity as a station!", nameof(station));

        return TryGetJobSlot(station, jobPrototypeId, out var slots, stationJobs) && slots == null; // Pirate: replacement roles share their base job's slots.
    }

    /// <inheritdoc cref="TryGetJobSlot(Robust.Shared.GameObjects.EntityUid,string,out System.Nullable{uint},Content.Server.Station.Components.StationJobsComponent?)"/>
    /// <param name="station">Station to get slot info from.</param>
    /// <param name="job">Job to get slot info for.</param>
    /// <param name="slots">The number of slots remaining. Null if infinite.</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    public bool TryGetJobSlot(EntityUid station, JobPrototype job, out int? slots, StationJobsComponent? stationJobs = null)
    {
        return TryGetJobSlot(station, job.ID, out slots, stationJobs);
    }

    /// <summary>
    /// Returns information about the given job slot.
    /// </summary>
    /// <param name="station">Station to get slot info from.</param>
    /// <param name="jobPrototypeId">Job prototype ID to get slot info for.</param>
    /// <param name="slots">The number of slots remaining. Null if infinite.</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    /// <returns>Whether or not the slot exists.</returns>
    /// <exception cref="ArgumentException">Thrown when the given station is not a station.</exception>
    /// <remarks>slots will be null if the slot doesn't exist, as well, so make sure to check the return value.</remarks>
    public bool TryGetJobSlot(EntityUid station, string jobPrototypeId, out int? slots, StationJobsComponent? stationJobs = null)
    {
        // Pirate: replacement roles share their base job's available slots.
        return TryGetJobSlotPirate(station, jobPrototypeId, out slots, stationJobs);
    }

    /// <summary>
    /// Returns all jobs available on the station.
    /// </summary>
    /// <param name="station">Station to get jobs for</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    /// <returns>Set containing all jobs available.</returns>
    /// <exception cref="ArgumentException">Thrown when the given station is not a station.</exception>
    public IEnumerable<ProtoId<JobPrototype>> GetAvailableJobs(EntityUid station, StationJobsComponent? stationJobs = null)
    {
        // Pirate: expose employer variants for the source job's map slots.
        return GetAvailableJobsPirate(station, stationJobs);
    }

    /// <summary>
    /// Returns all overflow jobs available on the station.
    /// </summary>
    /// <param name="station">Station to get jobs for</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    /// <returns>Set containing all overflow jobs available.</returns>
    /// <exception cref="ArgumentException">Thrown when the given station is not a station.</exception>
    public IReadOnlySet<ProtoId<JobPrototype>> GetOverflowJobs(EntityUid station, StationJobsComponent? stationJobs = null)
    {
        // Pirate: overflow availability follows source job slots.
        return GetOverflowJobsPirate(station, stationJobs);
    }

    /// <summary>
    /// Returns a readonly dictionary of all jobs and their slot info.
    /// </summary>
    /// <param name="station">Station to get jobs for</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    /// <returns>List of all jobs on the station.</returns>
    /// <exception cref="ArgumentException">Thrown when the given station is not a station.</exception>
    public IReadOnlyDictionary<ProtoId<JobPrototype>, int?> GetJobs(EntityUid station, StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs))
            throw new ArgumentException("Tried to use a non-station entity as a station!", nameof(station));

        return stationJobs.JobList;
    }

    /// <summary>
    /// Returns a readonly dictionary of all round-start jobs and their slot info.
    /// </summary>
    /// <param name="station">Station to get jobs for</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    /// <returns>List of all round-start jobs.</returns>
    /// <exception cref="ArgumentException">Thrown when the given station is not a station.</exception>
    public Dictionary<ProtoId<JobPrototype>, int?> GetRoundStartJobs(EntityUid station, StationJobsComponent? stationJobs = null)
    {
        // Pirate: expand map quotas to applicable employer variants.
        return GetRoundStartJobsPirate(station, stationJobs);
    }

    /// <summary>
    /// Looks at the given priority list, and picks the best available job (optionally with the given exclusions)
    /// </summary>
    /// <param name="station">Station to pick from.</param>
    /// <param name="jobPriorities">The priority list to use for selecting a job.</param>
    /// <param name="pickOverflows">Whether or not to pick from the overflow list.</param>
    /// <param name="disallowedJobs">A set of disallowed jobs, if any.</param>
    /// <returns>The selected job, if any.</returns>
    public ProtoId<JobPrototype>? PickBestAvailableJobWithPriority(EntityUid station, IReadOnlyDictionary<ProtoId<JobPrototype>, JobPriority> jobPriorities, bool pickOverflows, IReadOnlySet<ProtoId<JobPrototype>>? disallowedJobs = null)
    {
        if (station == EntityUid.Invalid)
            return null;

        var available = GetAvailableJobs(station);
        bool TryPick(JobPriority priority, [NotNullWhen(true)] out ProtoId<JobPrototype>? jobId)
        {
            var filtered = jobPriorities
                .Where(p =>
                            p.Value == priority
                            && disallowedJobs != null
                            && !disallowedJobs.Contains(p.Key)
                            && available.Contains(p.Key))
                .Select(p => p.Key)
                .ToList();

            if (filtered.Count != 0)
            {
                jobId = _random.Pick(filtered);
                return true;
            }

            jobId = default;
            return false;
        }

        if (TryPick(JobPriority.High, out var picked))
        {
            return picked;
        }

        if (TryPick(JobPriority.Medium, out picked))
        {
            return picked;
        }

        if (TryPick(JobPriority.Low, out picked))
        {
            return picked;
        }

        if (!pickOverflows)
            return null;

        var overflows = GetOverflowJobs(station);
        if (overflows.Count == 0)
            return null;

        return _random.Pick(overflows);
    }

    #endregion Public API

    #region Latejoin job management

    private bool _availableJobsDirty;

    private TickerJobsAvailableEvent _cachedAvailableJobs = new(new(), new());

    /// <summary>
    /// Assembles an event from the current available-to-play jobs.
    /// This is moderately expensive to construct.
    /// </summary>
    /// <returns>The event.</returns>
    private TickerJobsAvailableEvent GenerateJobsAvailableEvent()
    {
        // Pirate: publish role variants alongside the source map roles.
        return GenerateJobsAvailableEventPirate();
    }

    /// <summary>
    /// Updates the cached available jobs. Moderately expensive.
    /// </summary>
    private void UpdateJobsAvailable()
    {
        _availableJobsDirty = true;
    }

    private void OnPlayerJoinedLobby(PlayerJoinedLobbyEvent ev)
    {
        RaiseNetworkEvent(_cachedAvailableJobs, ev.PlayerSession.Channel);
    }

    private void OnStationRenamed(EntityUid uid, StationJobsComponent component, StationRenamedEvent args)
    {
        UpdateJobsAvailable();
    }

    #endregion
}
