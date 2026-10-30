// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Pirate.Shared.Voodoo;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Timing;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Pirate.Voodoo;

[TestFixture]
public sealed class WickerDollTest
{
    [TestCase("ready", true)]
    [TestCase("distant", false)]
    [TestCase("holy", false)]
    [TestCase("stored", false)]
    public async Task CursesRespectProtectionRangeStorageAndCooldown(string condition, bool allowed)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var user = entities.SpawnEntity("MobHuman", map.GridCoords);
            var victimCoordinates = condition == "distant"
                ? new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + new Vector2(200, 0))
                : map.GridCoords;
            var victim = entities.SpawnEntity("MobHuman", victimCoordinates);
            var doll = entities.SpawnEntity("VoodooDoll", map.GridCoords);
            var knife = entities.SpawnEntity("CombatKnife", map.GridCoords);
            var hands = server.System<SharedHandsSystem>();
            Assert.That(hands.TryPickupAnyHand(user, doll), Is.True);
            Assert.That(hands.TryPickupAnyHand(user, knife), Is.True);
            var voodoo = entities.GetComponent<VoodooDollComponent>(doll);
            voodoo.Target = victim;

            if (condition == "holy")
            {
                var bloodstream = entities.GetComponent<BloodstreamComponent>(victim);
                var solutions = server.System<SharedSolutionContainerSystem>();
                Assert.That(solutions.ResolveSolution(victim, bloodstream.BloodSolutionName,
                    ref bloodstream.BloodSolution), Is.True);
                Assert.That(solutions.TryAddReagent(bloodstream.BloodSolution!.Value,
                    "Holywater", FixedPoint2.New(5)), Is.True);
            }
            else if (condition == "stored")
            {
                Assert.That(hands.TryDrop(user, doll, checkActionBlocker: false), Is.True);
                var owner = entities.SpawnEntity(null, map.GridCoords);
                var containers = server.System<SharedContainerSystem>();
                var storage = containers.EnsureContainer<Container>(owner, "voodoo-test-storage");
                Assert.That(containers.Insert(doll, storage), Is.True);
            }

            var damage = entities.GetComponent<DamageableComponent>(victim);
            var before = damage.TotalDamage;
            entities.EventBus.RaiseLocalEvent(doll, new InteractUsingEvent(user, knife, doll, map.GridCoords));
            var after = damage.TotalDamage;

            Assert.That(after > before, Is.EqualTo(allowed));
            Assert.That(server.System<UseDelaySystem>().IsDelayed(doll, voodoo.DelayId), Is.EqualTo(allowed),
                "Rejected curses must not consume the shared cooldown.");

            entities.EventBus.RaiseLocalEvent(doll, new InteractUsingEvent(user, knife, doll, map.GridCoords));
            Assert.That(damage.TotalDamage, Is.EqualTo(after), "A second immediate curse must do no damage.");
        });

        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task LosingTheDollRestoresTheView(bool delete)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var user = entities.SpawnEntity("MobHuman", map.GridCoords);
            server.PlayerMan.SetAttachedEntity(pair.Player!, user);
            var victim = entities.SpawnEntity("MobHuman", map.GridCoords);
            var doll = entities.SpawnEntity("VoodooDoll", map.GridCoords);
            var hands = server.System<SharedHandsSystem>();
            Assert.That(hands.TryPickupAnyHand(user, doll), Is.True);
            var voodoo = entities.GetComponent<VoodooDollComponent>(doll);
            voodoo.Target = victim;
            entities.EnsureComponent<TargetingComponent>(user).Target = TargetBodyPart.Head;

            entities.EventBus.RaiseLocalEvent(doll, new UseInHandEvent(user));
            Assert.That(voodoo.Viewer, Is.EqualTo(user));
            Assert.That(entities.GetComponent<EyeComponent>(user).Target, Is.EqualTo(victim));

            if (delete)
                entities.DeleteEntity(doll);
            else
                Assert.That(hands.TryDrop(user, doll, checkActionBlocker: false), Is.True);

            Assert.That(voodoo.Viewer, Is.Null);
            Assert.That(entities.GetComponent<EyeComponent>(user).Target, Is.Null);
        });

        await pair.CleanReturnAsync();
    }
}
