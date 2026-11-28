// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Goobstation.Shared.Mind.Components;
using Content.Server._Pirate.Blueshield;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Jobs;
using Content.Server.Revolutionary.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._DV.Carrying;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.CCVar;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles;
using Content.Shared.Shuttles.Components;
using Content.Shared.Station.Components;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests.Tests._Pirate.Blueshield;

[TestFixture]
public sealed class BlueshieldLifelineTest
{
    private static readonly ProtoId<JobPrototype> BlueshieldJob = "BlueshieldOfficer";

    [Test]
    public async Task RescueOnActualCentcommAfterShuttleDeparture()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var em = server.EntMan;
        server.CfgMan.SetCVar(CCVars.EmergencyShuttleEnabled, true);
        server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
        server.CfgMan.SetCVar(CCVars.GameMap, "Saltern");
        await server.WaitPost(() => server.System<GameTicker>().RestartRound());
        await pair.RunTicksSync(25);
        server.CfgMan.SetCVar(CCVars.EmergencyShuttleDockTime, 2f);
        await pair.WaitCommand("callshuttle 0:02");
        await pair.RunSeconds(5);

        await server.WaitAssertion(() =>
        {
            var (station, cc) = em.AllComponentsList<StationCentcommComponent>().Single();
            var centcomm = cc.MapEntity!.Value;
            var shuttle = em.GetComponent<StationEmergencyShuttleComponent>(station).EmergencyShuttle!.Value;
            Assert.That(server.System<EmergencyShuttleSystem>().EmergencyShuttleArrived, Is.True);
            Assert.That(em.HasComponent<FTLMapComponent>(server.Transform(shuttle).MapUid), Is.True);

            var stationGrid = em.GetComponent<StationDataComponent>(station).Grids.First(grid =>
                server.Transform(grid).MapID == server.System<GameTicker>().DefaultMap);
            var origin = new EntityCoordinates(stationGrid, 0.5f, 0.5f);
            var officer = em.SpawnAttachedTo("MobHuman", origin);
            var captain = em.SpawnAttachedTo("MobHuman", origin);
            em.EnsureComponent<CommandStaffComponent>(officer);
            em.EnsureComponent<CommandStaffComponent>(captain);
            var minds = server.System<SharedMindSystem>();
            var mind = minds.CreateMind(null);
            minds.TransferTo(mind, captain);
            var passengerBag = em.SpawnAttachedTo("ClothingBackpack", origin);
            Assert.That(server.System<InventorySystem>().TryEquip(captain, passengerBag, "back", silent: true, force: true), Is.True);
            var fakeDisk = em.SpawnAttachedTo("NukeDiskFake", origin);
            Assert.That(server.System<SharedContainerSystem>().Insert(fakeDisk, em.GetComponent<StorageComponent>(passengerBag).Container), Is.True);

            var job = server.ProtoMan.Index(BlueshieldJob);
            job.Special.OfType<AddImplantSpecial>().Single().AfterEquip(officer);
            var actions = server.System<SharedActionsSystem>();
            var action = actions.GetActions(officer).Single(a => em.GetComponent<MetaDataComponent>(a).EntityPrototype?.ID == "ActionActivateBlueshieldLifeline");
            Assert.That(server.System<CarryingSystem>().TryCarry(officer, captain), Is.True);
            var revolution = server.System<RevolutionaryRuleSystem>();
            Assert.That(revolution.CheckCommandLose(), Is.False);

            actions.PerformAction(officer, action);
            var (marker, _) = em.AllComponentsList<BlueshieldLifelineArrivalComponent>().Single();
            Assert.That(server.Transform(officer).Coordinates, Is.EqualTo(server.Transform(marker).Coordinates));
            Assert.That(server.Transform(officer).MapUid, Is.EqualTo(centcomm));
            Assert.That(server.Transform(captain).ParentUid, Is.EqualTo(officer));
            Assert.That(server.Transform(fakeDisk).MapUid, Is.EqualTo(centcomm));
            Assert.That(minds.TryGetMind(captain, out var rescuedMind, out _), Is.True);
            Assert.That(rescuedMind, Is.EqualTo(mind.Owner));
            Assert.That(em.GetComponent<MindLastMobComponent>(mind).LastMob, Is.EqualTo(captain));
            Assert.That(server.System<MobStateSystem>().IsAlive(captain), Is.True);
            Assert.That(revolution.CheckCommandLose(), Is.True);

            // Another surviving head, including an ordinary shuttle evacuee, still counts under the existing rules.
            var head = em.SpawnAttachedTo("MobHuman", server.Transform(marker).Coordinates);
            em.EnsureComponent<CommandStaffComponent>(head);
            Assert.That(revolution.CheckCommandLose(), Is.False);
            em.DeleteEntity(head);

            // Leaving CentComm clears the revolution exception even for someone still carried by the officer.
            server.System<SharedTransformSystem>().SetCoordinates(officer, origin);
            Assert.That(em.HasComponent<LifelineEvacuatedCommandComponent>(captain), Is.False);
            Assert.That(revolution.CheckCommandLose(), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task NestedDiskBlocksUntilManualRetry(bool onPassenger)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var em = server.EntMan;
        var origin = await pair.CreateTestMap();
        var dest = await pair.CreateTestMap();
        EntityUid officer = default, passenger = default, implant = default, action = default;
        await server.WaitAssertion(() =>
        {
            RegisterArrival(em, origin, dest);
            officer = em.SpawnAttachedTo("MobHuman", origin.GridCoords);
            passenger = em.SpawnAttachedTo("MobHuman", origin.GridCoords);
            implant = server.System<SharedSubdermalImplantSystem>().AddImplant(officer, "BlueshieldLifelineImplant")!.Value;
            action = em.GetComponent<SubdermalImplantComponent>(implant).Action!.Value;
            var (disk, box) = EquipDisk(em, onPassenger ? passenger : officer, origin.GridCoords);
            Assert.That(server.System<CarryingSystem>().TryCarry(officer, passenger), Is.True);
            em.System<SharedActionsSystem>().PerformAction(officer, (action, em.GetComponent<ActionComponent>(action)));
            Assert.That(server.Transform(officer).MapUid, Is.EqualTo(origin.MapUid));
            Assert.That(server.Transform(passenger).ParentUid, Is.EqualTo(officer));
            Assert.That(em.IsQueuedForDeletion(implant), Is.False);
            Assert.That(em.HasComponent<PendingBlueshieldLifelineComponent>(implant), Is.False);
            Assert.That(em.System<SharedContainerSystem>().Remove(disk, box, destination: origin.GridCoords), Is.True);
        });

        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.Transform(officer).MapUid, Is.EqualTo(origin.MapUid));
            server.System<SharedActionsSystem>().PerformAction(officer, (action, em.GetComponent<ActionComponent>(action)));
            Assert.That(server.Transform(passenger).MapUid, Is.EqualTo(dest.MapUid));
        });
        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(em.EntityExists(implant), Is.False);
            Assert.That(server.System<SharedActionsSystem>().GetActions(officer).Any(a => a.Owner == action), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task DeathWaitsForDiskUnlessRevived(bool revive)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var em = server.EntMan;
        var origin = await pair.CreateTestMap();
        var dest = await pair.CreateTestMap();
        EntityUid officer = default, implant = default, disk = default;
        await server.WaitAssertion(() =>
        {
            RegisterArrival(em, origin, dest);
            officer = em.SpawnAttachedTo("MobHuman", origin.GridCoords);
            implant = server.System<SharedSubdermalImplantSystem>().AddImplant(officer, "BlueshieldLifelineImplant")!.Value;
            var contents = EquipDisk(em, officer, origin.GridCoords);
            disk = contents.Disk;
            var mobs = server.System<MobStateSystem>();
            mobs.ChangeMobState(officer, MobState.Dead);
            Assert.That(server.Transform(officer).MapUid, Is.EqualTo(origin.MapUid));
            Assert.That(em.HasComponent<PendingBlueshieldLifelineComponent>(implant), Is.True);
            if (revive)
                mobs.ChangeMobState(officer, MobState.Alive);
            Assert.That(em.System<SharedContainerSystem>().Remove(disk, contents.Box, destination: origin.GridCoords), Is.True);
        });
        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.Transform(disk).MapUid, Is.EqualTo(origin.MapUid));
            Assert.That(server.Transform(officer).MapUid, Is.EqualTo(revive ? origin.MapUid : dest.MapUid));
            Assert.That(server.System<MobStateSystem>().IsDead(officer), Is.EqualTo(!revive));
            Assert.That(em.EntityExists(implant), Is.EqualTo(revive));
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SuddenDeathOnlyRescuesPassengerWithoutTheDisk(bool passengerHasDisk)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var em = server.EntMan;
        var origin = await pair.CreateTestMap();
        var dest = await pair.CreateTestMap();
        EntityUid officer = default, passenger = default;
        await server.WaitAssertion(() =>
        {
            RegisterArrival(em, origin, dest);
            officer = em.SpawnAttachedTo("MobHuman", origin.GridCoords);
            passenger = em.SpawnAttachedTo("MobHuman", origin.GridCoords);
            server.System<SharedSubdermalImplantSystem>().AddImplant(officer, "BlueshieldLifelineImplant");
            if (passengerHasDisk)
                EquipDisk(em, passenger, origin.GridCoords);
            Assert.That(server.System<CarryingSystem>().TryCarry(officer, passenger), Is.True);
            server.System<MobStateSystem>().ChangeMobState(officer, MobState.Dead);
        });
        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.Transform(officer).MapUid, Is.EqualTo(dest.MapUid));
            Assert.That(server.Transform(passenger).MapUid, Is.EqualTo(passengerHasDisk ? origin.MapUid : dest.MapUid));
            Assert.That(server.System<MobStateSystem>().IsAlive(passenger), Is.True);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MissingDestinationRetainsChargeThenDeathEvacuates()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var em = server.EntMan;
        var origin = await pair.CreateTestMap();
        var dest = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var officer = em.SpawnAttachedTo("MobHuman", origin.GridCoords);
            var implant = server.System<SharedSubdermalImplantSystem>().AddImplant(officer, "BlueshieldLifelineImplant")!.Value;
            var action = em.GetComponent<SubdermalImplantComponent>(implant).Action!.Value;
            em.SpawnAttachedTo("BlueshieldLifelineArrival", dest.GridCoords); // Unregistered grid.
            server.System<SharedActionsSystem>().PerformAction(officer, (action, em.GetComponent<ActionComponent>(action)));
            Assert.That(server.Transform(officer).MapUid, Is.EqualTo(origin.MapUid));
            Assert.That(em.IsQueuedForDeletion(implant), Is.False);

            RegisterArrival(em, origin, dest);
            server.System<MobStateSystem>().ChangeMobState(officer, MobState.Dead);
            Assert.That(server.Transform(officer).MapUid, Is.EqualTo(dest.MapUid));
            Assert.That(server.System<MobStateSystem>().IsDead(officer), Is.True);
        });
        await pair.CleanReturnAsync();
    }

    private static void RegisterArrival(IEntityManager em, TestMapData origin, TestMapData dest)
    {
        var station = em.EnsureComponent<StationCentcommComponent>(em.SpawnAttachedTo(null, origin.GridCoords));
        station.Entity = dest.Grid.Owner;
        station.MapEntity = dest.MapUid;
        em.SpawnAttachedTo("BlueshieldLifelineArrival", dest.GridCoords.Offset(new Vector2(0.5f)));
    }

    private static (EntityUid Disk, BaseContainer Box) EquipDisk(IEntityManager em, EntityUid host, EntityCoordinates coordinates)
    {
        var backpack = em.SpawnAttachedTo("ClothingBackpack", coordinates);
        Assert.That(em.System<InventorySystem>().TryEquip(host, backpack, "back", silent: true, force: true), Is.True);
        var box = em.SpawnAttachedTo("BoxCardboard", coordinates);
        var containers = em.System<SharedContainerSystem>();
        Assert.That(containers.Insert(box, em.GetComponent<StorageComponent>(backpack).Container), Is.True);
        var disk = em.SpawnAttachedTo("NukeDisk", coordinates);
        var storage = em.GetComponent<StorageComponent>(box).Container;
        Assert.That(containers.Insert(disk, storage), Is.True);
        return (disk, storage);
    }
}
