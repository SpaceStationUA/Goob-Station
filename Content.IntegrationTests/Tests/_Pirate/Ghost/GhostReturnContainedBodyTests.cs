// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.IntegrationTests.Pair;
using Content.Server.EUI;
using Content.Server.Ghost;
using Content.Server.Mind;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Ghost;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Storage.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Pirate.Ghost;

[TestFixture]
public sealed class GhostReturnContainedBodyTests
{
    [Test]
    public async Task ReturnToBodyWaitsUntilBodyLeavesEntityStorage()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
            Dirty = true,
        });
        var map = await pair.CreateTestMap();
        var entMan = pair.Server.EntMan;
        var euiManager = pair.Server.ResolveDependency<EuiManager>();
        var ghostSystem = pair.Server.System<GhostSystem>();
        var mindSystem = pair.Server.System<MindSystem>();
        var mobState = pair.Server.System<MobStateSystem>();
        var storageSystem = pair.Server.System<EntityStorageSystem>();
        var session = pair.Server.PlayerMan.Sessions.Single();

        EntityUid body = default;
        EntityUid bodyBag = default;
        EntityUid ghost = default;

        await pair.Server.WaitAssertion(() =>
        {
            body = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var mind = mindSystem.CreateMind(session.UserId, session.Name);
            mindSystem.TransferTo(mind, body, mind: mind.Comp);
            mobState.ChangeMobState(body, MobState.Dead);

            Assert.That(ghostSystem.OnGhostAttempt(mind, true, mind: mind.Comp), Is.True);
            ghost = session.AttachedEntity!.Value;
            Assert.That(entMan.HasComponent<GhostComponent>(ghost), Is.True);

            bodyBag = entMan.SpawnEntity("BodyBag", map.GridCoords);
            Assert.That(storageSystem.Insert(body, bodyBag), Is.True);
            Assert.That(entMan.HasComponent<InsideEntityStorageComponent>(body), Is.True);
        });
        await pair.RunTicksSync(10);

        await pair.Client.WaitPost(() => pair.Client.System<Content.Client.Ghost.GhostSystem>().ReturnToBody());
        await pair.RunTicksSync(10);

        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(session.AttachedEntity, Is.EqualTo(ghost));
            Assert.That(mindSystem.TryGetMind(session.UserId, out _, out var mind), Is.True);
            var mindComponent = mind!;
            Assert.That(mindComponent.VisitingEntity, Is.EqualTo(ghost));

            var returnEui = new ReturnToBodyEui(mindComponent, ghostSystem, pair.Server.PlayerMan);
            euiManager.OpenEui(returnEui, session);
            returnEui.HandleMessage(new ReturnToBodyMessage(true));

            Assert.That(session.AttachedEntity, Is.EqualTo(ghost));
            Assert.That(mindComponent.VisitingEntity, Is.EqualTo(ghost));
        });

        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(storageSystem.Remove(body, bodyBag), Is.True);
            Assert.That(entMan.HasComponent<InsideEntityStorageComponent>(body), Is.False);
        });
        await pair.RunTicksSync(5);

        await pair.Client.WaitPost(() => pair.Client.System<Content.Client.Ghost.GhostSystem>().ReturnToBody());
        await pair.RunTicksSync(10);

        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(session.AttachedEntity, Is.EqualTo(body));
            Assert.That(entMan.HasComponent<GhostComponent>(session.AttachedEntity!.Value), Is.False);
        });

        await pair.CleanReturnAsync();
    }
}
