// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Server._Pirate.MalfAI.Factory.Components;
using Content.Server._Pirate.MalfAI.Factory.Systems;
using Content.Server.Materials;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Materials;
using Content.Shared.Mind;
using Content.Shared.Players;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared._Pirate.MalfAI;
using Content.Shared._Pirate.MalfAI.Factory.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Pirate;

[TestFixture]
public sealed class MalfAiBorgFactoryTest
{
    [Test]
    public async Task FactoryRequiresAnchoringAndConvertsLivingPlayerHumanoid()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false
        });

        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var mindSystem = entMan.System<SharedMindSystem>();
        var laws = entMan.System<Content.Server.Silicons.Laws.SiliconLawSystem>();
        var session = server.PlayerMan.Sessions.Single();
        var mind = session.ContentData()!.Mind!.Value;

        EntityUid factory = default;
        EntityUid human = default;
        EntityUid malfAi = default;

        await server.WaitPost(() =>
        {
            malfAi = entMan.SpawnEntity(null, map.GridCoords);
            entMan.EnsureComponent<MalfAiMarkerComponent>(malfAi);
            entMan.EnsureComponent<StationAiHeldComponent>(malfAi);
            factory = entMan.SpawnEntity("RoboticsFactoryGrid", map.GridCoords);
            entMan.EnsureComponent<MalfFactoryOwnerComponent>(factory).Controller = malfAi;
            human = entMan.SpawnEntity("MobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            mindSystem.TransferTo(mind, human, true);
            entMan.System<SharedTransformSystem>().Unanchor(factory);

            // An unanchored factory must not consume a crew member.
            var rejected = new MaterialReclaimerProcessEntityEvent(human);
            entMan.EventBus.RaiseLocalEvent(factory, rejected);
            Assert.That(rejected.Handled, Is.True);
            Assert.That(entMan.HasComponent<BorgChassisComponent>(human), Is.False);

            Assert.That(entMan.System<SharedTransformSystem>().AnchorEntity(factory), Is.True);
        });

        await server.WaitRunTicks(5);

        await server.WaitPost(() =>
        {
            // Exercise the same intake pipeline as a body pushed onto the factory.
            Assert.That(entMan.System<MaterialReclaimerSystem>().TryStartProcessItem(factory, human), Is.True);
        });

        await server.WaitRunTicks(5);
        await server.WaitAssertion(() =>
        {
            var owned = entMan.GetComponent<MindComponent>(mind).OwnedEntity;
            Assert.That(owned, Is.Not.Null);
            var borg = owned!.Value;
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<BorgChassisComponent>(borg), Is.True);
                Assert.That(entMan.GetComponent<MetaDataComponent>(borg).EntityPrototype!.ID,
                    Is.EqualTo("PlayerBorgBatteryNoMind"));
                Assert.That(entMan.HasComponent<MalfAiControlledComponent>(borg), Is.True);
                Assert.That(entMan.GetComponent<MalfAiControlledComponent>(borg).Controller, Is.EqualTo(malfAi));
                Assert.That(laws.GetLaws(borg).Laws.Any(law => law.LawIdentifierOverride == "0"), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FactoryRejectsMindlessHumanoidAndDoesNotConsumeIt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        EntityUid factory = default;
        EntityUid human = default;

        await server.WaitPost(() =>
        {
            factory = entMan.SpawnEntity("RoboticsFactoryGrid", map.GridCoords);
            Assert.That(entMan.GetComponent<TransformComponent>(factory).Anchored, Is.True);
            human = entMan.SpawnEntity("MobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var ev = new MaterialReclaimerProcessEntityEvent(human);
            entMan.EventBus.RaiseLocalEvent(factory, ev);
            Assert.That(entMan.HasComponent<BorgChassisComponent>(human), Is.False);
            Assert.That(entMan.EntityExists(human), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FactoryConversionIsOneUseForConvertedBorg()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, DummyTicker = false });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var mindSystem = entMan.System<SharedMindSystem>();
        var session = server.PlayerMan.Sessions.Single();
        var mind = session.ContentData()!.Mind!.Value;
        EntityUid factory = default;
        EntityUid human = default;

        await server.WaitPost(() =>
        {
            factory = entMan.SpawnEntity("RoboticsFactoryGrid", map.GridCoords);
            Assert.That(entMan.GetComponent<TransformComponent>(factory).Anchored, Is.True);
            human = entMan.SpawnEntity("MobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            mindSystem.TransferTo(mind, human, true);
            var first = new MaterialReclaimerProcessEntityEvent(human);
            entMan.EventBus.RaiseLocalEvent(factory, first);
        });
        await server.WaitRunTicks(5);
        await server.WaitAssertion(() =>
        {
            var borg = entMan.GetComponent<MindComponent>(mind).OwnedEntity;
            Assert.That(borg, Is.Not.Null);
            var second = new MaterialReclaimerProcessEntityEvent(borg!.Value);
            entMan.EventBus.RaiseLocalEvent(factory, second);
            Assert.That(entMan.GetComponent<MindComponent>(mind).OwnedEntity, Is.EqualTo(borg));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FactoryPurchaseOnlyAllowsOneConcurrentBuild()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        EntityUid action = default;
        var before = entMan.Count<RoboticsFactoryGridComponent>();

        await server.WaitAssertion(() =>
        {
            var maps = entMan.System<SharedMapSystem>();
            maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(0, 0), new Tile(1));
            maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(1, 0), new Tile(1));
            var ai = entMan.SpawnEntity("StationAiBrain", map.GridCoords);
            entMan.EnsureComponent<MalfAiMarkerComponent>(ai);
            entMan.EnsureComponent<DoAfterComponent>(ai);
            action = entMan.System<SharedActionsSystem>().AddAction(ai, "ActionMalfAiRoboticsFactory")!.Value;

            entMan.EventBus.RaiseEvent(EventSource.Local,
                new AIBuildRequestEvent(ai, map.GridCoords, "RoboticsFactoryGrid"));
            entMan.EventBus.RaiseEvent(EventSource.Local,
                new AIBuildRequestEvent(ai, map.GridCoords.Offset(new Vector2(1, 0)), "RoboticsFactoryGrid"));
        });
        await pair.RunSeconds(3.5f);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Count<RoboticsFactoryGridComponent>(), Is.EqualTo(before + 1));
            Assert.That(entMan.EntityExists(action), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AiBuildRejectsInvalidPlacementWithoutSpawning()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();
        var before = entMan.Count<RoboticsFactoryGridComponent>();
        await server.WaitPost(() =>
        {
            var requester = entMan.SpawnEntity(null, Robust.Shared.Map.MapCoordinates.Nullspace);
            entMan.EnsureComponent<MalfAiMarkerComponent>(requester);
            entMan.EventBus.RaiseEvent(EventSource.Local, new Content.Server._Pirate.MalfAI.Factory.Systems.AIBuildRequestEvent(
                requester, Robust.Shared.Map.EntityCoordinates.Invalid, "RoboticsFactoryGrid"));
            var wall = entMan.SpawnEntity("WallSolid", map.GridCoords);
            Assert.That(entMan.GetComponent<TransformComponent>(wall).Anchored, Is.True);
            // Occupied tiles must be rejected without looking up the nonexistent WallMount tag.
            entMan.EventBus.RaiseEvent(EventSource.Local, new AIBuildRequestEvent(
                requester, map.GridCoords, "RoboticsFactoryGrid"));
        });
        await server.WaitRunTicks(2);
        await server.WaitAssertion(() => Assert.That(entMan.Count<RoboticsFactoryGridComponent>(), Is.EqualTo(before)));
        await pair.CleanReturnAsync();
    }
}
