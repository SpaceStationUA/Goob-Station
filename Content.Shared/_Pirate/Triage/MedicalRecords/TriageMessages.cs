// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Triage.MedicalRecords;

[Serializable, NetSerializable]
public sealed class HealthAnalyzerTriageStatusMessage(TriageStatus triageStatus) : BoundUserInterfaceMessage
{
    public readonly TriageStatus TriageStatus = triageStatus;
}

[Serializable, NetSerializable]
public sealed class HealthAnalyzerTriageClaimMessage : BoundUserInterfaceMessage;
