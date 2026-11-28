// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server._Pirate.GunGame.Components;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server._Pirate.Suspicion.GridMarker;
using Content.Shared.CCVar;
using Robust.Shared.Enums;
using Content.Server.KillTracking;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared._Pirate.Suspicion;
using Content.Shared._Pirate.Suspicion.SuspicionGameRule.Components;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate;

[TestFixture]
public sealed class PirateArenaRuleTest
{
    [Test]
    public async Task GunGameProgressionCountsOnlyAnotherParticipantOnce()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true, DummyTicker = false });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var ticker = server.System<GameTicker>();
            ticker.SetGamePreset("Sandbox", force: true, resetDelay: 1);
            Assert.That(ticker.StartGameRule("GunGame", out var ruleId));
            var rule = entities.GetComponent<GunGameRuleComponent>(ruleId);
            Assert.That(rule.RewardSpawnsQueue, Has.Count.EqualTo(31));
            var killer = new NetUserId(Guid.NewGuid());
            var player = new GunGamePlayerTrackingInfo(killer, new Queue<ProtoId<StartingGearPrototype>>(new ProtoId<StartingGearPrototype>[] { "GunGameChef", "GunGameTider" }));
            rule.KillsPerWeapon = 2;
            rule.PlayerInfo.Add(killer, player);

            void Kill(KillSource source, bool participant = true, bool suicide = false, bool duplicate = false)
            {
                var body = entities.SpawnEntity("MobHuman", map.GridCoords);
                if (participant)
                    entities.EnsureComponent<GunGameTrackerComponent>(body);
                var reported = new KillReportedEvent(body, source, null, suicide);
                entities.EventBus.RaiseLocalEvent(body, ref reported, true);
                if (duplicate)
                    entities.EventBus.RaiseLocalEvent(body, ref reported, true);
            }

            var attributed = new KillPlayerSource(killer);
            Kill(attributed, participant: false);
            Kill(new KillEnvironmentSource());
            Kill(attributed, suicide: true);
            Kill(new KillPlayerSource(new NetUserId(Guid.NewGuid())));
            Assert.That(player.Kills, Is.Zero);
            Kill(attributed, duplicate: true);
            Assert.That(player.Kills, Is.EqualTo(1));
            Assert.That(player.RewardQueue, Has.Count.EqualTo(2));
            Kill(attributed);
            Assert.That(player.RewardQueue.Peek().Id, Is.EqualTo("GunGameTider"));
            Assert.That(rule.Victor, Is.Null);
            Kill(attributed);
            Kill(attributed);
            Assert.That(rule.Victor, Is.EqualTo(killer));
            Assert.That(player.RewardQueue, Has.Count.EqualTo(1));
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("Suspicion", "CentcommSSS")]
    [TestCase("GunGame", "MW-Dock")]
    public async Task FullMapCooldownAndConfiguredStationStillLoadAnArena(string presetId, string fallback)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        var partner = await server.AddDummySession("MapPartner");
        await server.WaitPost(() =>
        {
            if (partner.Status == SessionStatus.Connected)
                server.PlayerMan.JoinGame(partner);
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            var ticker = server.System<GameTicker>();
            var maps = server.ResolveDependency<IGameMapManager>();
            server.CfgMan.SetCVar(CCVars.PirateMapRotationDontRepeatCount, 2);
            server.CfgMan.SetCVar(CCVars.GameMap, "BoxTP");
            ticker.SetGamePreset(presetId, resetDelay: 1);
            var pool = server.ProtoMan.Index(ticker.Preset!.MapPool!.Value);
            foreach (var id in pool.Maps)
                maps.TryAddPirateMapRotationUnavailablePool(server.ProtoMan.Index(id));

            Assert.That(maps.CurrentlyEligibleMaps().Select(map => map.ID), Is.EquivalentTo(new[] { fallback }));
            Assert.That(maps.GetSelectedMap()?.ID, Is.EqualTo("BoxTP"));
            ticker.ToggleReadyAll(true);
            ticker.StartRound();
            Assert.That(ticker.CurrentPreset?.ID, Is.EqualTo(presetId));
            Assert.That(maps.GetSelectedMap()?.ID, Is.EqualTo(fallback));
            Assert.That(ticker.DefaultMap, Is.Not.EqualTo(MapId.Nullspace));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ManualTrainSurvivesSelectingTheTttPreset()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var ticker = server.System<GameTicker>();
            var maps = server.ResolveDependency<IGameMapManager>();
            server.CfgMan.SetCVar(CCVars.GameMap, string.Empty);
            maps.SelectMap("TrainSSS");
            ticker.SetGamePreset("Suspicion", resetDelay: 1);
            Assert.That(maps.GetSelectedMap()?.ID, Is.EqualTo("TrainSSS"));
            Assert.That(maps.AllVotableMaps().Select(map => map.ID), Does.Not.Contain("TrainSSS"));
            ticker.ToggleReadyAll(true);
            ticker.StartRound(force: true);
            Assert.That(ticker.CurrentPreset?.ID, Is.EqualTo("Suspicion"));
            Assert.That(maps.GetSelectedMap()?.ID, Is.EqualTo("TrainSSS"));
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("Suspicion", "1v1CitySSS")]
    [TestCase("Suspicion", "CrashedReachSS")]
    [TestCase("GunGame", "MW-Dock")]
    public async Task ArenaParticipantsAndRulesAreRemovedOnNextShift(string preset, string map)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var ticker = server.System<GameTicker>();
        var other = await server.AddDummySession("ArenaPartner");
        await server.WaitPost(() =>
        {
            if (other.Status == SessionStatus.Connected)
                server.PlayerMan.JoinGame(other);
            server.CfgMan.SetCVar(CCVars.SSSPreparingDuration, 1);
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            server.CfgMan.SetCVar(CCVars.GameLobbyDefaultPreset, "Extended");
            server.CfgMan.SetCVar(CCVars.GameMap, map);
            ticker.SetGamePreset(preset, resetDelay: 1);
            ticker.ToggleReadyAll(true);
            ticker.StartRound();
            Assert.That(ticker.CurrentPreset?.ID, Is.EqualTo(preset));
            Assert.That(server.ResolveDependency<IGameMapManager>().GetSelectedMap()?.ID, Is.EqualTo(map));
        });
        await pair.RunTicksSync(server.Timing.TickRate * 2);
        await server.WaitAssertion(() =>
        {
            if (preset == "Suspicion")
            {
                Assert.That(entities.EntityQuery<SuspicionPlayerComponent>().Count(), Is.EqualTo(2));
                Assert.That(entities.EntityQuery<SuspicionRoleComponent>().Select(x => x.Role),
                    Is.EquivalentTo(new[] { SuspicionRole.Traitor, SuspicionRole.Detective }));
            }
            else
            {
                Assert.That(entities.EntityQuery<GunGameTrackerComponent>().Count(), Is.EqualTo(2));
                var game = entities.EntityQuery<GunGameRuleComponent>().Single();
                Assert.That(game.PlayerInfo, Has.Count.EqualTo(2));
                Assert.That(game.PlayerInfo.Values.All(player => player.RewardQueue.Count == 31));
            }

            ticker.RestartRound();
            Assert.That(ticker.Preset?.ID, Is.EqualTo("Extended"));
            server.CfgMan.SetCVar(CCVars.GameMap, "BoxTP");
            ticker.ToggleReadyAll(true);
            ticker.StartRound(force: true);
            Assert.That(ticker.CurrentPreset?.ID, Is.EqualTo("Extended"));
            Assert.That(server.ResolveDependency<IGameMapManager>().GetSelectedMap()?.ID, Is.EqualTo("BoxTP"));
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.EntityQuery<SuspicionRuleComponent>(), Is.Empty);
            Assert.That(entities.EntityQuery<SuspicionPlayerComponent>(), Is.Empty);
            Assert.That(entities.EntityQuery<SuspicionGridMarkerComponent>(), Is.Empty);
            Assert.That(entities.EntityQuery<SuspicionRoleComponent>(), Is.Empty);
            Assert.That(entities.EntityQuery<GunGameRuleComponent>(), Is.Empty);
            Assert.That(entities.EntityQuery<GunGameTrackerComponent>(), Is.Empty);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(SuspicionRole.Innocent, true)]
    [TestCase(SuspicionRole.Detective, true)]
    [TestCase(SuspicionRole.Traitor, false)]
    public async Task OnlyCrewCanGiveTheJesterAVictory(SuspicionRole killerRole, bool jesterWins)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true, DummyTicker = false, Connected = true, InLobby = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var ticker = server.System<GameTicker>();
            Assert.That(ticker.StartGameRule("Suspicion", out var ruleId));
            var rule = entities.GetComponent<SuspicionRuleComponent>(ruleId);
            rule.GameState = SuspicionGameState.InProgress;
            rule.EndAt = server.Timing.CurTime + TimeSpan.FromMinutes(8);
            var user = server.PlayerMan.Sessions.Single().UserId;
            EntityUid Assign(SuspicionRole role, NetUserId? id = null, SuspicionSubRole? subRole = null)
            {
                var body = entities.SpawnEntity("MobHuman", map.GridCoords);
                entities.EnsureComponent<SuspicionPlayerComponent>(body);
                var minds = server.System<MindSystem>();
                var mind = minds.CreateMind(id);
                minds.TransferTo(mind, body);
                var roles = server.System<RoleSystem>();
                roles.MindAddRole(mind, "MindRoleSuspicion");
                Assert.That(roles.MindHasRole<SuspicionRoleComponent>(mind, out var assigned));
                assigned!.Value.Comp2.Role = role;
                assigned.Value.Comp2.SubRole = subRole;
                return body;
            }
            Assign(killerRole, user);
            var victim = Assign(SuspicionRole.Wildcard, subRole: SuspicionSubRole.Jester);
            var ev = new KillReportedEvent(victim, new KillPlayerSource(user), null, false);
            entities.EventBus.RaiseLocalEvent(victim, ref ev, true);
            Assert.That(rule.GameState, Is.EqualTo(jesterWins ? SuspicionGameState.PostRound : SuspicionGameState.InProgress));
            Assert.That(rule.Winner, Is.EqualTo(jesterWins ? "pirate-ttt-win-jester" : null));
        });
        await pair.CleanReturnAsync();
    }
}
