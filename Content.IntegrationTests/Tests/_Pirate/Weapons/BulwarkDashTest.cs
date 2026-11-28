// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Goobstation.Common.Weapons.MeleeDash;
using Content.Goobstation.Shared.Weapons.MeleeDash;
using Content.Shared.Blocking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Physics;
using Content.Shared.Throwing;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests._Pirate.Weapons;

[TestFixture]
public sealed class BulwarkDashTest
{
    [Test]
    public async Task BlockingAndInterruptedDashKeepTableCollision()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var em = server.EntMan;
        EntityUid user = default, shield = default;
        NetEntity weapon = default;
        const int table = (int) CollisionGroup.MidImpassable;
        var fixtures = server.System<FixtureSystem>();
        var blocking = server.System<BlockingSystem>();

        await server.WaitAssertion(() =>
        {
            user = em.SpawnEntity("MobHuman", map.GridCoords);
            server.PlayerMan.SetAttachedEntity(pair.Player!, user);
            shield = em.SpawnEntity("Bulwark", map.GridCoords);
            weapon = em.GetNetEntity(shield);
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(user, shield), Is.True);
            Assert.That(blocking.StartBlocking(shield, em.GetComponent<BlockingComponent>(shield), user), Is.True);
        });
        await pair.RunTicksSync(8);
        async Task Dash()
        {
            await pair.Client.WaitPost(() => pair.Client.EntMan.RaisePredictiveEvent(new MeleeDashEvent(weapon, Vector2.UnitX * 4)));
            await pair.RunTicksSync(3);
        }
        await Dash();
        await server.WaitAssertion(() =>
        {
            Assert.That(em.HasComponent<DashingComponent>(user), Is.False);
            Assert.That(blocking.StopBlocking(shield, em.GetComponent<BlockingComponent>(shield), user), Is.True);
            Assert.That(fixtures.GetFixtureOrNull(user, "fix1")!.CollisionMask & table, Is.EqualTo(table));
        });
        await Dash();
        await server.WaitAssertion(() =>
        {
            Assert.That(em.HasComponent<DashingComponent>(user), Is.True);
            server.System<ThrownItemSystem>().StopThrow(user, em.GetComponent<ThrownItemComponent>(user));
            Assert.That(em.HasComponent<DashingComponent>(user), Is.False);
            Assert.That(fixtures.GetFixtureOrNull(user, "fix1")!.CollisionMask & table, Is.EqualTo(table));
        });
        await pair.CleanReturnAsync();
    }
}
