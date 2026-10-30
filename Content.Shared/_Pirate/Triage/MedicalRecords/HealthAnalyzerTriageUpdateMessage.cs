// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Triage.MedicalRecords;

/// <summary>Pirate: triage state shared by all three local analyzer display modes.</summary>
[Serializable, NetSerializable]
public sealed class HealthAnalyzerTriageUpdateMessage(NetEntity patient, MedicalRecord? record, bool active) : BoundUserInterfaceMessage
{
    public readonly NetEntity Patient = patient;
    public readonly MedicalRecord? Record = record;
    public readonly bool Active = active;
}
