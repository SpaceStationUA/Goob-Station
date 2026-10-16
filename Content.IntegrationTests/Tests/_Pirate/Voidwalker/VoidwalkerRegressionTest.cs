// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Pirate.Server.Voidwalker.Kidnapping;
using Content.Pirate.Server.Voidwalker.Kidnapping.Voided;
using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Pirate.Voidwalker;

[TestFixture]
public sealed partial class VoidwalkerRegressionTest
{
    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  id: VoidwalkerRegressionDummy
  components:
  - type: DoAfter
""";

    [Serializable, NetSerializable]
    private sealed partial class GazeDoAfterEvent : DoAfterEvent
    {
        public override DoAfterEvent Clone() => this;
    }

    [TestCase(false, 1f, false)]
    [TestCase(false, 25f, true)]
    [TestCase(true, 1f, true)]
    public async Task GazeStillChecksRangeAndMap(bool changeMap, float distance, bool cancelled)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var maps = entMan.System<SharedMapSystem>();
        var transform = entMan.System<SharedTransformSystem>();
        var doAfter = entMan.System<SharedDoAfterSystem>();
        var ev = new GazeDoAfterEvent();
        EntityUid firstMap = default;
        EntityUid secondMap = default;
        EntityUid target = default;
        MapId firstMapId = default;
        MapId secondMapId = default;

        await server.WaitPost(() =>
        {
            firstMap = maps.CreateMap(out firstMapId);
            secondMap = maps.CreateMap(out secondMapId);
            var user = entMan.SpawnEntity("VoidwalkerRegressionDummy", new MapCoordinates(Vector2.Zero, firstMapId));
            target = entMan.SpawnEntity("VoidwalkerRegressionDummy", new MapCoordinates(Vector2.UnitX, firstMapId));
            var args = new DoAfterArgs(entMan, user, TimeSpan.FromSeconds(2), ev, null, target)
            {
                Broadcast = true,
                NeedHand = false,
                RequireCanInteract = false,
                DistanceThreshold = 20,
                IgnoreObstruction = true,
            };
            Assert.That(doAfter.TryStartDoAfter(args), Is.True);
        });

        await server.WaitRunTicks(1);
        Assert.That(ev.Cancelled, Is.False);

        await server.WaitPost(() => transform.SetMapCoordinates(target,
            new MapCoordinates(new Vector2(distance, 0), changeMap ? secondMapId : firstMapId)));
        await server.WaitRunTicks(1);
        Assert.That(ev.Cancelled, Is.EqualTo(cancelled));

        await server.WaitPost(() =>
        {
            entMan.DeleteEntity(firstMap);
            entMan.DeleteEntity(secondMap);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task KidnappedBodyReturnsToNonStationOrigin()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var maps = entMan.System<SharedMapSystem>();
        EntityUid origin = default;
        EntityUid voidMap = default;
        EntityUid victim = default;
        EntityCoordinates originalCoordinates = default;

        await server.WaitPost(() =>
        {
            origin = maps.CreateMap();
            voidMap = maps.CreateMap(out var voidMapId);
            originalCoordinates = new EntityCoordinates(origin, new Vector2(2, 3));
            victim = entMan.SpawnEntity("VoidwalkerRegressionDummy", new MapCoordinates(Vector2.Zero, voidMapId));
            var kidnapped = entMan.AddComponent<VoidwalkerKidnappedComponent>(victim);
            kidnapped.OriginalMap = origin;
            kidnapped.OriginalCoordinates = originalCoordinates;
        });

        await server.WaitRunTicks(2);
        await server.WaitPost(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<TransformComponent>(victim).Coordinates, Is.EqualTo(originalCoordinates));
                Assert.That(entMan.HasComponent<VoidwalkerKidnappedComponent>(victim), Is.False);
            });
            entMan.DeleteEntity(origin);
            entMan.DeleteEntity(voidMap);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FailedSpaceReturnsDoNotBlockOtherVictimsOrVomiting()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var maps = entMan.System<SharedMapSystem>();
        var timing = server.ResolveDependency<IGameTiming>();
        EntityUid map = default;
        VoidedComponent first = null;
        VoidedComponent second = null;

        await server.WaitPost(() =>
        {
            map = maps.CreateMap(out var mapId);
            var firstVictim = entMan.SpawnEntity("VoidwalkerRegressionDummy", new MapCoordinates(Vector2.Zero, mapId));
            var secondVictim = entMan.SpawnEntity("VoidwalkerRegressionDummy", new MapCoordinates(Vector2.UnitX, mapId));
            first = entMan.AddComponent<VoidedComponent>(firstVictim);
            second = entMan.AddComponent<VoidedComponent>(secondVictim);
            first.NextVomitTime = TimeSpan.Zero;
            second.NextVomitTime = TimeSpan.Zero;
        });

        await server.WaitRunTicks(1);
        await server.WaitPost(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(first.NextSpacedCheck, Is.GreaterThan(timing.CurTime));
                Assert.That(second.NextSpacedCheck, Is.GreaterThan(timing.CurTime));
                Assert.That(first.NextVomitTime, Is.GreaterThan(timing.CurTime));
                Assert.That(second.NextVomitTime, Is.GreaterThan(timing.CurTime));
            });
            entMan.DeleteEntity(map);
        });
        await pair.CleanReturnAsync();
    }
}
