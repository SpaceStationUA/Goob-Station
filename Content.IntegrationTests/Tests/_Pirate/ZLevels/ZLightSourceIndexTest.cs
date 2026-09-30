// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Content.Client._Pirate.ZLevels.Lighting;
using Content.Shared.CCVar;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Pirate.ZLevels;

[TestFixture]
public sealed class ZLightSourceIndexTest
{
    [Test]
    public async Task IndexMatchesOriginalAcrossLightLifecycleAndParentMapChanges()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var client = pair.Client;
        await client.WaitAssertion(() =>
        {
            var entities = client.EntMan;
            var maps = client.System<SharedMapSystem>();
            var transforms = client.System<SharedTransformSystem>();
            var metadata = client.System<MetaDataSystem>();
            var lights = client.System<PointLightSystem>();
            var system = client.System<CMUZLevelProjectedLightingSystem>();
            var firstMap = maps.CreateMap(out var firstId);
            var secondMap = maps.CreateMap(out var secondId);
            client.CfgMan.SetCVar(CCVars.ZProjectedLightSourceIndex, 2);

            // Drive the actual discovery method without requiring a player/viewer or drawing a frame.
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var sourceMaps = (HashSet<MapId>) typeof(CMUZLevelProjectedLightingSystem)
                .GetField("_sourceMaps", flags)!.GetValue(system)!;
            var discover = typeof(CMUZLevelProjectedLightingSystem).GetMethod("DiscoverSourceLights", flags)!;
            sourceMaps.Clear();
            sourceMaps.Add(firstId);
            var bounds = new Box2Rotated(new Box2(-5, -5, 5, 5), Angle.FromDegrees(35), Vector2.Zero);

            void Check(int expectedSources, bool? rebuilt = null)
            {
                discover.Invoke(system, new object[] { bounds, 0.01f });
                var stats = system.SourceStats;
                Assert.That(stats.Compared, Is.True);
                Assert.That(stats.Mismatch, Is.False, "Index source values/order differ from the original scan.");
                Assert.That(stats.Accepted, Is.EqualTo(expectedSources));
                if (rebuilt != null)
                    Assert.That(stats.IndexRebuilds, Is.EqualTo(rebuilt.Value ? 1 : 0));
            }

            EntityUid SpawnLight(MapId map)
            {
                var uid = entities.SpawnEntity(null, new MapCoordinates(Vector2.Zero, map));
                entities.AddComponent<PointLightComponent>(uid);
                lights.SetRadius(uid, 3f);
                lights.SetEnergy(uid, 2f);
                return uid;
            }

            var first = SpawnLight(firstId);
            var other = SpawnLight(secondId);
            Check(1, true);
            Check(1, false);
            lights.SetEnabled(first, false);
            Check(0, false);
            lights.SetEnabled(first, true);
            lights.SetContainerOccluded(first, true); // The original projection includes these lights.
            Check(1, false);
            transforms.SetCoordinates(first, new EntityCoordinates(firstMap, new Vector2(50, 0)));
            Check(0, false);
            lights.SetRadius(first, 100f); // A far-away light can reach the view after a radius change.
            Check(1, false);
            lights.SetEnergy(first, 0f);
            Check(0, false);
            lights.SetEnergy(first, 2f);
            entities.AddComponent<CMUZProjectedLightComponent>(first);
            Check(0, true);
            entities.RemoveComponent<CMUZProjectedLightComponent>(first);
            Check(1, true);
            metadata.SetEntityPaused(first, true);
            Check(0, true);
            metadata.SetEntityPaused(first, false);
            Check(1, true);

            var carrier = entities.SpawnEntity(null, new MapCoordinates(Vector2.Zero, secondId));
            transforms.SetCoordinates(other, new EntityCoordinates(carrier, Vector2.Zero));
            Check(1);
            transforms.SetCoordinates(carrier, new EntityCoordinates(firstMap, Vector2.Zero));
            Check(2, true); // Recursive parent map changes must move child lights into the index.
            metadata.RemoveFlag(other, MetaDataFlags.ExtraTransformEvents);
            transforms.SetCoordinates(carrier, new EntityCoordinates(secondMap, Vector2.Zero));
            Check(1, true); // Another system releasing the shared flag must not break our subscription.
            entities.RemoveComponent<PointLightComponent>(first);
            Check(0, true);
            first = SpawnLight(firstId); // Reused entity-query slots must preserve original tie order.
            Check(1, true);
            entities.DeleteEntity(first);
            Check(0, true);
            entities.DeleteEntity(carrier);
            entities.DeleteEntity(firstMap);
            entities.DeleteEntity(secondMap);
        });
        await pair.CleanReturnAsync();
    }
}
