// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Tests.Interaction;
using Content.Pirate.Shared.Genetics.Interaction;
using Content.Pirate.Shared.Genetics.Mutations;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Misc;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.Tests._Pirate.Genetics;

public sealed class GeneticsTelekinesisTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    [TestCase(false)]
    [TestCase(true)]
    public async Task TelekinesisFollowsMovementRequestsAndCleansUpItsActiveTether(bool holdingItem)
    {
        await SpawnTarget("Wrench");
        var target = STarget!.Value;
        var mutations = Server.System<MutationSystem>();
        TetherGunComponent gun = null;
        EntityUid tether = default;
        EntityUid? stream = null;
        float damping = 0;
        await Server.WaitAssertion(() =>
        {
            if (holdingItem)
            {
                var held = SEntMan.SpawnEntity("Screwdriver", SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
                Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(SPlayer, held), Is.True);
            }
            Assert.That(mutations.AddMutation(SPlayer, "MutationTelekinesis"), Is.True);
            damping = SEntMan.GetComponent<PhysicsComponent>(target).AngularDamping;
            var action = new TelekinesisActionEvent { Performer = SPlayer, Target = target };
            SEntMan.EventBus.RaiseLocalEvent(SPlayer, action);
            Assert.That(action.Handled, Is.True);
            gun = SEntMan.GetComponent<TetherGunComponent>(SPlayer);
            Assert.That(gun.Tethered, Is.EqualTo(target));
            Assert.That(gun.TetherEntity, Is.Not.Null);
            tether = gun.TetherEntity!.Value;
            stream = gun.Stream;
            Assert.That(SEntMan.HasComponent<TetheredComponent>(target), Is.True);
            Assert.That(SEntMan.GetComponent<PhysicsComponent>(target).BodyStatus, Is.EqualTo(BodyStatus.InAir));
            AssertLookup(Server.System<SharedTetherGunSystem>(), SPlayer, gun);

            // Exercise the same session-based request handler used by client cursor updates.
            var coordinates = SEntMan.GetCoordinates(PlayerCoords).Offset(new Vector2(0, 2));
            var requestType = typeof(SharedTetherGunSystem).GetNestedType("RequestTetherMoveEvent", BindingFlags.NonPublic)!;
            var request = Activator.CreateInstance(requestType)!;
            requestType.GetField("Coordinates")!.SetValue(request, SEntMan.GetNetCoordinates(coordinates));
            typeof(SharedTetherGunSystem).GetMethod("OnTetherMove", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(Server.System<SharedTetherGunSystem>(), new[] { request, (object) new EntitySessionEventArgs(ServerSession) });
            Assert.That(SEntMan.GetComponent<TransformComponent>(tether).Coordinates, Is.EqualTo(coordinates));
        });
        await RunTicks(5);
        await Client.WaitAssertion(() =>
        {
            // The client must resolve the body-owned tether too, or it never sends cursor updates.
            var clientGun = CEntMan.GetComponent<TetherGunComponent>(CPlayer);
            AssertLookup(Client.System<SharedTetherGunSystem>(), CPlayer, clientGun);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(mutations.RemoveMutation(SPlayer, "MutationTelekinesis"), Is.True);
            Assert.That(SEntMan.HasComponent<TelekinesisComponent>(SPlayer), Is.False);
            Assert.That(SEntMan.HasComponent<TetherGunComponent>(SPlayer), Is.False);
            Assert.That(SEntMan.HasComponent<TetheredComponent>(target), Is.False);
            Assert.That(gun.Tethered, Is.Null);
            Assert.That(gun.TetherEntity, Is.Null);
            Assert.That(gun.Stream, Is.Null);
            var physics = SEntMan.GetComponent<PhysicsComponent>(target);
            Assert.That(physics.BodyStatus, Is.EqualTo(BodyStatus.OnGround));
            Assert.That(physics.AngularDamping, Is.EqualTo(damping));
        });
        await RunTicks(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(tether), Is.False, "The tether anchor and its joint must be deleted.");
            if (stream is {} sound)
                Assert.That(SEntMan.EntityExists(sound), Is.False, "The looping tether sound must stop.");
        });
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
