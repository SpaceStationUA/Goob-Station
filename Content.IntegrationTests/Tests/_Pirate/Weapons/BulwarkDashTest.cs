// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Goobstation.Common.Weapons.MeleeDash;
using Content.Goobstation.Shared.Weapons.MeleeDash;
using Content.IntegrationTests.Tests.Movement;
using Content.Shared.Blocking;
using Content.Shared.Physics;
using Content.Shared.Throwing;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests._Pirate.Weapons;

public sealed class BulwarkDashTest : MovementTest
{
    protected override int Tiles => 6;

    private async Task Dash(NetEntity shield)
    {
        await Client.WaitPost(() => CEntMan.RaisePredictiveEvent(new MeleeDashEvent(shield, Vector2.UnitX * 4)));
        await RunTicks(3);
    }

    [Test]
    public async Task BlockingAndInterruptedDashKeepTableCollision()
    {
        var shield = await PlaceInHands("Bulwark");
        var weapon = ToServer(shield);
        var blocking = Server.System<BlockingSystem>();
        var block = SEntMan.GetComponent<BlockingComponent>(weapon);
        var fixtures = Server.System<FixtureSystem>();
        const int table = (int) CollisionGroup.MidImpassable;

        await Server.WaitAssertion(() => Assert.That(blocking.StartBlocking(weapon, block, SPlayer), Is.True));
        await RunTicks(3);
        await Dash(shield);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<DashingComponent>(SPlayer), Is.False);
            Assert.That(blocking.StopBlocking(weapon, block, SPlayer), Is.True);
            Assert.That(fixtures.GetFixtureOrNull(SPlayer, "fix1")!.CollisionMask & table, Is.EqualTo(table));
        });

        await Dash(shield);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<DashingComponent>(SPlayer), Is.True);
            Server.System<ThrownItemSystem>().StopThrow(SPlayer, SEntMan.GetComponent<ThrownItemComponent>(SPlayer));
            Assert.That(SEntMan.HasComponent<DashingComponent>(SPlayer), Is.False);
            Assert.That(fixtures.GetFixtureOrNull(SPlayer, "fix1")!.CollisionMask & table, Is.EqualTo(table));
        });
    }

    [Test]
    public async Task DashCrossesTableAndWalkingBackStopsAtItsEdge()
    {
        var table = await SpawnTarget("Table");
        var shield = await PlaceInHands("Bulwark");
        Assert.That(Delta(Player, table), Is.EqualTo(-1f).Within(0.05f));

        await Dash(shield);
        for (var i = 0; i < STiming.TickRate; i++)
        {
            if (!SEntMan.HasComponent<DashingComponent>(SPlayer) || Delta(Player, table) >= 1f)
                break;

            await RunTicks(1);
        }

        Assert.That(Delta(Player, table), Is.GreaterThanOrEqualTo(1f), "The dash must clear the far edge of the table.");
        Assert.That(SEntMan.HasComponent<DashingComponent>(SPlayer), Is.True);

        await RunSeconds(1f);
        Assert.That(SEntMan.HasComponent<DashingComponent>(SPlayer), Is.False);
        var landingDistance = Delta(Player, table);
        await Move(DirectionFlag.West, 2f);
        var stoppedDistance = Delta(Player, table);
        Assert.That(stoppedDistance, Is.LessThan(landingDistance - 0.25f), "The player must actually walk back to the table.");
        Assert.That(stoppedDistance, Is.InRange(0.5f, 1f), "The table must stop ordinary movement at its far edge.");
    }
}
