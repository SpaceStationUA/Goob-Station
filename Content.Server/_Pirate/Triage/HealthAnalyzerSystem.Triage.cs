// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.Triage.MedicalRecords;
using Content.Server.Medical.Components;
using Content.Shared._Pirate.Triage.MedicalRecords;
using Content.Shared.MedicalScanner;
using Content.Shared.StationRecords;

namespace Content.Server.Medical;

public sealed partial class HealthAnalyzerSystem
{
    [Dependency] private readonly MedicalRecordsSystem _medicalRecords = default!;

    private void InitializeTriage()
    {
        Subs.BuiEvents<HealthAnalyzerComponent>(HealthAnalyzerUiKey.Key, subs =>
        {
            subs.Event<HealthAnalyzerTriageStatusMessage>(OnTriageStatus);
            subs.Event<HealthAnalyzerTriageClaimMessage>(OnTriageClaim);
        });
    }

    private void OnTriageStatus(Entity<HealthAnalyzerComponent> ent, ref HealthAnalyzerTriageStatusMessage args)
    {
        if (!Enum.IsDefined(args.TriageStatus) || !TryGetTriagePatient(ent, out var patient, out var key))
            return;
        _medicalRecords.SetPatientStatus(key, args.TriageStatus);
        SendTriageUpdate(ent, patient, true);
    }

    private void OnTriageClaim(Entity<HealthAnalyzerComponent> ent, ref HealthAnalyzerTriageClaimMessage args)
    {
        if (!TryGetTriagePatient(ent, out var patient, out var key))
            return;
        _medicalRecords.ClaimPatient(key, args.Actor);
        SendTriageUpdate(ent, patient, true);
    }

    /// <summary>
    /// Pirate: resolve the current scan at request time instead of trusting a stale record key.
    /// </summary>
    public bool TryGetTriagePatient(Entity<HealthAnalyzerComponent> ent, out EntityUid patient, out StationRecordKey key)
    {
        patient = default;
        key = default;
        if (!ent.Comp.IsAnalyzerActive || ent.Comp.ScannedEntity is not {} target || TerminatingOrDeleted(target) ||
            (ent.Comp.MaxScanRange is {} range &&
             !_transformSystem.InRange(Transform(target).Coordinates, Transform(ent).Coordinates, range)) ||
            _medicalRecords.GetMedicalRecordsKey(target) is not {} recordKey)
            return false;
        patient = target;
        key = recordKey;
        return true;
    }

    private void SendTriageUpdate(EntityUid analyzer, EntityUid patient, bool active)
    {
        _uiSystem.ServerSendUiMessage(analyzer, HealthAnalyzerUiKey.Key,
            new HealthAnalyzerTriageUpdateMessage(GetNetEntity(patient), _medicalRecords.GetMedicalRecords(patient), active));
    }
}
