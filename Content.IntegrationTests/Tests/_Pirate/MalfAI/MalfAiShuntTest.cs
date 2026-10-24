// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Destructible;
using Content.Server.Mind;
using Content.Shared._Pirate.MalfAI;
using Content.Shared._Pirate.MalfAI.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Ghost;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Pirate.MalfAI;

[TestFixture]
public sealed class MalfAiShuntTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task DestroyedApcReturnsAiToCore(bool deleteDirectly)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false,
            Dirty = true,
        });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entMan = server.EntMan;
        var containers = server.System<SharedContainerSystem>();
        var stationAi = server.System<SharedStationAiSystem>();
        var mindSystem = server.System<MindSystem>();
        var session = pair.Player!;
        EntityUid core = default;
        EntityUid ai = default;
        EntityUid apc = default;
        EntityUid oldEye = default;

        await server.WaitAssertion(() =>
        {
            core = entMan.SpawnEntity("PlayerStationAiEmpty", map.GridCoords);
            ai = entMan.SpawnEntity("StationAiBrain", map.GridCoords);
            apc = entMan.SpawnEntity("APCBasic", map.GridCoords);
            Assert.That(containers.Insert(ai,
                containers.GetContainer(core, StationAiHolderComponent.Container)), Is.True);
            entMan.EnsureComponent<MalfAiMarkerComponent>(ai);
            var mind = mindSystem.CreateMind(session.UserId, session.Name);
            mindSystem.TransferTo(mind, ai, mind: mind.Comp);
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var shunt = new MalfAiShuntToApcActionEvent { Performer = ai, Target = apc };
            entMan.EventBus.RaiseLocalEvent(ai, shunt);
            Assert.That(shunt.Handled, Is.True);
            Assert.That(containers.TryGetContainingContainer(ai, out var container), Is.True);
            Assert.That(container!.Owner, Is.EqualTo(apc));
            oldEye = entMan.GetComponent<StationAiCoreComponent>(apc).RemoteEntity!.Value;
        });
        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            if (deleteDirectly)
                entMan.DeleteEntity(apc);
            else
                server.System<DamageableSystem>().ChangeDamage(apc,
                    new DamageSpecifier(server.ProtoMan.Index<DamageTypePrototype>("Blunt"), 60),
                    ignoreResistances: true);
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(apc), Is.True);
            Assert.That(entMan.Deleted(oldEye), Is.True);
            Assert.That(entMan.Deleted(ai), Is.False);
            Assert.That(containers.TryGetContainingContainer(ai, out var container), Is.True);
            Assert.That(container!.Owner, Is.EqualTo(core));
            Assert.That(entMan.HasComponent<MalfAiShuntedComponent>(ai), Is.False);
            Assert.That(entMan.HasComponent<StationAiHeldComponent>(ai), Is.True);
            Assert.That(server.System<MobStateSystem>().IsAlive(ai), Is.True);
            Assert.That(session.AttachedEntity, Is.EqualTo(ai));
            Assert.That(mindSystem.TryGetMind(ai, out _, out var mind), Is.True);
            Assert.That(mind!.IsVisitingEntity, Is.False);

            Assert.That(stationAi.TryGetCore(ai, out var holder), Is.True);
            var eye = holder.Comp!.RemoteEntity!.Value;
            Assert.That(entMan.Deleted(eye), Is.False);
            Assert.That(entMan.GetComponent<EyeComponent>(ai).Target, Is.EqualTo(eye));
            Assert.That(entMan.GetComponent<RelayInputMoverComponent>(ai).RelayEntity, Is.EqualTo(eye));
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("missing")]
    [TestCase("broken")]
    [TestCase("unpowered")]
    [TestCase("occupied")]
    public async Task DestroyedApcWithUnavailableCoreGhostsAi(string coreState)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false,
            Dirty = true,
        });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entMan = server.EntMan;
        var containers = server.System<SharedContainerSystem>();
        var mindSystem = server.System<MindSystem>();
        var session = pair.Player!;
        EntityUid core = default;
        EntityUid ai = default;
        EntityUid apc = default;

        await server.WaitAssertion(() =>
        {
            core = entMan.SpawnEntity("PlayerStationAiEmpty", map.GridCoords);
            ai = entMan.SpawnEntity("StationAiBrain", map.GridCoords);
            apc = entMan.SpawnEntity("APCBasic", map.GridCoords);
            Assert.That(containers.Insert(ai,
                containers.GetContainer(core, StationAiHolderComponent.Container)), Is.True);
            entMan.EnsureComponent<MalfAiMarkerComponent>(ai);
            var mind = mindSystem.CreateMind(session.UserId, session.Name);
            mindSystem.TransferTo(mind, ai, mind: mind.Comp);

            var shunt = new MalfAiShuntToApcActionEvent { Performer = ai, Target = apc };
            entMan.EventBus.RaiseLocalEvent(ai, shunt);
            Assert.That(shunt.Handled, Is.True);
            Assert.That(containers.TryGetContainingContainer(ai, out var container), Is.True);
            Assert.That(container!.Owner, Is.EqualTo(apc));

            switch (coreState)
            {
                case "missing":
                    entMan.DeleteEntity(core);
                    break;
                case "broken":
                    server.System<DamageableSystem>().ChangeDamage(core,
                        new DamageSpecifier(server.ProtoMan.Index<DamageTypePrototype>("Blunt"), 450),
                        ignoreResistances: true);
                    Assert.That(entMan.GetComponent<DestructibleComponent>(core).IsBroken, Is.True);
                    break;
                case "unpowered":
                    server.System<SharedPowerReceiverSystem>().SetPowerDisabled(core, true);
                    break;
                case "occupied":
                    var otherAi = entMan.SpawnEntity("StationAiBrain", map.GridCoords);
                    Assert.That(containers.Insert(otherAi,
                        containers.GetContainer(core, StationAiHolderComponent.Container)), Is.True);
                    break;
            }
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            if (coreState == "unpowered")
                Assert.That(server.System<SharedPowerReceiverSystem>().IsPowered(core), Is.False);

            server.System<DamageableSystem>().ChangeDamage(apc,
                new DamageSpecifier(server.ProtoMan.Index<DamageTypePrototype>("Blunt"), 60),
                ignoreResistances: true);
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(apc), Is.True);
            Assert.That(entMan.Deleted(ai), Is.True);
            Assert.That(session.AttachedEntity, Is.Not.Null);
            var ghost = entMan.GetComponent<GhostComponent>(session.AttachedEntity!.Value);
            Assert.That(ghost.CanReturnToBody, Is.False);
            Assert.That(mindSystem.TryGetMind(session.UserId, out _, out var mind), Is.True);
            Assert.That(mind!.OwnedEntity, Is.EqualTo(session.AttachedEntity));
            Assert.That(mind.IsVisitingEntity, Is.False);
        });

        await pair.CleanReturnAsync();
    }
}
