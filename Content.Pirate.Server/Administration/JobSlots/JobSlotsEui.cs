// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Pirate.Shared.Administration.JobSlots;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Eui;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.Administration.JobSlots;

public sealed class JobSlotsEui : BaseEui
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private readonly StationJobsSystem _jobSystem;
    private readonly StationSystem _stationSystem;

    public JobSlotsEui()
    {
        IoCManager.InjectDependencies(this);
        _jobSystem = _entityManager.System<StationJobsSystem>();
        _stationSystem = _entityManager.System<StationSystem>();
    }

    public override void Opened()
    {
        base.Opened();
        _adminManager.OnPermsChanged += OnPermsChanged;
        StateDirty();
    }

    public override void Closed()
    {
        _adminManager.OnPermsChanged -= OnPermsChanged;
        base.Closed();
    }

    public override JobSlotsEuiState GetNewState()
    {
        if (!_adminManager.HasAdminFlag(Player, AdminFlags.Admin))
            return new JobSlotsEuiState([]);

        var stations = new List<JobSlotsStationData>();

        foreach (var station in _stationSystem.GetStations())
        {
            if (!_entityManager.HasComponent<StationJobsComponent>(station))
                continue;

            var jobs = _jobSystem.GetJobs(station)
                .ToDictionary(entry => entry.Key, entry => entry.Value);
            var stationName = _entityManager.GetComponent<MetaDataComponent>(station).EntityName;

            stations.Add(new JobSlotsStationData(
                _entityManager.GetNetEntity(station),
                stationName,
                jobs));
        }

        stations.Sort((left, right) =>
            string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase));

        return new JobSlotsEuiState(stations);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (!_adminManager.HasAdminFlag(Player, AdminFlags.Admin))
        {
            Close();
            return;
        }

        switch (msg)
        {
            case RefreshJobSlotsEuiMessage:
                StateDirty();
                break;
            case AdjustJobSlotsEuiMessage adjust:
                AdjustSlots(adjust);
                break;
        }
    }

    private void AdjustSlots(AdjustJobSlotsEuiMessage message)
    {
        if (message.Amount is 0 or < -JobSlotsEuiConstants.MaxAdjustment or > JobSlotsEuiConstants.MaxAdjustment)
        {
            SendResult(JobSlotsUpdateResult.InvalidAmount, message.Job);
            return;
        }

        if (!_entityManager.TryGetEntity(message.Station, out var station) ||
            station is not { } stationUid ||
            !_entityManager.HasComponent<StationJobsComponent>(stationUid) ||
            !_stationSystem.GetStationsSet().Contains(stationUid))
        {
            SendResult(JobSlotsUpdateResult.StationUnavailable, message.Job);
            StateDirty();
            return;
        }

        if (!_prototypeManager.TryIndex<JobPrototype>(message.Job, out var job))
        {
            SendResult(JobSlotsUpdateResult.JobUnavailable, message.Job);
            return;
        }

        var existed = _jobSystem.TryGetJobSlot(stationUid, message.Job, out var before);
        if (!job.SetPreference && !existed)
        {
            SendResult(JobSlotsUpdateResult.AdjustmentFailed, message.Job);
            return;
        }

        if (existed && before is null)
        {
            SendResult(JobSlotsUpdateResult.Unlimited, message.Job);
            StateDirty();
            return;
        }

        if (before is { } previous &&
            message.Amount > 0 &&
            previous > int.MaxValue - message.Amount)
        {
            SendResult(JobSlotsUpdateResult.InvalidAmount, message.Job);
            return;
        }

        if (!_jobSystem.TryAdjustJobSlot(
                stationUid,
                message.Job,
                message.Amount,
                createSlot: true,
                clamp: true))
        {
            SendResult(JobSlotsUpdateResult.AdjustmentFailed, message.Job);
            StateDirty();
            return;
        }

        if (!_jobSystem.TryGetJobSlot(stationUid, message.Job, out var after) || after is null)
        {
            SendResult(JobSlotsUpdateResult.AdjustmentFailed, message.Job);
            StateDirty();
            return;
        }

        var stationName = _entityManager.GetComponent<MetaDataComponent>(stationUid).EntityName;
        var beforeText = existed ? before?.ToString() ?? "unlimited" : "not present";
        var jobDescription = $"{job.ID} ({job.LocalizedName})";
        var changeDescription = $"{beforeText} -> {after.Value} (delta {message.Amount})";

        _adminLogger.Add(
            LogType.AdminMessage,
            LogImpact.Medium,
            $"{Player:actor} adjusted job slots for {jobDescription} on {stationName}: {changeDescription}");

        SendResult(JobSlotsUpdateResult.Success, message.Job, after.Value);
        StateDirty();
    }

    private void SendResult(JobSlotsUpdateResult result, ProtoId<JobPrototype> job, int? slots = null)
    {
        SendMessage(new JobSlotsUpdateEuiMessage(result, job, slots));
    }

    private void OnPermsChanged(AdminPermsChangedEventArgs args)
    {
        if (args.Player == Player && !_adminManager.HasAdminFlag(Player, AdminFlags.Admin))
            Close();
    }
}
