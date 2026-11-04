// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Server._Pirate.Newbies;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Shuttles.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Shuttles.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests._Pirate.Newbies;

[TestFixture]
public sealed class NovicePilotTest
{
    [TestCase(0, false, true)]
    [TestCase(3.99, true, true)]
    [TestCase(4, false, false)]
    [TestCase(100, true, false)]
    public async Task ActualShuttleAccelerationRespectsTotalPlaytime(double hours, bool diagonal, bool restricted)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid user = default;
        await server.WaitAssertion(() =>
        {
            user = entities.SpawnEntity("MobHuman", map.GridCoords);
            server.PlayerMan.SetAttachedEntity(pair.Player!, user);
            NoviceTestPlaytime.Set(server.ResolveDependency<PlayTimeTrackingManager>(), pair.Player!, hours);
            var console = entities.SpawnEntity("ComputerShuttle", map.GridCoords);
            var pilot = entities.AddComponent<PilotComponent>(user);
            pilot.Console = console;
            pilot.HeldButtons = ShuttleButtons.StrafeRight | (diagonal ? ShuttleButtons.StrafeUp : ShuttleButtons.None);
            var shuttle = entities.GetComponent<ShuttleComponent>(map.Grid);
            Array.Fill(shuttle.LinearThrust, 100000f);
            server.System<SharedPhysicsSystem>().SetLinearDamping(map.Grid, entities.GetComponent<PhysicsComponent>(map.Grid), 0f);
        });
        await pair.RunTicksSync(30);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.HasComponent<PilotComponent>(user), Is.True, "The pilot must still be actively controlling the shuttle.");
            var speed = entities.GetComponent<PhysicsComponent>(map.Grid).LinearVelocity.Length();
            Assert.That(speed, Is.GreaterThan(1f), "The test must actually accelerate the shuttle.");
            if (restricted)
                Assert.That(speed, Is.LessThanOrEqualTo(4.001f));
            else
                Assert.That(speed, Is.GreaterThan(4.1f));
            Assert.That(entities.GetComponent<ShuttleComponent>(map.Grid).BaseMaxLinearVelocity, Is.EqualTo(40f),
                "The temporary restriction must not change the shuttle's normal limit.");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PassiveConsoleDoesNotLimitSpeedButNoviceTakeoverDoes()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid user = default;
        await server.WaitAssertion(() =>
        {
            user = entities.SpawnEntity("MobHuman", map.GridCoords);
            server.PlayerMan.SetAttachedEntity(pair.Player!, user);
            NoviceTestPlaytime.Set(server.ResolveDependency<PlayTimeTrackingManager>(), pair.Player!, 0);
            var console = entities.SpawnEntity("ComputerShuttle", map.GridCoords);
            var pilot = entities.AddComponent<PilotComponent>(user);
            pilot.Console = console;
            var physics = server.System<SharedPhysicsSystem>();
            physics.SetLinearDamping(map.Grid, entities.GetComponent<PhysicsComponent>(map.Grid), 0f);
            physics.SetLinearVelocity(map.Grid, new Vector2(12f, 9f));
        });
        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            var body = entities.GetComponent<PhysicsComponent>(map.Grid);
            Assert.That(body.LinearVelocity.Length(), Is.GreaterThan(4f));
            entities.GetComponent<PilotComponent>(user).HeldButtons = ShuttleButtons.RotateLeft;
        });
        await pair.RunTicksSync(2);
        await server.WaitAssertion(() => Assert.That(entities.GetComponent<PhysicsComponent>(map.Grid).LinearVelocity.Length(), Is.LessThanOrEqualTo(4.001f)));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UnknownPlaytimeIsCappedAndLoadedVeteranUnlocks()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        var tracking = server.ResolveDependency<PlayTimeTrackingManager>();
        EntityUid user = default;
        await server.WaitAssertion(() =>
        {
            user = entities.SpawnEntity("MobHuman", map.GridCoords);
            server.PlayerMan.SetAttachedEntity(pair.Player!, user);
            NoviceTestPlaytime.Set(tracking, pair.Player!, 100);
            NoviceTestPlaytime.SetLoaded(tracking, pair.Player!, false);
            try
            {
                Assert.That(server.System<NovicePilotSpeedSystem>().GetSpeedLimit(user, 40f), Is.EqualTo(4f));
            }
            finally
            {
                NoviceTestPlaytime.SetLoaded(tracking, pair.Player!, true);
            }
            tracking.QueueSendTimers(pair.Player!);
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() => Assert.That(server.System<NovicePilotSpeedSystem>().GetSpeedLimit(user, 40f), Is.EqualTo(40f)));
        await pair.CleanReturnAsync();
    }
}
