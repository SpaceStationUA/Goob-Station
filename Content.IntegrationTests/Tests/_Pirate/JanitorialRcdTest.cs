// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Numerics;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.RCD;
using Content.Shared.RCD.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Pirate;

public sealed class JanitorialRcdTest : InteractionTest
{
    [TestCase("RCDFloorSteel", "FloorSteel", 1, true)]
    [TestCase("RCDFloorSteelDark", "FloorDark", 1, true)]
    [TestCase("RCDFloorSteelWhite", "FloorWhite", 1, true)]
    [TestCase("RCDFloorTechMaint", "FloorTechMaint", 1, true)]
    [TestCase("RCDMousetrap", "MousetrapArmed", 20, false)]
    [TestCase("RCDWetFloorSign", "WetFloorSign", 20, false)]
    [TestCase("RCDSpaceCleaner", "SpaceCleanerTablet", 20, false)]
    public async Task EveryRecipeBuildsItsProductAndSpendsCharges(string recipe, string product, int cost, bool tile)
    {
        var location = Transform.WithEntityId(new EntityCoordinates(SPlayer, new Vector2(0, 1)), MapData.Grid);
        await SetTile(PlatingRCD, SEntMan.GetNetCoordinates(location), MapData.Grid);
        var rcd = await PlaceInHands("RCDRechargingJanitor");
        var charges = Server.System<SharedChargesSystem>();
        await UseInHand();
        await RunTicks(3);
        Assert.That(IsUiOpen(RcdUiKey.Key), Is.True);
        await SendBui(RcdUiKey.Key, new RCDSystemMessage(recipe), rcd);
        await CloseBui(RcdUiKey.Key, rcd);
        Assert.That(SEntMan.GetComponent<RCDComponent>(ToServer(rcd)).ProtoId, Is.EqualTo((ProtoId<RCDPrototype>) recipe));
        Assert.That(charges.GetCurrentCharges(ToServer(rcd)), Is.EqualTo(20));

        await Interact(null, location);
        await RunSeconds(2);
        if (tile)
            await AssertTile(product, FromServer(location));
        else
        {
            await AssertEntityLookup((product, 1));
            AssertLocation(FromServer(await FindEntity(product)), FromServer(location));
        }
        Assert.That(charges.GetCurrentCharges(ToServer(rcd)), Is.EqualTo(20 - cost));

        // A duplicate tile/object must not consume another charge or produce another entity.
        await Interact(null, location);
        await RunSeconds(2);
        Assert.That(charges.GetCurrentCharges(ToServer(rcd)), Is.EqualTo(20 - cost));
        if (!tile)
            await AssertEntityLookup((product, 1));
    }

    [Test]
    public async Task RechargesOneUnitPerFifteenSecondsAndCannotSelectEngineeringRecipes()
    {
        var rcd = await PlaceInHands("RCDRechargingJanitor");
        await UseInHand();
        await RunTicks(3);
        await SendBui(RcdUiKey.Key, new RCDSystemMessage("RCDFloorSteel"), rcd);
        await SendBui(RcdUiKey.Key, new RCDSystemMessage("WallSolid"), rcd);
        await CloseBui(RcdUiKey.Key, rcd);
        await Server.WaitAssertion(() =>
        {
            var uid = ToServer(rcd);
            Assert.That(SEntMan.GetComponent<RCDComponent>(uid).ProtoId, Is.EqualTo((ProtoId<RCDPrototype>) "RCDFloorSteel"));
            var charges = Server.System<SharedChargesSystem>();
            var limited = SEntMan.GetComponent<LimitedChargesComponent>(uid);
            var recharge = SEntMan.GetComponent<AutoRechargeComponent>(uid);
            var timing = Server.ResolveDependency<IGameTiming>();
            Assert.That(recharge.RechargeDuration, Is.EqualTo(TimeSpan.FromSeconds(15)));
            charges.SetCharges(uid, 0);
#pragma warning disable RA0002 // Simulate elapsed recharge time without waiting five real minutes.
            limited.LastUpdate = timing.CurTime - TimeSpan.FromSeconds(14.99);
#pragma warning restore RA0002
            Assert.That(charges.GetCurrentCharges(uid), Is.Zero);
#pragma warning disable RA0002 // Simulate elapsed recharge time without waiting five real minutes.
            limited.LastUpdate = timing.CurTime - TimeSpan.FromSeconds(15);
#pragma warning restore RA0002
            Assert.That(charges.GetCurrentCharges(uid), Is.EqualTo(1));
#pragma warning disable RA0002 // Simulate elapsed recharge time without waiting five real minutes.
            limited.LastUpdate = timing.CurTime - TimeSpan.FromMinutes(5);
#pragma warning restore RA0002
            Assert.That(charges.GetCurrentCharges(uid), Is.EqualTo(20));
#pragma warning disable RA0002 // Simulate elapsed recharge time without waiting five real minutes.
            limited.LastUpdate = timing.CurTime - TimeSpan.FromMinutes(10);
#pragma warning restore RA0002
            Assert.That(charges.GetCurrentCharges(uid), Is.EqualTo(20));
        });
    }
}
