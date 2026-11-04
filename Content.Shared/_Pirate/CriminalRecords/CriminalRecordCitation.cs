// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.CriminalRecords;

[Serializable, NetSerializable]
public enum CitationStatus : byte
{
    Active,

    Paid,

    Voided,
}

[Serializable, NetSerializable, DataRecord]
public sealed partial record CriminalRecordCitation
{
    [DataField]
    public uint Id;

    [DataField]
    public string Reason = string.Empty;

    [DataField]
    public string Details = string.Empty;

    [DataField]
    public int Fine;

    [DataField]
    public CitationStatus Status = CitationStatus.Active;

    [DataField]
    public string? IssuedBy;

    [DataField]
    public DateTime IssuedAt;

    [DataField]
    public string? ClosedBy;

    [DataField]
    public DateTime? ClosedAt;

    // Payment and void update this entry instead of adding new entries.
    [DataField]
    public uint? LogEntryId;
}

public static class CriminalRecordCitations
{
    public const int MaxFine = 50000;

    public const int MaxReasonLength = 64;

    public const int MaxDetailsLength = 300;
}

[Serializable, NetSerializable]
public sealed class CriminalRecordAddCitation(string reason, string details, int fine) : BoundUserInterfaceMessage
{
    public readonly string Reason = reason;
    public readonly string Details = details;
    public readonly int Fine = fine;
}

[Serializable, NetSerializable]
public sealed class CriminalRecordPayCitation(uint citationId) : BoundUserInterfaceMessage
{
    public readonly uint CitationId = citationId;
}

[Serializable, NetSerializable]
public sealed class CriminalRecordVoidCitation(uint citationId) : BoundUserInterfaceMessage
{
    public readonly uint CitationId = citationId;
}
