// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Triage.MedicalRecords;
using Content.Shared.Access.Systems;
using Content.Shared.IdentityManagement;
using Content.Shared.StationRecords;
using Content.Server.StationRecords.Systems;
using Content.Server.Access.Systems;
using Content.Shared.Access.Components;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.Triage.MedicalRecords;

public sealed class MedicalRecordsSystem : SharedMedicalRecordsSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StationRecordsSystem _records = default!;
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly IdCardSystem _idCard = default!;
    private static readonly TimeSpan ExpirationTime = TimeSpan.FromMinutes(5);
    private TimeSpan _nextExpirationCheck;
    private readonly List<(StationRecordKey Key, MedicalRecord Record)> _expiredRecords = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AfterGeneralRecordCreatedEvent>(OnGeneralRecordCreated);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextExpirationCheck)
            return;

        _nextExpirationCheck = _timing.CurTime + TimeSpan.FromSeconds(1);
        _expiredRecords.Clear();
        var query = EntityQueryEnumerator<StationRecordsComponent>();
        while (query.MoveNext(out var station, out var records))
        {
            foreach (var (id, record) in _records.GetRecordsOfType<MedicalRecord>(station, records))
            {
                if (IsExpired(record))
                    _expiredRecords.Add((new StationRecordKey(id, station), record));
            }
        }

        // Update the station records and HUD together, after enumerating the record tables.
        foreach (var (key, record) in _expiredRecords)
            SetStatus(key, ClearExpired(record));
    }

    private bool IsExpired(MedicalRecord record)
        => record.LastUpdated is {} updated && _timing.CurTime - updated >= ExpirationTime;

    private static MedicalRecord ClearExpired(MedicalRecord record)
        => record with { Status = TriageStatus.None, ClaimedName = null, LastUpdated = null };

    private void OnGeneralRecordCreated(AfterGeneralRecordCreatedEvent evt)
    {
        _records.AddRecordEntry(evt.Key, new MedicalRecord());
        _records.Synchronize(evt.Key);
    }

    public void SetStatus(StationRecordKey key, MedicalRecord record)
    {
        var name = _records.RecordName(key);
        if (!string.IsNullOrEmpty(name))
            UpdateMedicalRecords(name, record);

        _records.AddRecordEntry(key, record);
        _records.Synchronize(key);
    }

    public MedicalRecord? GetMedicalRecords(EntityUid patient)
    {
        _access.FindStationRecordKeys(patient, out var keys);
        foreach (var key in keys)
        {
            if (_records.TryGetRecord<MedicalRecord>(key, out var record))
            {
                if (IsExpired(record))
                {
                    record = ClearExpired(record);
                    SetStatus(key, record);
                }
                return record;
            }
        }
        foreach (var key in keys)
        {
            var medicalRecord = new MedicalRecord();
            SetStatus(key, medicalRecord);
            return medicalRecord;
        }
        return null;
    }

    public StationRecordKey? GetMedicalRecordsKey(EntityUid patient)
    {
        _access.FindStationRecordKeys(patient, out var keys);
        foreach (var key in keys)
        {
            if (_records.TryGetRecord<MedicalRecord>(key, out var record))
                return key;
        }
        foreach (var key in keys)
        {
            SetStatus(key, new MedicalRecord());
            return key;
        }
        return null;
    }

    public void SetPatientStatus(StationRecordKey patient, TriageStatus status)
    {
        if (!Enum.IsDefined(status)) // Pirate: reject invalid client enum values.
            return;
        if (status != TriageStatus.None)
        {
            // Records may be created lazily; the first status assignment must still be saved.
            _records.TryGetRecord<MedicalRecord>(patient, out var record);
            SetStatus(patient, (record ?? new MedicalRecord()) with { Status = status, LastUpdated = _timing.CurTime });
        }
        else
        {
            SetStatus(patient, new MedicalRecord());
        }
    }

    public void ClaimPatient(StationRecordKey patient, EntityUid claimer)
    {
        // Require ID card with medical access to claim patient
        if (!_idCard.TryFindIdCard(claimer, out var idCard))
            return;

        // Check if ID card has medical access
        if (!TryComp<AccessComponent>(idCard.Owner, out var access) ||
            !access.Tags.Contains("Medical"))
            return;

        var claimerName = idCard.Comp.FullName;
        if (string.IsNullOrEmpty(claimerName))
            return;

        if (!_records.TryGetRecord<MedicalRecord>(patient, out var record))
            return;

        // Makes claim patient toggleable
        var newClaim = record.ClaimedName == claimerName ? null : claimerName;
        TimeSpan? newTime = newClaim != null
            ? _timing.CurTime
            : record.Status == TriageStatus.None ? null : record.LastUpdated ?? _timing.CurTime;

        SetStatus(patient, record with { ClaimedName = newClaim, LastUpdated = newTime });
    }
}
