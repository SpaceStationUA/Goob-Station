using Content.Shared.Psionics;
using Content.Shared.StationRecords;
using Robust.Shared.Serialization;

namespace Content.Shared.PsionicsRecords;

[Serializable, NetSerializable]
public enum PsionicsRecordsConsoleKey : byte
{
    Key
}
[Serializable, NetSerializable]
public sealed class PsionicsRecordsConsoleState : BoundUserInterfaceState
{
    public uint? SelectedKey = null;

    public PsionicsRecord? PsionicsRecord = null;
    public GeneralStationRecord? StationRecord = null;
    public Content.Shared._Pirate.StationRecords.RecordPortraitData? Portrait;
    public readonly Dictionary<uint, string>? RecordListing;
    public readonly StationRecordsFilter? Filter;

    public PsionicsRecordsConsoleState(Dictionary<uint, string>? recordListing, StationRecordsFilter? newFilter)
    {
        RecordListing = recordListing;
        Filter = newFilter;
    }
    public PsionicsRecordsConsoleState() : this(null, null)
    {
    }

    public bool IsEmpty() => SelectedKey == null && StationRecord == null && PsionicsRecord == null && RecordListing == null;
}
[Serializable, NetSerializable]
public sealed class PsionicsRecordChangeStatus : BoundUserInterfaceMessage
{
    public readonly PsionicsStatus Status;
    public readonly string? Reason;

    public PsionicsRecordChangeStatus(PsionicsStatus status, string? reason)
    {
        Status = status;
        Reason = reason;
    }
}
[Serializable, NetSerializable]
public sealed class PsionicsMenuRequestState : BoundUserInterfaceMessage
{
}
