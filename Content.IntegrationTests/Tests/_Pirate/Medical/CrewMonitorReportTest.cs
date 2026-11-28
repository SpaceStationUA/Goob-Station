// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Client.Medical.CrewMonitoring;
using Content.Goobstation.Shared.CrewMonitoring;
using Content.Server.Medical.CrewMonitoring;
using Content.Server.Power.Components;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Implants.Components;
using Content.Shared.Interaction;
using Content.Shared.Medical.CrewMonitoring;
using Content.Shared.Station.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Pirate.Medical;

[TestFixture]
public sealed class CrewMonitorReportTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: TestCommandMonitorWearer
          parent: MobHuman
          components:
          - type: CommandStaff
          - type: SuitSensor
            onMob: true
            mode: SensorCords
            randomMode: false
        """;

    [Test]
    public async Task RangeRingsFollowTheSelectedLinkedDeck()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var client = pair.Client;
        await client.WaitAssertion(() =>
        {
            var maps = client.System<SharedMapSystem>();
            var transform = client.System<SharedTransformSystem>();
            var lowerMap = maps.CreateMap(out var lowerId);
            var upperMap = maps.CreateMap(out var upperId);
            try
            {
                var lower = maps.CreateGridEntity(lowerId).Owner;
                var upper = maps.CreateGridEntity(upperId).Owner;
                transform.SetCoordinates(lower, new EntityCoordinates(lowerMap, new Vector2(-10, 20)));
                transform.SetCoordinates(upper, new EntityCoordinates(upperMap, new Vector2(70, -50)));
                transform.SetWorldRotation(upper, Angle.FromDegrees(90));
                using var radar = new CrewMonitoringNavMapControl
                {
                    MapUid = lower,
                    SensorRangeCenter = new EntityCoordinates(lower, new Vector2(3, 5)),
                    SensorRange = 48f,
                };
                var size = new Vector2(400);
                radar.Measure(size);
                radar.Arrange(UIBox2.FromDimensions(Vector2.Zero, size));
                radar.ForceNavMapUpdate();

                Assert.That(radar.TryGetSensorViewOrigin(out var original, out var range), Is.True);
                Assert.That(range, Is.EqualTo(48f));
                radar.MapUid = upper;
                radar.ForceNavMapUpdate();
                Assert.That(radar.TryGetSensorViewOrigin(out _, out _), Is.False, "An unrelated map has no coverage.");

                client.EntMan.EnsureComponent<CEZLinkedGridComponent>(lower).PeerGrids[1] = upper;
                Assert.That(radar.TryGetSensorViewOrigin(out var projected, out range), Is.True);
                Assert.That(Vector2.Distance(projected, original), Is.LessThan(0.01f));
                Assert.That(range, Is.EqualTo(48f));

                radar.RecenterToConnectedServer();
                Assert.That(radar.TryGetSensorViewOrigin(out var centered, out _), Is.True);
                Assert.That(Vector2.Distance(centered, radar.PixelSize / 2f), Is.LessThan(0.01f));
                Assert.That(radar.MapUid, Is.EqualTo(upper));

                radar.SetGridTransformsFrozen(true);
                radar.MapUid = lower;
                radar.ForceNavMapUpdate();
                radar.SetGridTransformsFrozen(true);
                radar.RecenterToConnectedServer();
                Assert.That(Vector2.Distance(radar.Offset, new Vector2(3, 5)), Is.LessThan(0.01f),
                    "Recentering an offline monitor uses the snapshot for the newly displayed deck.");
            }
            finally
            {
                maps.DeleteMap(upperId);
                maps.DeleteMap(lowerId);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CommandTrackerReachesHandheldAcrossLinkedDecks()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var lower = await pair.CreateTestMap();
        var upper = await pair.CreateTestMap();
        var server = pair.Server;
        var em = server.EntMan;
        EntityUid monitor = default, publicMonitor = default, relay = default, commander = default, wearer = default;
        var ui = server.System<SharedUserInterfaceSystem>();
        await server.WaitAssertion(() =>
        {
            var station = em.SpawnEntity(null, lower.GridCoords);
            em.EnsureComponent<StationMemberComponent>(lower.Grid.Owner).Station = station;
            em.EnsureComponent<StationMemberComponent>(upper.Grid.Owner).Station = station;
            relay = em.SpawnEntity("CrewMonitoringServer", lower.GridCoords);
            var power = em.GetComponent<ApcPowerReceiverComponent>(relay);
            power.NeedsPower = false;
            power.Powered = true;

            wearer = em.SpawnEntity("MobHuman", upper.GridCoords);
            commander = em.SpawnEntity("TestCommandMonitorWearer", upper.GridCoords);
            server.PlayerMan.SetAttachedEntity(pair.Player!, wearer);
            monitor = em.SpawnEntity("HandheldCrewMonitorBSO", upper.GridCoords);
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(wearer, monitor), Is.True);

            Assert.That(ui.TryOpenUi(monitor, CrewMonitoringUIKey.Key, wearer), Is.True);
            var console = em.GetComponent<CrewMonitoringConsoleComponent>(monitor);
            em.EventBus.RaiseLocalEvent(monitor, new CrewMonitoringScanCompleteMessage());
            Assert.That(console.CachedServers, Is.Empty, "unrelated maps are separate even at identical coordinates");

            em.EnsureComponent<CEZLinkedGridComponent>(lower.Grid.Owner).PeerGrids[1] = upper.Grid.Owner;
            em.EnsureComponent<CEZLinkedGridComponent>(upper.Grid.Owner).PeerGrids[0] = lower.Grid.Owner;
            em.EventBus.RaiseLocalEvent(monitor, new CrewMonitoringScanCompleteMessage());
            Assert.That(console.CachedServers.Select(entry => entry.NetEntity), Does.Contain(em.GetNetEntity(relay)));
            em.EventBus.RaiseLocalEvent(monitor, new CrewMonitoringSelectServerMessage(em.GetNetEntity(relay)));
            Assert.That(console.SelectedServerUid, Is.EqualTo(relay));

            publicMonitor = em.SpawnEntity("HandheldCrewMonitor", upper.GridCoords);
            Assert.That(ui.TryOpenUi(publicMonitor, CrewMonitoringUIKey.Key, wearer), Is.True);
            em.EventBus.RaiseLocalEvent(publicMonitor, new CrewMonitoringScanCompleteMessage());
            em.EventBus.RaiseLocalEvent(publicMonitor, new CrewMonitoringSelectServerMessage(em.GetNetEntity(relay)));

            // Complete the normal interaction with the scanner in the user's hand.
            em.GetComponent<CrewMonitorScanningComponent>(monitor).DoAfterTime = TimeSpan.Zero;
            em.EventBus.RaiseLocalEvent(monitor, new AfterInteractEvent(wearer, monitor, commander, upper.GridCoords, true));
        });
        await pair.RunTicksSync(60);
        await server.WaitAssertion(() =>
        {
            Assert.That(ui.TryGetUiState<CrewMonitoringState>(monitor, CrewMonitoringUIKey.Key, out var state), Is.True);
            Assert.That(state!.ServerOnline && state.CommandTrackersOnly, Is.True);
            Assert.That(state.Sensors, Has.Count.EqualTo(1));
            Assert.That(ui.TryGetUiState<CrewMonitoringState>(publicMonitor, CrewMonitoringUIKey.Key, out var publicState), Is.True);
            Assert.That(publicState!.CommandTrackersOnly, Is.False);
            Assert.That(publicState.Sensors.Any(s => s.IsCommandTracker), Is.False);
            Assert.That(publicState.Sensors.Any(s => s.OwnerUid == em.GetNetEntity(commander)), Is.True);

            var tracker = state.Sensors.Single(s => s.IsCommandTracker && s.OwnerUid == em.GetNetEntity(commander));
            Assert.That(tracker.IsActive, Is.True);
            Assert.That(tracker.Coordinates?.NetEntity, Is.EqualTo(em.GetNetEntity(upper.Grid.Owner)));
            var implants = em.GetComponent<ImplantedComponent>(commander).ImplantContainer;
            var first = implants.ContainedEntities.Single(id => em.GetComponent<MetaDataComponent>(id).EntityPrototype?.ID == "CommandTrackingImplant");
            em.DeleteEntity(first);
            em.EventBus.RaiseLocalEvent(monitor, new AfterInteractEvent(wearer, monitor, commander, upper.GridCoords, true));
        });
        await pair.RunTicksSync(60);
        await server.WaitAssertion(() =>
        {
            var implants = em.GetComponent<ImplantedComponent>(commander).ImplantContainer;
            Assert.That(implants.ContainedEntities.Count(id => em.GetComponent<MetaDataComponent>(id).EntityPrototype?.ID == "CommandTrackingImplant"), Is.EqualTo(1));
            var transform = server.System<SharedTransformSystem>();
            transform.SetCoordinates(relay, upper.GridCoords);
            transform.SetCoordinates(wearer, lower.GridCoords);
            em.EventBus.RaiseLocalEvent(monitor, new CrewMonitoringScanCompleteMessage());
            var console = em.GetComponent<CrewMonitoringConsoleComponent>(monitor);
            Assert.That(console.CachedServers.Select(entry => entry.NetEntity), Does.Contain(em.GetNetEntity(relay)));
            Assert.That(console.SelectedServerUid, Is.EqualTo(relay));

            transform.SetCoordinates(wearer, new EntityCoordinates(lower.Grid.Owner, new Vector2(1000, 0)));
            em.EventBus.RaiseLocalEvent(monitor, new CrewMonitoringScanCompleteMessage());
            Assert.That(em.GetComponent<CrewMonitoringConsoleComponent>(monitor).CachedServers, Is.Empty);
        });
        await pair.CleanReturnAsync();
    }
}
