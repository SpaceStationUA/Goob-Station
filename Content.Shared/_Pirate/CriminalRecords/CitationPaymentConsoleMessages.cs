// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.CriminalRecords;

[Serializable, NetSerializable]
public enum CitationPaymentConsoleUiKey : byte
{
    Key,
}

// Do not send security status, notes, or history to public terminals.
[Serializable, NetSerializable]
public sealed record CitationPaymentPerson(uint RecordId, string Name, string Job, List<CriminalRecordCitation> Citations);

[Serializable, NetSerializable]
public sealed class CitationPaymentConsoleState(List<CitationPaymentPerson> people, bool hasStation) : BoundUserInterfaceState
{
    public readonly List<CitationPaymentPerson> People = people;
    public readonly bool HasStation = hasStation;
}

[Serializable, NetSerializable]
public sealed class CitationPaymentMessage(uint recordId, uint citationId) : BoundUserInterfaceMessage
{
    public readonly uint RecordId = recordId;
    public readonly uint CitationId = citationId;
}

[Serializable, NetSerializable]
public sealed class CitationPaymentRefreshMessage : BoundUserInterfaceMessage
{
}
