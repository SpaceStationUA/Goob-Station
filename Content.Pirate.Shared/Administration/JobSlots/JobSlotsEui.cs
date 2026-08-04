// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Eui;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Administration.JobSlots;

public static class JobSlotsEuiConstants
{
    public const int MaxAdjustment = 100;
}

[Serializable, NetSerializable]
public sealed class JobSlotsEuiState : EuiStateBase
{
    public List<JobSlotsStationData> Stations { get; }

    public JobSlotsEuiState(List<JobSlotsStationData> stations)
    {
        Stations = stations;
    }
}

[Serializable, NetSerializable]
public sealed class JobSlotsStationData
{
    public NetEntity Station { get; }
    public string Name { get; }
    public Dictionary<ProtoId<JobPrototype>, int?> Jobs { get; }

    public JobSlotsStationData(
        NetEntity station,
        string name,
        Dictionary<ProtoId<JobPrototype>, int?> jobs)
    {
        Station = station;
        Name = name;
        Jobs = jobs;
    }
}

[Serializable, NetSerializable]
public sealed class AdjustJobSlotsEuiMessage : EuiMessageBase
{
    public NetEntity Station { get; }
    public ProtoId<JobPrototype> Job { get; }
    public int Amount { get; }

    public AdjustJobSlotsEuiMessage(NetEntity station, ProtoId<JobPrototype> job, int amount)
    {
        Station = station;
        Job = job;
        Amount = amount;
    }
}

[Serializable, NetSerializable]
public sealed class RefreshJobSlotsEuiMessage : EuiMessageBase;

[Serializable, NetSerializable]
public enum JobSlotsUpdateResult : byte
{
    Success,
    InvalidAmount,
    StationUnavailable,
    JobUnavailable,
    AdjustmentFailed,
    Unlimited,
}

[Serializable, NetSerializable]
public sealed class JobSlotsUpdateEuiMessage : EuiMessageBase
{
    public JobSlotsUpdateResult Result { get; }
    public ProtoId<JobPrototype> Job { get; }
    public int? Slots { get; }

    public JobSlotsUpdateEuiMessage(
        JobSlotsUpdateResult result,
        ProtoId<JobPrototype> job,
        int? slots = null)
    {
        Result = result;
        Job = job;
        Slots = slots;
    }
}
