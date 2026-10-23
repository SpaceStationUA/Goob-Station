// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Content.Server._Pirate.Triage.MedicalRecords;
using Content.Server.Medical;
using Content.Server.Medical.Components;
using Content.Server.StationRecords.Systems;
using Content.Shared._Pirate.Triage.MedicalRecords;
using Content.Shared.Access.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.StationRecords;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Pirate.Triage;

[TestFixture]
public sealed class MedicalTriageTest
{
    [Test]
    public async Task StatusesReachHudAndExpiredRecordsClearIt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var records = server.System<StationRecordsSystem>();
        var triage = server.System<MedicalRecordsSystem>();
        var timing = server.ResolveDependency<IGameTiming>();
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var station = entities.SpawnEntity(null, map.GridCoords);
            entities.AddComponent<StationRecordsComponent>(station);
            var human = entities.SpawnEntity("MobHuman", map.GridCoords);
            var card = entities.SpawnEntity("MedicalIDCard", map.GridCoords);
            var name = Identity.Name(human, entities);
            entities.GetComponent<IdCardComponent>(card).FullName = name;
            Assert.That(server.System<InventorySystem>().TryEquip(human, card, "id", force: true), Is.True);
            var key = records.AddRecordEntry(station, new GeneralStationRecord { Name = name });
            entities.EnsureComponent<StationRecordKeyStorageComponent>(card).Key = key;
            Assert.That(triage.GetMedicalRecords(human), Is.Not.Null);

            foreach (var status in new[] { TriageStatus.Minor, TriageStatus.Delayed, TriageStatus.Immediate, TriageStatus.Expectant })
            {
                triage.SetPatientStatus(key, status);
                Assert.That(triage.GetMedicalRecords(human)!.Status, Is.EqualTo(status));
                Assert.That(entities.GetComponent<MedicalRecordComponent>(human).Record.Status, Is.EqualTo(status));
            }
            triage.SetPatientStatus(key, (TriageStatus) 255);
            Assert.That(triage.GetMedicalRecords(human)!.Status, Is.EqualTo(TriageStatus.Expectant));

            var record = triage.GetMedicalRecords(human)!;
            triage.SetStatus(key, record with { LastUpdated = timing.CurTime - TimeSpan.FromMinutes(5) + TimeSpan.FromMilliseconds(1) });
            Assert.That(triage.GetMedicalRecords(human)!.Status, Is.EqualTo(TriageStatus.Expectant));
            triage.SetStatus(key, record with { LastUpdated = timing.CurTime - TimeSpan.FromMinutes(5), ClaimedName = "Doctor Ada" });
            var expired = triage.GetMedicalRecords(human)!;
            Assert.That(expired.Status, Is.EqualTo(TriageStatus.None));
            Assert.That(expired.ClaimedName, Is.Null);
            Assert.That(expired.LastUpdated, Is.Null);
            Assert.That(entities.HasComponent<MedicalRecordComponent>(human), Is.False);
            entities.DeleteEntity(human);
            entities.DeleteEntity(station);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(TriageStatus.None)]
    [TestCase(TriageStatus.Immediate)]
    [TestCase(TriageStatus.Expectant)]
    public async Task ClaimRequiresMedicalAccessAndTogglesForTheSameDoctor(TriageStatus status)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var records = server.System<StationRecordsSystem>();
        var triage = server.System<MedicalRecordsSystem>();
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var station = entities.SpawnEntity(null, map.GridCoords);
            entities.AddComponent<StationRecordsComponent>(station);
            var key = records.AddRecordEntry(station, new GeneralStationRecord { Name = "Triage patient" });
            triage.SetPatientStatus(key, status);
            var doctor = entities.SpawnEntity("MobHuman", map.GridCoords);
            var card = entities.SpawnEntity("MedicalIDCard", map.GridCoords);
            var id = entities.GetComponent<IdCardComponent>(card);
            id.FullName = "Doctor Ada";
            var access = entities.GetComponent<AccessComponent>(card);
            access.Tags.Remove("Medical");
            triage.ClaimPatient(key, doctor);
            AssertUnclaimed();
            Assert.That(server.System<InventorySystem>().TryEquip(doctor, card, "id", force: true), Is.True);
            triage.ClaimPatient(key, doctor);
            AssertUnclaimed();
            access.Tags.Add("Medical");
            id.FullName = "";
            triage.ClaimPatient(key, doctor);
            AssertUnclaimed();

            id.FullName = "Doctor Ada";
            triage.ClaimPatient(key, doctor);
            Assert.That(records.TryGetRecord<MedicalRecord>(key, out var claimed), Is.True);
            Assert.That(claimed!.ClaimedName, Is.EqualTo("Doctor Ada"));
            Assert.That(claimed.Status, Is.EqualTo(status));
            Assert.That(claimed.LastUpdated, Is.Not.Null);
            triage.ClaimPatient(key, doctor);
            AssertUnclaimed();
            Assert.That(records.TryGetRecord<MedicalRecord>(key, out var unclaimed), Is.True);
            Assert.That(unclaimed!.Status, Is.EqualTo(status));
            Assert.That(unclaimed.LastUpdated, Is.EqualTo(status == TriageStatus.None ? null : claimed.LastUpdated));
            entities.DeleteEntity(doctor);
            entities.DeleteEntity(station);

            void AssertUnclaimed()
            {
                Assert.That(records.TryGetRecord<MedicalRecord>(key, out var record), Is.True);
                Assert.That(record!.ClaimedName, Is.Null);
            }
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(TriageStatus.None)]
    [TestCase(TriageStatus.Immediate)]
    [TestCase(TriageStatus.Expectant)]
    public async Task ExpiredHudRecordsClearWithoutAnotherAnalyzerScan(TriageStatus status)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var records = server.System<StationRecordsSystem>();
        var triage = server.System<MedicalRecordsSystem>();
        var timing = server.ResolveDependency<IGameTiming>();
        var map = await pair.CreateTestMap();
        EntityUid station = default, expiredPatient = default, freshPatient = default;
        StationRecordKey expiredKey = default, freshKey = default;
        await server.WaitAssertion(() =>
        {
            station = entities.SpawnEntity(null, map.GridCoords);
            entities.AddComponent<StationRecordsComponent>(station);
            expiredPatient = entities.SpawnEntity("MobHuman", map.GridCoords);
            freshPatient = entities.SpawnEntity("MobHuman", map.GridCoords);
            server.System<MetaDataSystem>().SetEntityName(expiredPatient, "Expired triage patient");
            server.System<MetaDataSystem>().SetEntityName(freshPatient, "Fresh triage patient");
        });
        await pair.RunTicksSync(1); // Identity names are refreshed after spawning and renaming.
        await server.WaitAssertion(() =>
        {
            Assert.That(Identity.Name(expiredPatient, entities), Is.Not.EqualTo(Identity.Name(freshPatient, entities)));
            expiredKey = records.AddRecordEntry(station, new GeneralStationRecord { Name = Identity.Name(expiredPatient, entities) });
            freshKey = records.AddRecordEntry(station, new GeneralStationRecord { Name = Identity.Name(freshPatient, entities) });
            triage.SetStatus(expiredKey, new MedicalRecord
            {
                Status = status, ClaimedName = "Doctor Ada",
                LastUpdated = timing.CurTime - TimeSpan.FromMinutes(5) + TimeSpan.FromMilliseconds(1)
            });
            triage.SetStatus(freshKey, new MedicalRecord
            {
                Status = status, ClaimedName = "Doctor Ada", LastUpdated = timing.CurTime
            });
            Assert.That(entities.HasComponent<MedicalRecordComponent>(expiredPatient), Is.True);
            Assert.That(entities.HasComponent<MedicalRecordComponent>(freshPatient), Is.True);
        });

        await pair.RunTicksSync(60); // Let the server expire the record without accessing it through an analyzer.
        await server.WaitAssertion(() =>
        {
            Assert.That(records.TryGetRecord<MedicalRecord>(expiredKey, out var expired), Is.True);
            Assert.That(expired!.Status, Is.EqualTo(TriageStatus.None));
            Assert.That(expired.ClaimedName, Is.Null);
            Assert.That(expired.LastUpdated, Is.Null);
            Assert.That(entities.HasComponent<MedicalRecordComponent>(expiredPatient), Is.False);
            Assert.That(records.TryGetRecord<MedicalRecord>(freshKey, out var fresh), Is.True);
            Assert.That(fresh!.Status, Is.EqualTo(status));
            Assert.That(fresh.ClaimedName, Is.EqualTo("Doctor Ada"));
            Assert.That(entities.HasComponent<MedicalRecordComponent>(freshPatient), Is.True);
            entities.DeleteEntity(expiredPatient);
            entities.DeleteEntity(freshPatient);
            entities.DeleteEntity(station);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AnalyzerRejectsInactiveDistantDeletedOrUnrecordedPatients()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var records = server.System<StationRecordsSystem>();
        var analyzerSystem = server.System<HealthAnalyzerSystem>();
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var station = entities.SpawnEntity(null, map.GridCoords);
            entities.AddComponent<StationRecordsComponent>(station);
            var human = entities.SpawnEntity("MobHuman", map.GridCoords);
            var card = entities.SpawnEntity("MedicalIDCard", map.GridCoords);
            var key = records.AddRecordEntry(station, new GeneralStationRecord { Name = Identity.Name(human, entities) });
            entities.EnsureComponent<StationRecordKeyStorageComponent>(card).Key = key;
            Assert.That(server.System<InventorySystem>().TryEquip(human, card, "id", force: true), Is.True);
            var analyzer = entities.SpawnEntity("HandheldHealthAnalyzer", map.GridCoords);
            var comp = entities.GetComponent<HealthAnalyzerComponent>(analyzer);
#pragma warning disable RA0002 // Fixture sets the scan lifecycle state under test.
            comp.ScannedEntity = human;
#pragma warning restore RA0002
#pragma warning disable RA0002 // Fixture sets the scan lifecycle state under test.
            comp.IsAnalyzerActive = true;
#pragma warning restore RA0002
            Assert.That(analyzerSystem.TryGetTriagePatient((analyzer, comp), out var patient, out var found), Is.True);
            Assert.That(patient, Is.EqualTo(human));
            Assert.That(found, Is.EqualTo(key));
#pragma warning disable RA0002 // Fixture sets the scan lifecycle state under test.
            comp.IsAnalyzerActive = false;
#pragma warning restore RA0002
            Assert.That(analyzerSystem.TryGetTriagePatient((analyzer, comp), out _, out _), Is.False);
#pragma warning disable RA0002 // Fixture sets the scan lifecycle state under test.
            comp.IsAnalyzerActive = true;
#pragma warning restore RA0002
            var transform = server.System<SharedTransformSystem>();
            transform.SetCoordinates(human, map.GridCoords.Offset(new System.Numerics.Vector2(10, 0)));
            Assert.That(analyzerSystem.TryGetTriagePatient((analyzer, comp), out _, out _), Is.False);
            transform.SetCoordinates(human, map.GridCoords);
            entities.GetComponent<StationRecordKeyStorageComponent>(card).Key = null;
            Assert.That(analyzerSystem.TryGetTriagePatient((analyzer, comp), out _, out _), Is.False);
            entities.DeleteEntity(human);
            Assert.That(analyzerSystem.TryGetTriagePatient((analyzer, comp), out _, out _), Is.False);
#pragma warning disable RA0002 // Fixture sets the scan lifecycle state under test.
            comp.ScannedEntity = null;
#pragma warning restore RA0002
            Assert.That(analyzerSystem.TryGetTriagePatient((analyzer, comp), out _, out _), Is.False);
            entities.DeleteEntity(analyzer);
            entities.DeleteEntity(station);
        });
        await pair.CleanReturnAsync();
    }
}
