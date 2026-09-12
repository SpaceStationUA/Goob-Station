// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Server._Pirate.MalfAI;
using Content.Server.Power.Components;
using Content.Server.Store.Systems;
using Content.Shared._Pirate.MalfAI;
using Content.Shared._Pirate.MalfAI.Actions;
using Content.Shared.CCVar;
using Content.Shared.Actions.Components;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Electrocution;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Pirate;

[TestFixture]
public sealed class MalfAiEconomyDisruptionTest
{
    [Test]
    public async Task ApcSiphonPaysCpuOnceDisablesPowerAndRestoresOriginalState()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        var siphon = server.System<MalfAiApcSiphonSystem>();
        EntityUid ai = default;
        EntityUid apc = default;

        await server.WaitAssertion(() =>
        {
            ai = entMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(1, 0)));
            entMan.EnsureComponent<MalfAiMarkerComponent>(ai);
            entMan.EnsureComponent<StationAiHeldComponent>(ai);
            var store = entMan.EnsureComponent<StoreComponent>(ai);
            store.CurrencyWhitelist.Add("CPU");
            store.Balance["CPU"] = FixedPoint2.Zero;

            apc = entMan.SpawnEntity("APCBasic", map.GridCoords.Offset(new Vector2(2, 0)));
            server.CfgMan.SetCVar(CCVars.MalfAiSiphonDurationSeconds, 0.1f);
        });

        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            var apcComp = entMan.GetComponent<ApcComponent>(apc);
            var battery = entMan.GetComponent<PowerNetworkBatteryComponent>(apc);
            var store = entMan.GetComponent<StoreComponent>(ai);

            Assert.That(apcComp.MainBreakerEnabled, Is.True);
            Assert.That(siphon.TrySiphon(apc, ai), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(store.Balance["CPU"].Float(), Is.EqualTo(5f));
                Assert.That(apcComp.MainBreakerEnabled, Is.False);
                Assert.That(battery.CanDischarge, Is.False);
                Assert.That(entMan.HasComponent<MalfAiApcSiphonedComponent>(apc), Is.True);
                Assert.That(siphon.TrySiphon(apc, ai), Is.False, "A siphoned APC must not pay CPU twice.");
            });
        });

        await pair.RunSeconds(0.25f);
        await server.WaitAssertion(() =>
        {
            var apcComp = entMan.GetComponent<ApcComponent>(apc);
            var battery = entMan.GetComponent<PowerNetworkBatteryComponent>(apc);
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<MalfAiApcSiphonedComponent>(apc), Is.False);
                Assert.That(apcComp.MainBreakerEnabled, Is.True);
                Assert.That(battery.CanDischarge, Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StorePurchaseRequiresCpuAndEnablesCameraUpgrade()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        var storeSystem = server.System<StoreSystem>();
        EntityUid ai = default;

        await server.WaitAssertion(() =>
        {
            ai = entMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(1, 0)));
            entMan.EnsureComponent<MalfAiMarkerComponent>(ai);
            entMan.EnsureComponent<StationAiHeldComponent>(ai);
            var store = entMan.EnsureComponent<StoreComponent>(ai);
            store.Categories.Add("MalfAI");
            store.CurrencyWhitelist.Add("CPU");
            storeSystem.RefreshAllListings(store);

            var listing = store.Listings.Single(item => item.ID == "MalfAiToggleCameraUpgrade");
            var denied = new StoreBuyListingMessage(listing) { Actor = ai };
            entMan.EventBus.RaiseLocalEvent(ai, denied);
            Assert.Multiple(() =>
            {
                Assert.That(store.Balance.TryGetValue("CPU", out var balance) ? balance.Float() : 0f, Is.Zero);
                Assert.That(listing.PurchaseAmount, Is.Zero);
                Assert.That(entMan.HasComponent<MalfAiCameraUpgradeComponent>(ai), Is.False);
            });

            store.Balance["CPU"] = FixedPoint2.New(50);
            var purchase = new StoreBuyListingMessage(listing) { Actor = ai };
            entMan.EventBus.RaiseLocalEvent(ai, purchase);
            var upgrade = entMan.GetComponent<MalfAiCameraUpgradeComponent>(ai);
            Assert.Multiple(() =>
            {
                Assert.That(store.Balance["CPU"].Float(), Is.Zero);
                Assert.That(listing.PurchaseAmount, Is.EqualTo(1));
                Assert.That(upgrade.EnabledDesired, Is.True);
                Assert.That(upgrade.EnabledEffective, Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("MalfAiOverloadMachine", 50)]
    [TestCase("MalfAiOverrideMachine", 75)]
    public async Task RepeatPurchasePreservesEveryPaidCharge(string listingId, int price)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var ai = entMan.SpawnEntity(null, map.GridCoords);
            var store = entMan.EnsureComponent<StoreComponent>(ai);
            store.Categories.Add("MalfAI");
            store.CurrencyWhitelist.Add("CPU");
            store.Balance["CPU"] = FixedPoint2.New(price * 3);
            server.System<StoreSystem>().RefreshAllListings(store);
            var listing = store.Listings.Single(item => item.ID == listingId);

            entMan.EventBus.RaiseLocalEvent(ai, new StoreBuyListingMessage(listing) { Actor = ai });
            var action = entMan.GetComponent<ActionsComponent>(ai).Actions.Single();
            var charges = entMan.GetComponent<LimitedChargesComponent>(action);
            var system = entMan.System<SharedChargesSystem>();
            Assert.That(system.GetCurrentCharges((action, charges)), Is.EqualTo(2));

            entMan.EventBus.RaiseLocalEvent(ai, new StoreBuyListingMessage(listing) { Actor = ai });
            Assert.That(system.GetCurrentCharges((action, charges)), Is.EqualTo(4));
            system.AddCharges((action, charges), -1);
            entMan.EventBus.RaiseLocalEvent(ai, new StoreBuyListingMessage(listing) { Actor = ai });
            Assert.Multiple(() =>
            {
                Assert.That(system.GetCurrentCharges((action, charges)), Is.EqualTo(5));
                Assert.That(store.Balance["CPU"].Float(), Is.Zero);
                Assert.That(entMan.GetComponent<ActionsComponent>(ai).Actions.Count, Is.EqualTo(1));
            });
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task LockdownBoltsElectrifiesAndRestoresGridDoors(bool initiallyBolted, bool overlapping)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        EntityUid ai = default;
        EntityUid door = default;

        await server.WaitAssertion(() =>
        {
            ai = entMan.SpawnEntity(null, map.GridCoords);
            entMan.EnsureComponent<StoreComponent>(ai);
            door = entMan.SpawnEntity("Airlock", map.GridCoords);
            Assert.That(entMan.GetComponent<TransformComponent>(ai).GridUid, Is.EqualTo(map.Grid.Owner));
            Assert.That(entMan.GetComponent<TransformComponent>(door).GridUid, Is.EqualTo(map.Grid.Owner));
        });

        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            entMan.System<SharedDoorSystem>().TrySetBoltDown(
                (door, entMan.GetComponent<DoorBoltComponent>(door)), initiallyBolted, requirePower: false);
            var doorComp = entMan.GetComponent<DoorComponent>(door);
            var airlock = entMan.GetComponent<AirlockComponent>(door);
            var bolts = entMan.GetComponent<DoorBoltComponent>(door);
            var electrified = entMan.GetComponent<ElectrifiedComponent>(door);
            Assert.Multiple(() =>
            {
                Assert.That(doorComp.State, Is.EqualTo(DoorState.Closed));
                Assert.That(airlock.Safety, Is.True);
                Assert.That(bolts.BoltsDown, Is.EqualTo(initiallyBolted));
                Assert.That(electrified.Enabled, Is.False);
            });

            var lockdown = new MalfAiLockdownGridActionEvent
            {
                Performer = ai,
                Duration = 0.1f,
            };
            entMan.EventBus.RaiseLocalEvent(ai, lockdown);
            if (overlapping)
                entMan.EventBus.RaiseLocalEvent(ai, new MalfAiLockdownGridActionEvent { Performer = ai, Duration = 0.5f });
            Assert.Multiple(() =>
            {
                Assert.That(lockdown.Handled, Is.True);
                Assert.That(bolts.BoltsDown, Is.True);
                Assert.That(airlock.Safety, Is.False);
                Assert.That(electrified.Enabled, Is.True);
            });
        });

        await pair.RunSeconds(0.25f);
        if (overlapping)
        {
            await server.WaitAssertion(() =>
            {
                Assert.That(entMan.GetComponent<ElectrifiedComponent>(door).Enabled, Is.True);
                Assert.That(entMan.GetComponent<AirlockComponent>(door).Safety, Is.False);
                Assert.That(entMan.GetComponent<MalfAiLockdownDoorComponent>(door).ActiveLockdowns, Is.EqualTo(1));
            });
            await pair.RunSeconds(0.5f);
        }
        await server.WaitAssertion(() =>
        {
            var airlock = entMan.GetComponent<AirlockComponent>(door);
            var bolts = entMan.GetComponent<DoorBoltComponent>(door);
            var electrified = entMan.GetComponent<ElectrifiedComponent>(door);
            Assert.Multiple(() =>
            {
                Assert.That(bolts.BoltsDown, Is.EqualTo(initiallyBolted));
                Assert.That(airlock.Safety, Is.True);
                Assert.That(electrified.Enabled, Is.False);
                Assert.That(entMan.GetComponent<DoorComponent>(door).State, Is.EqualTo(DoorState.Closed));
            });
        });

        await pair.CleanReturnAsync();
    }
}
