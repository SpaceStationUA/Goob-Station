// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Atmos.Piping.Components;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Piping.Binary.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.NodeContainer;
using Robust.Shared.GameObjects;
using GasCanisterComponent = Content.Shared.Atmos.Piping.Unary.Components.GasCanisterComponent;

namespace Content.IntegrationTests.Tests._Pirate.Newbies;

[TestFixture]
public sealed class NoviceCanisterTest
{
    [TestCase(0, true, "AirCanister")]
    [TestCase(23.99, true, "PlasmaCanister")]
    [TestCase(24, false, "AirCanister")]
    [TestCase(25, false, "PlasmaCanister")]
    public async Task RoomReleaseRequires24Hours(double hours, bool restricted, string prototype)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var user = entities.SpawnEntity("MobHuman", map.GridCoords);
            server.PlayerMan.SetAttachedEntity(pair.Player!, user);
            var tracking = server.ResolveDependency<PlayTimeTrackingManager>();
            NoviceTestPlaytime.Set(tracking, pair.Player!, hours);
            var canister = entities.SpawnEntity(prototype, map.GridCoords);
            var gas = entities.GetComponent<GasCanisterComponent>(canister);
            SetValve(entities, canister, user, true);
            Assert.That(gas.ReleaseValve, Is.EqualTo(!restricted));
            SetValve(entities, canister, user, false);
            Assert.That(gas.ReleaseValve, Is.False, "Closing must always be permitted.");

            NoviceTestPlaytime.SetLoaded(tracking, pair.Player!, false);
            try
            {
                SetValve(entities, canister, user, true);
                Assert.That(gas.ReleaseValve, Is.False, "Unknown playtime must not bypass the restriction.");
            }
            finally
            {
                NoviceTestPlaytime.SetLoaded(tracking, pair.Player!, true);
            }
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task FillingTankAllowedButRemovalClosesValve(bool forcedRemoval)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid user = default, canister = default, tank = default;
        await server.WaitAssertion(() =>
        {
            user = entities.SpawnEntity("MobHuman", map.GridCoords);
            server.PlayerMan.SetAttachedEntity(pair.Player!, user);
            NoviceTestPlaytime.Set(server.ResolveDependency<PlayTimeTrackingManager>(), pair.Player!, 0);
            canister = entities.SpawnEntity("AirCanister", map.GridCoords);
            tank = entities.SpawnEntity("JetpackBlue", map.GridCoords);
            var gas = entities.GetComponent<GasCanisterComponent>(canister);
            Assert.That(server.System<ItemSlotsSystem>().TryInsert(canister, gas.GasTankSlot, tank, null), Is.True);
            SetValve(entities, canister, user, true);
            Assert.That(gas.ReleaseValve, Is.True);
        });
        await pair.RunTicksSync(20);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<GasTankComponent>(tank).Air.Pressure, Is.GreaterThan(0f));
            var gas = entities.GetComponent<GasCanisterComponent>(canister);
            if (forcedRemoval)
                entities.DeleteEntity(tank);
            else
                entities.EventBus.RaiseLocalEvent(canister,
                    new GasCanisterHoldingTankEjectMessage { Actor = user, UiKey = GasCanisterUiKey.Key });

            var before = gas.Air.TotalMoles;
            var update = new AtmosDeviceUpdateEvent(1f, null, null);
            entities.EventBus.RaiseLocalEvent(canister, ref update);
            Assert.That(gas.ReleaseValve, Is.False);
            Assert.That(gas.Air.TotalMoles, Is.EqualTo(before), "No gas may escape before the guard closes the valve.");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ConnectedPipeAllowedAndDisconnectClosesBeforeRelease()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid user = default, canister = default;
        await server.WaitAssertion(() =>
        {
            user = entities.SpawnEntity("MobHuman", map.GridCoords);
            server.PlayerMan.SetAttachedEntity(pair.Player!, user);
            NoviceTestPlaytime.Set(server.ResolveDependency<PlayTimeTrackingManager>(), pair.Player!, 0);
            entities.SpawnEntity("GasPort", map.GridCoords);
            canister = entities.SpawnEntity("AirCanister", map.GridCoords);
            SetValve(entities, canister, user, true);
            Assert.That(entities.GetComponent<GasCanisterComponent>(canister).ReleaseValve, Is.False,
                "Merely standing on a connector is not a connection.");
            server.System<SharedTransformSystem>().AnchorEntity(canister, entities.GetComponent<TransformComponent>(canister));
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            var gas = entities.GetComponent<GasCanisterComponent>(canister);
            var port = (PortablePipeNode) entities.GetComponent<NodeContainerComponent>(canister).Nodes[gas.PortName];
            Assert.That(port.NodeGroup!.Nodes.Count, Is.GreaterThan(1), "The test must connect an actual pipe network.");
            SetValve(entities, canister, user, true);
            Assert.That(gas.ReleaseValve, Is.True);
            server.System<SharedTransformSystem>().Unanchor(canister, entities.GetComponent<TransformComponent>(canister));
            var update = new AtmosDeviceUpdateEvent(1f, null, null);
            entities.EventBus.RaiseLocalEvent(canister, ref update);
            Assert.That(gas.ReleaseValve, Is.False);
            // The old pipe net can survive until the next node update, so only assert no ambient release here.
            Assert.That(gas.Air.TotalMoles, Is.GreaterThan(0f));
        });
        await pair.CleanReturnAsync();
    }

    private static void SetValve(IEntityManager entities, EntityUid canister, EntityUid actor, bool open)
    {
        entities.EventBus.RaiseLocalEvent(canister,
            new GasCanisterChangeReleaseValveMessage(open) { Actor = actor, UiKey = GasCanisterUiKey.Key });
    }
}
