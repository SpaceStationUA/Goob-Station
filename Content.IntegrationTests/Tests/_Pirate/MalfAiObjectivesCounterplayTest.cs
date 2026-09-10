// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server._Pirate.MalfAI;
using Content.Server.AlertLevel;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Objectives.Systems;
using Content.Shared.Objectives.Systems;
using Content.Shared.Roles;
using Content.Server.Silicons.Laws;
using Content.Server.Station.Systems;
using Content.Shared._Pirate.MalfAI;
using Content.Shared._Pirate.MalfAI.Actions;
using Content.Shared.Actions;
using Content.Shared.CCVar;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Pirate;

[TestFixture]
public sealed class MalfAiObjectivesCounterplayTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: MalfCounterplayTestStation
  parent: [BaseStation, BaseStationAlertLevels]
";

    [Test]
    public async Task RoleSelectionGrantsPowersSurvivalAndMasterLaws()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false,
            Dirty = true,
        });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var (core, ai) = SpawnAi(entMan, map.GridCoords);
            var minds = entMan.System<MindSystem>();
            var session = server.PlayerMan.Sessions.Single();
            var mind = minds.CreateMind(session.UserId, "Malfunctioning AI");
            minds.TransferTo(mind, ai);
            // Supply real eligible targets for the rule's random objectives.
            var crew = entMan.SpawnEntity("MobHuman", map.GridCoords);
            minds.TransferTo(minds.CreateMind(null, "Crew member"), crew);
            var rule = entMan.SpawnEntity("MalfAi", MapCoordinates.Nullspace);
            Assert.That(entMan.System<GameTicker>().StartGameRule(rule), Is.True);
            var selection = entMan.GetComponent<AntagSelectionComponent>(rule);
            Assert.That(entMan.System<AntagSelectionSystem>().MakeAntag(
                (rule, selection), session, selection.Definitions.Single(), ignoreSpawner: true), Is.True);

            var survive = mind.Comp.Objectives.Single(uid => entMan.HasComponent<MalfAiSurviveObjectiveComponent>(uid));
            Assert.That(Progress(entMan, survive, mind), Is.EqualTo(1f));
            var granted = entMan.System<SharedActionsSystem>().GetActions(ai)
                .Select(action => entMan.GetComponent<MetaDataComponent>(action.Owner).EntityPrototype?.ID).ToArray();
            Assert.That(granted, Does.Contain("ActionMalfAiOpenStore"));
            Assert.That(granted, Does.Contain("ActionMalfAiOpenBorgsUi"));
            Assert.That(entMan.System<SiliconLawSystem>().GetLaws(rule).Laws.Select(law => law.LawString),
                Is.EqualTo(new[]
                {
                    Loc.GetString("silicon-law-malfai-master-1"),
                    Loc.GetString("silicon-law-malfai-master-2"),
                    Loc.GetString("silicon-law-malfai-master-3"),
                }));
            Assert.That(mind.Comp.OwnedEntity, Is.EqualTo(ai));
            Assert.That(Holder(entMan, core).ContainedEntities, Does.Contain(ai));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ProtectObjectiveAssignmentTargetsMindRatherThanBody()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var minds = entMan.System<MindSystem>();
            var owner = minds.CreateMind(null, "Objective owner");
            var (_, ai) = SpawnAi(entMan, map.GridCoords);
            minds.TransferTo(owner, ai);
            entMan.System<SharedRoleSystem>().MindAddRole(owner, "MindRoleMalfAi", owner.Comp, silent: true);
            var target = minds.CreateMind(null, "Protected crew member");
            var body = entMan.SpawnEntity("MobHuman", map.GridCoords);
            minds.TransferTo(target, body);

            Assert.That(entMan.System<SharedObjectivesSystem>().TryCreateObjective(
                owner,
                "MalfAiProtectObjective",
                out var objective), Is.True);
            Assert.That(objective, Is.Not.Null);
            Assert.That(entMan.GetComponent<Content.Server.Objectives.Components.TargetObjectiveComponent>(objective!.Value).Target,
                Is.EqualTo(target.Owner));
            Assert.That(Progress(entMan, objective.Value, owner), Is.EqualTo(1f));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TargetObjectivesFollowMindAcrossDeathReplacementAndBodyLoss()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var minds = entMan.System<MindSystem>();
            var owner = minds.CreateMind(null, "Objective owner");
            var target = minds.CreateMind(null, "Target");
            var body = entMan.SpawnEntity("MobHuman", map.GridCoords);
            minds.TransferTo(target, body);
            var kill = entMan.SpawnEntity("MalfAiAssassinateObjective", MapCoordinates.Nullspace);
            var protect = entMan.SpawnEntity("MalfAiProtectObjective", MapCoordinates.Nullspace);
            var targets = entMan.System<TargetObjectiveSystem>();
            targets.SetTarget(kill, target);
            targets.SetTarget(protect, target);

            Assert.That(Progress(entMan, kill, owner), Is.EqualTo(0f));
            Assert.That(Progress(entMan, protect, owner), Is.EqualTo(1f));
            entMan.System<MobStateSystem>().ChangeMobState(body, MobState.Dead);
            Assert.That(Progress(entMan, kill, owner), Is.EqualTo(1f));
            Assert.That(Progress(entMan, protect, owner), Is.EqualTo(0f));

            var replacement = entMan.SpawnEntity("MobHuman", map.GridCoords);
            minds.TransferTo(target, replacement);
            Assert.That(Progress(entMan, kill, owner), Is.EqualTo(0f), "The dead former body is no longer the target.");
            Assert.That(Progress(entMan, protect, owner), Is.EqualTo(1f));
            minds.TransferTo(target, null, createGhost: false);
            Assert.That(Progress(entMan, kill, owner), Is.EqualTo(1f));
            Assert.That(Progress(entMan, protect, owner), Is.EqualTo(0f));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ShuntReturnsTheSameBrainAndPreservesMindAndSurvival()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        EntityUid apc = default;
        EntityUid ai = default;

        await server.WaitAssertion(() =>
        {
            var (core, brain) = SpawnAi(entMan, map.GridCoords);
            ai = brain;
            var minds = entMan.System<MindSystem>();
            var mind = minds.CreateMind(null, "Shunting AI");
            minds.TransferTo(mind, ai);
            entMan.EnsureComponent<MalfAiMarkerComponent>(ai);
            var survive = entMan.SpawnEntity("MalfAiSurviveObjective", MapCoordinates.Nullspace);
            apc = entMan.SpawnEntity("APCBasic", map.GridCoords);
            var shunt = new MalfAiShuntToApcActionEvent { Performer = ai, Target = apc };
            entMan.EventBus.RaiseLocalEvent(ai, shunt);
            Assert.That(shunt.Handled, Is.True);
            Assert.That(Holder(entMan, apc).ContainedEntities, Is.EqualTo(new[] { ai }));
            Assert.That(Holder(entMan, core).ContainedEntities, Is.Empty);
            Assert.That(mind.Comp.OwnedEntity, Is.EqualTo(ai));
            Assert.That(Progress(entMan, survive, mind), Is.EqualTo(1f));
            var returnAction = entMan.GetComponent<MalfAiShuntedComponent>(ai).ReturnAction;
            Assert.That(entMan.System<SharedActionsSystem>().GetActions(ai).Select(action => action.Owner), Does.Contain(returnAction));

            var ret = new MalfAiReturnToCoreActionEvent { Performer = ai };
            entMan.EventBus.RaiseLocalEvent(ai, ret);
            Assert.That(ret.Handled, Is.True);
            Assert.That(Holder(entMan, core).ContainedEntities, Is.EqualTo(new[] { ai }));
            Assert.That(Holder(entMan, apc).ContainedEntities, Is.Empty);
            Assert.That(mind.Comp.OwnedEntity, Is.EqualTo(ai));
            Assert.That(Progress(entMan, survive, mind), Is.EqualTo(1f));
            Assert.That(entMan.System<SharedActionsSystem>().GetActions(ai).Select(action => action.Owner), Does.Not.Contain(returnAction));
        });
        await server.WaitRunTicks(2);
        await server.WaitAssertion(() => Assert.That(entMan.HasComponent<StationAiHolderComponent>(apc), Is.False));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CardingAbortsDoomsdayAndUploadAllowsARealCountdownToComplete()
    {
        // Completion starts a map-wide ripple with private timing state, so this pair must not be reused.
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        EntityUid station = default;
        EntityUid core = default;
        EntityUid ai = default;
        EntityUid card = default;
        EntityUid user = default;
        EntityUid sabotage = default;
        EntityUid survive = default;
        Entity<MindComponent> mind = default;
        var oldDuration = server.CfgMan.GetCVar(CCVars.MalfAiDoomsdayDuration);

        await server.WaitAssertion(() =>
        {
            server.CfgMan.SetCVar(CCVars.MalfAiDoomsdayDuration, 60f);
            station = entMan.SpawnEntity("MalfCounterplayTestStation", MapCoordinates.Nullspace);
            entMan.System<StationSystem>().AddGridToStation(station, map.Grid);
            entMan.System<AlertLevelSystem>().SetLevel(station, "blue", playSound: false, announce: false, force: true, locked: true);
            (core, ai) = SpawnAi(entMan, map.GridCoords);
            entMan.EnsureComponent<MalfAiMarkerComponent>(ai);
            mind = entMan.System<MindSystem>().CreateMind(null, "Doomsday AI");
            entMan.System<MindSystem>().TransferTo(mind, ai);
            sabotage = entMan.SpawnEntity("MalfAiDoomsdayObjective", MapCoordinates.Nullspace);
            survive = entMan.SpawnEntity("MalfAiSurviveObjective", MapCoordinates.Nullspace);
            StartDoomsday(entMan, ai);
            Assert.That(entMan.GetComponent<MalfAiDoomsdayComponent>(ai).Active, Is.True);
            Assert.That(entMan.GetComponent<AlertLevelComponent>(station).CurrentLevel, Is.EqualTo("cyan"));
            Assert.That(Progress(entMan, sabotage, mind), Is.EqualTo(0f));
            Assert.That(Progress(entMan, survive, mind), Is.EqualTo(1f));

            user = entMan.SpawnEntity("MobHuman", map.GridCoords);
            card = entMan.SpawnEntity("Intellicard", map.GridCoords);
        });
        await server.WaitAssertion(() =>
        {
            var transfer = new IntellicardDoAfterEvent
            {
                DoAfter = new Content.Shared.DoAfter.DoAfter(0,
                    new DoAfterArgs(entMan, user, TimeSpan.Zero, new IntellicardDoAfterEvent(), core, card, card),
                    TimeSpan.Zero),
            };
            entMan.EventBus.RaiseLocalEvent(core, transfer);
            Assert.That(transfer.Handled, Is.True);
            Assert.That(Holder(entMan, card).ContainedEntities, Is.EqualTo(new[] { ai }));
            Assert.That(Holder(entMan, core).ContainedEntities, Is.Empty);
            Assert.That(mind.Comp.OwnedEntity, Is.EqualTo(ai));
            Assert.That(entMan.GetComponent<MalfAiDoomsdayComponent>(ai).Active, Is.False);
            Assert.That(entMan.GetComponent<AlertLevelComponent>(station).CurrentLevel, Is.EqualTo("blue"));
            Assert.That(entMan.GetComponent<AlertLevelComponent>(station).IsLevelLocked, Is.True);
            Assert.That(Progress(entMan, survive, mind), Is.EqualTo(0f));
            Assert.That(Progress(entMan, sabotage, mind), Is.EqualTo(0f));

            transfer = new IntellicardDoAfterEvent
            {
                DoAfter = new Content.Shared.DoAfter.DoAfter(0,
                    new DoAfterArgs(entMan, user, TimeSpan.Zero, new IntellicardDoAfterEvent(), core, card, card),
                    TimeSpan.Zero),
            };
            entMan.EventBus.RaiseLocalEvent(core, transfer);
            Assert.That(transfer.Handled, Is.True);
            Assert.That(Holder(entMan, core).ContainedEntities, Is.EqualTo(new[] { ai }));
            Assert.That(Progress(entMan, survive, mind), Is.EqualTo(1f));
        });
        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<MalfAiDoomsdayComponent>(ai), Is.False);
            server.CfgMan.SetCVar(CCVars.MalfAiDoomsdayDuration, 0.05f);
            StartDoomsday(entMan, ai);
            Assert.That(Progress(entMan, sabotage, mind), Is.EqualTo(0f));
        });
        await server.WaitRunTicks(10);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<MalfAiDoomsdayComponent>(ai), Is.False);
            Assert.That(Progress(entMan, sabotage, mind), Is.EqualTo(1f));
            Assert.That(Progress(entMan, survive, mind), Is.EqualTo(1f));
            server.CfgMan.SetCVar(CCVars.MalfAiDoomsdayDuration, oldDuration);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BreakingCoreKillsAiAbortsDoomsdayAndFailsSurvival()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        EntityUid ai = default;
        EntityUid core = default;
        EntityUid survive = default;
        EntityUid sabotage = default;
        Entity<MindComponent> mind = default;

        await server.WaitAssertion(() =>
        {
            var station = entMan.SpawnEntity("MalfCounterplayTestStation", MapCoordinates.Nullspace);
            entMan.System<StationSystem>().AddGridToStation(station, map.Grid);
            (core, ai) = SpawnAi(entMan, map.GridCoords);
            entMan.EnsureComponent<MalfAiMarkerComponent>(ai);
            mind = entMan.System<MindSystem>().CreateMind(null, "AI core damage");
            entMan.System<MindSystem>().TransferTo(mind, ai);
            survive = entMan.SpawnEntity("MalfAiSurviveObjective", MapCoordinates.Nullspace);
            sabotage = entMan.SpawnEntity("MalfAiDoomsdayObjective", MapCoordinates.Nullspace);
            StartDoomsday(entMan, ai);
            Assert.That(entMan.GetComponent<MalfAiDoomsdayComponent>(ai).Active, Is.True);
            entMan.System<DamageableSystem>().TryChangeDamage(core,
                new DamageSpecifier { DamageDict = { ["Blunt"] = 500 } }, ignoreResistances: true);
            Assert.That(entMan.System<MobStateSystem>().IsDead(ai), Is.True);
            Assert.That(Progress(entMan, survive, mind), Is.EqualTo(0f));
        });
        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<MalfAiDoomsdayComponent>(ai), Is.False);
            Assert.That(Progress(entMan, sabotage, mind), Is.EqualTo(0f));
            entMan.System<DamageableSystem>().TryChangeDamage(core,
                new DamageSpecifier { DamageDict = { ["Blunt"] = 3000 } }, ignoreResistances: true);
        });
        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(core), Is.True);
            Assert.That(Progress(entMan, survive, mind), Is.EqualTo(0f));
        });
        await pair.CleanReturnAsync();
    }

    private static (EntityUid Core, EntityUid Ai) SpawnAi(IEntityManager entMan, EntityCoordinates coords)
    {
        var core = entMan.SpawnEntity("PlayerStationAiEmpty", coords);
        entMan.System<SharedPowerReceiverSystem>().SetNeedsPower(core, false);
        var ai = entMan.SpawnEntity("StationAiBrain", coords);
        Assert.That(entMan.System<SharedContainerSystem>().Insert(ai, Holder(entMan, core)), Is.True);
        return (core, ai);
    }

    private static ContainerSlot Holder(IEntityManager entMan, EntityUid holder)
        => entMan.System<SharedContainerSystem>().GetContainer(holder, StationAiHolderComponent.Container) as ContainerSlot
           ?? throw new AssertionException("AI holder is missing its brain slot.");

    private static float? Progress(IEntityManager entMan, EntityUid objective, Entity<MindComponent> mind)
        => entMan.System<ObjectivesSystem>().GetProgress(objective, mind);

    private static void StartDoomsday(IEntityManager entMan, EntityUid ai)
    {
        var action = new MalfAiDoomsdayActionEvent { Performer = ai };
        entMan.EventBus.RaiseLocalEvent(ai, action);
        Assert.That(action.Handled, Is.True);
    }

}
