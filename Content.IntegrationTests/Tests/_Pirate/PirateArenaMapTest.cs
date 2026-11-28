// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.Maps;
using Content.Server.Voting.Managers;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Maps;
using Content.Shared.Voting;
using Robust.Shared.Enums;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate;

[TestFixture]
public sealed class PirateArenaMapTest
{
    private static readonly string[] ArenaPresets = ["Suspicion", "GunGame", "CaptureTheFlag"];

    [Test]
    public async Task OrdinaryPresetsAndRotationExcludeArenaContent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var arenaPools = new HashSet<string>();
            var arenaRules = new HashSet<string>();
            // Ordinary random gifts and chameleon disguises both use the global spawnable entity catalogue.
            string[] arenaEquipment = ["ClothingOuterCoatCaptainLongcoat", "WeaponSniperElephant", "CaptureTheFlagRedBeret", "CaptureTheFlagBlueFlag", "CaptureTheFlagCollectorCover", "SusTraitorUplinkImplant"];
            foreach (var id in arenaEquipment)
                Assert.That(server.ProtoMan.Index<EntityPrototype>(id).HideSpawnMenu, Is.True, id);

            foreach (var id in ArenaPresets)
            {
                var preset = server.ProtoMan.Index<GamePresetPrototype>(id);
                arenaPools.Add(preset.MapPool!.Value.Id);
                arenaRules.UnionWith(preset.Rules.Select(rule => rule.Id));
            }

            var maps = server.ProtoMan.EnumeratePrototypes<GameMapPrototype>()
                .Where(map => ArenaPresets.Any(folder => map.MapPath.ToString().StartsWith($"/Maps/_Pirate/{folder}/", System.StringComparison.Ordinal)))
                .ToArray();
            Assert.That(maps, Is.Not.Empty);
            Assert.That(maps.Any(map => map.Fallback), Is.False);
            var arenaMaps = maps.Select(map => map.ID).ToHashSet();
            foreach (var pool in server.ProtoMan.EnumeratePrototypes<GameMapPoolPrototype>().Where(pool => !arenaPools.Contains(pool.ID)))
                Assert.That(pool.Maps.Any(map => arenaMaps.Contains(map.Id)), Is.False, pool.ID);

            foreach (var preset in server.ProtoMan.EnumeratePrototypes<GamePresetPrototype>().Where(preset => !ArenaPresets.Contains(preset.ID)))
                Assert.That(preset.Rules.Any(rule => arenaRules.Contains(rule.Id)), Is.False, preset.ID);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LobbyBallotsUseOnlineCountAndTheSelectedPresetsMapPool()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        var votes = server.ResolveDependency<IVoteManager>();

        object[] Options(StandardVoteType type)
        {
            var previous = votes.ActiveVotes.ToArray();
            votes.CreateStandardVote(null, type);
            var ballot = votes.ActiveVotes.Except(previous).Single();
            var options = ballot.VotesPerOption.Keys.ToArray();
            ballot.Cancel();
            return options;
        }

        var online = 1;
        foreach (var count in new[] { 1, 2, 20, 21 })
        {
            await server.AddDummySessions(count - online);
            online = count;
            await server.WaitAssertion(() =>
            {
                Assert.That(server.PlayerMan.PlayerCount, Is.EqualTo(count));
                var choices = Options(StandardVoteType.Preset);
                foreach (var arena in ArenaPresets)
                    Assert.That(choices.Contains(arena), Is.EqualTo(count is >= 2 and <= 20), $"{arena}, online {count}");
            });
        }

        await server.WaitAssertion(() =>
        {
            var ticker = server.System<GameTicker>();
            foreach (var id in ArenaPresets.Append("PirateGreenshift"))
            {
                ticker.SetGamePreset(id);
                var mapChoices = Options(StandardVoteType.Map).Cast<GameMapPrototype>().ToArray();
                Assert.That(mapChoices, Is.Not.Empty, id);
                var pool = ticker.Preset!.MapPool is { } mapPool
                    ? server.ProtoMan.Index(mapPool)
                    : server.ProtoMan.Index<GameMapPoolPrototype>(server.CfgMan.GetCVar(CCVars.GameMapPool));
                Assert.That(mapChoices.Select(map => map.ID), Is.SubsetOf(pool.Maps.Select(map => map.Id)), id);
            }
        });
        // Dummy connections count toward the lobby population but do not implement a voting UI transport.
        await server.RemoveAllDummySessions();
        await server.WaitAssertion(() =>
        {
            Assert.That(server.PlayerMan.PlayerCount, Is.EqualTo(1));
            votes.Update();
        });
        await pair.RunTicksSync(5);
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ArenaMapBallotCannotReplaceTheNextStation(bool stationChosenBeforeMapResult)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var ticker = server.System<GameTicker>();
            var maps = server.ResolveDependency<IGameMapManager>();
            var votes = server.ResolveDependency<IVoteManager>();
            server.CfgMan.SetCVar(CCVars.GameMap, string.Empty);
            server.CfgMan.SetCVar(CCVars.VoteTimerMap, 0);
            ticker.SetGamePreset("CaptureTheFlag");
            ticker.DelayStart(TimeSpan.FromMinutes(5));
            Assert.That(ticker.CanUpdateMap(), Is.True);
            votes.CreateStandardVote(null, StandardVoteType.Map);
            var ballot = votes.ActiveVotes.Single();
            Assert.That(ballot.VotesPerOption.Keys.Cast<GameMapPrototype>().Single().ID, Is.EqualTo("CaptureTheFlagArena"));
            ballot.CastVote(pair.Player!, 0);

            if (stationChosenBeforeMapResult)
            {
                ticker.SetGamePreset("PirateGreenshift");
                maps.SelectMap("BoxTP");
                votes.Update();
                Assert.That(maps.GetSelectedMap()?.ID, Is.EqualTo("BoxTP"));
            }
            else
            {
                votes.Update();
                Assert.That(maps.GetSelectedMap()?.ID, Is.EqualTo("CaptureTheFlagArena"));
                ticker.SetGamePreset("PirateGreenshift");
            }

            var stationPool = server.ProtoMan.Index<GameMapPoolPrototype>(server.CfgMan.GetCVar(CCVars.GameMapPool));
            var selected = maps.GetSelectedMap()!.ID;
            Assert.That(stationPool.Maps.Select(map => map.Id), Does.Contain(selected));
            ticker.ToggleReady(pair.Player!, true);
            ticker.StartRound();
            Assert.That(ticker.CurrentPreset?.ID, Is.EqualTo("PirateGreenshift"));
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            Assert.That(maps.GetSelectedMap()?.ID, Is.EqualTo(selected));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ConfiguredArenaMapClearsForStationAndSharedDeathmatchMapSurvives()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var ticker = server.System<GameTicker>();
            var maps = server.ResolveDependency<IGameMapManager>();
            ticker.SetGamePreset("CaptureTheFlag");
            server.CfgMan.SetCVar(CCVars.GameMap, "CaptureTheFlagArena");
            ticker.SetGamePreset("PirateGreenshift");
            Assert.That(maps.GetSelectedMap()?.ID, Is.Not.EqualTo("CaptureTheFlagArena"));
            Assert.That(server.CfgMan.GetCVar(CCVars.GameMap), Is.Empty);

            ticker.SetGamePreset("GunGame");
            maps.SelectMap("dm01-entryway");
            ticker.SetGamePreset("Deathmatch");
            Assert.That(maps.GetSelectedMap()?.ID, Is.EqualTo("dm01-entryway"));
            ticker.SetGamePreset("GunGame");
            ticker.SetGamePreset("PirateGreenshift");
            Assert.That(maps.GetSelectedMap()?.ID, Is.Not.EqualTo("dm01-entryway"));
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("Suspicion", "1v1CitySSS")]
    [TestCase("GunGame", "MW-Dock")]
    [TestCase("CaptureTheFlag", "CaptureTheFlagArena")]
    public async Task AdminCanStartArenaAboveVotingLimit(string preset, string map)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        var guests = await server.AddDummySessions(20);
        await server.WaitPost(() =>
        {
            if (guests[0].Status == SessionStatus.Connected)
                server.PlayerMan.JoinGame(guests[0]);
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            var ticker = server.System<GameTicker>();
            Assert.That(server.PlayerMan.PlayerCount, Is.EqualTo(21));
            server.CfgMan.SetCVar(CCVars.GameMap, map);
            ticker.SetGamePreset(preset, resetDelay: 1);
            ticker.ToggleReady(pair.Player!, true);
            ticker.ToggleReady(guests[0], true);
            ticker.StartRound();
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound), preset);
            Assert.That(ticker.CurrentPreset?.ID, Is.EqualTo(preset));
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("MW-Dock")]
    [TestCase("CS-Dust2")]
    [TestCase("TF2-2Fort")]
    [TestCase("D1-Logic")]
    [TestCase("1v1CitySSS")]
    [TestCase("ReachSSS")]
    [TestCase("RooftopsSSS")]
    [TestCase("CentcommSSS")]
    [TestCase("CrashedReachSS")]
    [TestCase("TrainSSS")]
    public async Task ArenaCanBeLoaded(string id)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var loader = server.System<MapLoaderSystem>();
        var maps = server.System<SharedMapSystem>();
        await server.WaitAssertion(() =>
        {
            var prototype = server.ProtoMan.Index<GameMapPrototype>(id);
            Assert.That(loader.TryLoadMap(prototype.MapPath, out var map, out _), Is.True, id);
            var mapId = map!.Value.Comp.MapId;
            Assert.That(mapId, Is.Not.EqualTo(MapId.Nullspace), id);
            if (!maps.IsInitialized(mapId))
                maps.InitializeMap(mapId);

            maps.DeleteMap(mapId);
        });
        await pair.CleanReturnAsync();
    }
}
