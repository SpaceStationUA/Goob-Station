// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using Content.Shared.Pinpointer;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Pirate.Pinpointer;

[TestFixture]
public sealed class NavMapDeltaTest
{
    [Test]
    public async Task TileEditsAndRemovalsPreserveTheAcknowledgedMap()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var maps = server.System<SharedMapSystem>();
        var clock = server.ResolveDependency<IGameTiming>();
        var serializer = server.ResolveDependency<IRobustSerializer>();
        var edge = new Vector2i(SharedNavMapSystem.ChunkSize * 2, 0);
        void SetTile(Vector2i position, Tile tile) => maps.SetTile(map.Grid.Owner, map.Grid.Comp, position, tile);

        IComponentState Snapshot(GameTick from)
        {
            var entities = server.EntMan;
            var component = entities.GetComponent<NavMapComponent>(map.Grid.Owner);
            var state = entities.GetComponentState(entities.EventBus, component, null, from);
            using var stream = new MemoryStream();
            serializer.Serialize(stream, state!);
            stream.Position = 0;
            return serializer.Deserialize<IComponentState>(stream);
        }

        await server.WaitPost(() =>
        {
            for (var x = 1; x < edge.X; x++)
                SetTile(new Vector2i(x, 0), map.Tile.Tile);
            server.System<Content.Server.Pinpointer.NavMapSystem>().EnsureNavMap(map.Grid.Owner);
        });
        await pair.RunTicksSync(2);
        IComponentState baseline = default!;
        IComponentDeltaState edit = default!, removal = default!;
        await server.WaitPost(() =>
        {
            baseline = Snapshot(GameTick.Zero);
            SetTile(new Vector2i(0, 1), map.Tile.Tile);
            SetTile(edge, map.Tile.Tile);
            edit = (IComponentDeltaState) Snapshot(clock.CurTick);
        });
        await pair.RunTicksSync(2);
        await server.WaitPost(() =>
        {
            SetTile(edge, Tile.Empty);
            removal = (IComponentDeltaState) Snapshot(clock.CurTick);
        });

        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.EntMan;
            var uid = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var mapComponent = entities.EnsureComponent<NavMapComponent>(uid);
            void Apply(IComponentState state)
            {
                var message = new ComponentHandleState(state, null);
                entities.EventBus.RaiseComponentEvent(uid, mapComponent, ref message);
            }

            var rebuilt = edit.CreateNewFullState(baseline);
            Apply(baseline);
            Assert.That(mapComponent.Chunks.Keys, Is.EquivalentTo(new[] { Vector2i.Zero, new Vector2i(1, 0) }));
            Assert.That(mapComponent.Chunks[new Vector2i(1, 0)].TileData[0], Is.EqualTo(SharedNavMapSystem.FloorMask));
            Assert.That(mapComponent.Chunks[Vector2i.Zero].TileData[1], Is.Zero);
            Apply(rebuilt);
            Assert.That(mapComponent.Chunks[Vector2i.Zero].TileData[1], Is.EqualTo(SharedNavMapSystem.FloorMask));
            Assert.That(mapComponent.Chunks[new Vector2i(2, 0)].TileData[0], Is.EqualTo(SharedNavMapSystem.FloorMask));
            Apply(baseline);
            Apply(edit);
            Assert.That(mapComponent.Chunks[new Vector2i(1, 0)].TileData[0], Is.EqualTo(SharedNavMapSystem.FloorMask));
            var version = mapComponent.DataVersion;
            Apply(removal);
            Assert.That(mapComponent.Chunks.Keys, Is.EquivalentTo(new[] { Vector2i.Zero, new Vector2i(1, 0) }));
            Assert.That(mapComponent.DataVersion, Is.GreaterThan(version));
            entities.DeleteEntity(uid);
        });

        await pair.CleanReturnAsync();
    }
}
