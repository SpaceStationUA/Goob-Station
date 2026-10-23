// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Tests.Interaction;
using Content.Pirate.Shared.Genetics.Interaction;
using Content.Pirate.Shared.Genetics.Mutations;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Misc;
using Moq;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests._Pirate.Genetics;

public sealed class GeneticsTelekinesisTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    [TestCase(false)]
    [TestCase(true)]
    public async Task BothPeersResolveBodyOwnedTelekinesisRegardlessOfTheActiveHand(bool holdingItem)
    {
        var mutations = Server.System<MutationSystem>();
        await Server.WaitAssertion(() =>
        {
            if (holdingItem)
            {
                var held = SEntMan.SpawnEntity("Screwdriver", SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
                Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(SPlayer, held), Is.True);
            }
            Assert.That(mutations.AddMutation(SPlayer, "MutationTelekinesis"), Is.True);
            AssertLookup(Server.System<SharedTetherGunSystem>(), SPlayer, SEntMan.GetComponent<TetherGunComponent>(SPlayer));
        });
        await RunTicks(5);
        await Client.WaitAssertion(() =>
        {
            AssertLookup(Client.System<SharedTetherGunSystem>(), CPlayer, CEntMan.GetComponent<TetherGunComponent>(CPlayer));
        });
        await Server.WaitAssertion(() => Assert.That(mutations.RemoveMutation(SPlayer, "MutationTelekinesis"), Is.True));
        await RunTicks(5);
        await Client.WaitAssertion(() => Assert.That(CEntMan.HasComponent<TetherGunComponent>(CPlayer), Is.False));
    }

    private static void AssertLookup(SharedTetherGunSystem system, EntityUid user, TetherGunComponent expected)
    {
        var arguments = new object[] { user, null, null };
        var found = typeof(SharedTetherGunSystem).GetMethod("TryGetTetherGun", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(system, arguments);
        Assert.That(found, Is.EqualTo(true));
        Assert.That(arguments[1], Is.EqualTo(user));
        Assert.That(arguments[2], Is.SameAs(expected));
    }
}

[TestFixture]
public sealed class GeneticsTelekinesisLifecycleTest
{
    // Test authoritative movement and cleanup separately from client lookup. Full joint replication
    // currently hits Robust's existing one-sided joint initialization diagnostic, also used by tether guns.
    [Test]
    public async Task TelekinesisMovementRequestsAndCleansingReleaseTheTargetAnchorAndSound()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var mutations = server.System<MutationSystem>();
        var map = await pair.CreateTestMap();
        EntityUid human = default, target = default, tether = default;
        EntityUid? stream = null;
        await server.WaitAssertion(() =>
        {
            human = entities.SpawnEntity("MobHuman", map.GridCoords);
            target = entities.SpawnEntity("Wrench", map.GridCoords.Offset(new Vector2(1, 0)));
            Assert.That(mutations.AddMutation(human, "MutationTelekinesis"), Is.True);
            var damping = entities.GetComponent<PhysicsComponent>(target).AngularDamping;
            var action = new TelekinesisActionEvent { Performer = human, Target = target };
            entities.EventBus.RaiseLocalEvent(human, action);
            Assert.That(action.Handled, Is.True);
            var gun = entities.GetComponent<TetherGunComponent>(human);
            Assert.That(gun.Tethered, Is.EqualTo(target));
            Assert.That(gun.TetherEntity, Is.Not.Null);
            tether = gun.TetherEntity!.Value;
            stream = gun.Stream;
            Assert.That(entities.HasComponent<TetheredComponent>(target), Is.True);
            Assert.That(entities.GetComponent<PhysicsComponent>(target).BodyStatus, Is.EqualTo(BodyStatus.InAir));
            var targetJoints = entities.GetComponent<JointComponent>(target).GetJoints;
            var anchorJoints = entities.GetComponent<JointComponent>(tether).GetJoints;
            Assert.That(targetJoints["tether"], Is.SameAs(anchorJoints["tether"]));

            var session = new Mock<ICommonSession>();
            session.SetupGet(s => s.AttachedEntity).Returns(human);
            var coordinates = map.GridCoords.Offset(new Vector2(0, 2));
            var requestType = typeof(SharedTetherGunSystem).GetNestedType("RequestTetherMoveEvent", BindingFlags.NonPublic)!;
            var request = Activator.CreateInstance(requestType)!;
            requestType.GetField("Coordinates")!.SetValue(request, entities.GetNetCoordinates(coordinates));
            typeof(SharedTetherGunSystem).GetMethod("OnTetherMove", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(server.System<SharedTetherGunSystem>(), new[] { request, (object) new EntitySessionEventArgs(session.Object) });
            Assert.That(entities.GetComponent<TransformComponent>(tether).Coordinates, Is.EqualTo(coordinates));

            Assert.That(mutations.RemoveMutation(human, "MutationTelekinesis"), Is.True);
            Assert.That(entities.HasComponent<TelekinesisComponent>(human), Is.False);
            Assert.That(entities.HasComponent<TetherGunComponent>(human), Is.False);
            Assert.That(entities.HasComponent<TetheredComponent>(target), Is.False);
            Assert.That(gun.Tethered, Is.Null);
            Assert.That(gun.TetherEntity, Is.Null);
            Assert.That(gun.Stream, Is.Null);
            Assert.That(targetJoints, Is.Empty);
            Assert.That(anchorJoints, Is.Empty);
            var physics = entities.GetComponent<PhysicsComponent>(target);
            Assert.That(physics.BodyStatus, Is.EqualTo(BodyStatus.OnGround));
            Assert.That(physics.AngularDamping, Is.EqualTo(damping));
        });
        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.EntityExists(tether), Is.False);
            if (stream is {} sound)
                Assert.That(entities.EntityExists(sound), Is.False);
            entities.DeleteEntity(target);
            entities.DeleteEntity(human);
        });
        await pair.CleanReturnAsync();
    }
}
