// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Server.GameTicking.Presets;
using Content.Server.Maps;
using Content.Shared.Maps;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate;

[TestFixture]
public sealed class PirateArenaMapTest
{
    [Test]
    public async Task OrdinaryPresetsAndRotationExcludeArenaContent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            HashSet<string> arenas = ["Suspicion", "GunGame", "CaptureTheFlag"];
            var arenaPools = new HashSet<string>();
            var arenaRules = new HashSet<string>();
            // Ordinary random gifts and chameleon disguises both use the global spawnable entity catalogue.
            string[] arenaEquipment = ["ClothingOuterCoatCaptainLongcoat", "WeaponSniperElephant", "CaptureTheFlagRedBeret", "CaptureTheFlagBlueFlag", "CaptureTheFlagCollectorCover", "SusTraitorUplinkImplant"];
            foreach (var id in arenaEquipment)
                Assert.That(server.ProtoMan.Index<EntityPrototype>(id).HideSpawnMenu, Is.True, id);

            foreach (var id in arenas)
            {
                var preset = server.ProtoMan.Index<GamePresetPrototype>(id);
                Assert.That(preset.ShowInVote, Is.False, id);
                arenaPools.Add(preset.MapPool!.Value.Id);
                arenaRules.UnionWith(preset.Rules.Select(rule => rule.Id));
            }

            var maps = server.ProtoMan.EnumeratePrototypes<GameMapPrototype>()
                .Where(map => arenas.Any(folder => map.MapPath.ToString().StartsWith($"/Maps/_Pirate/{folder}/", System.StringComparison.Ordinal)))
                .ToArray();
            Assert.That(maps, Is.Not.Empty);
            Assert.That(maps.Any(map => map.Fallback), Is.False);
            var arenaMaps = maps.Select(map => map.ID).ToHashSet();
            foreach (var pool in server.ProtoMan.EnumeratePrototypes<GameMapPoolPrototype>().Where(pool => !arenaPools.Contains(pool.ID)))
                Assert.That(pool.Maps.Any(map => arenaMaps.Contains(map.Id)), Is.False, pool.ID);

            foreach (var preset in server.ProtoMan.EnumeratePrototypes<GamePresetPrototype>().Where(preset => !arenas.Contains(preset.ID)))
                Assert.That(preset.Rules.Any(rule => arenaRules.Contains(rule.Id)), Is.False, preset.ID);
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
