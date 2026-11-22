// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Linq;
using System.Text.Json;
using Content.Shared.Pinpointer;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Content.IntegrationTests.Tests._Pirate.Pinpointer;

[TestFixture]
public sealed class NavMapMeasurementTest
{
    [Test]
    public async Task MeasureDelta()
    {
        var path = Environment.GetEnvironmentVariable("PIRATE_NAVMAP_BENCH_OUTPUT");
        if (string.IsNullOrEmpty(path))
            Assert.Ignore("Only run during the pull request comparison.");

        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            const int clients = 34;
            const int batches = 31;
            var entities = server.EntMan;
            var uid = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var component = entities.EnsureComponent<NavMapComponent>(uid);
            var baseline = component.CreationTick + 3u;
            for (var x = 0; x < 16; x++)
            for (var y = 0; y < 16; y++)
            {
                var origin = new Vector2i(x, y);
                var chunk = new NavMapChunk(origin);
                Array.Fill(chunk.TileData, SharedNavMapSystem.FloorMask);
                component.Chunks.Add(origin, chunk);
            }
            component.Chunks[Vector2i.Zero].LastUpdate = baseline;

            var session = server.PlayerMan.Sessions.Single();
            var serializer = server.ResolveDependency<IRobustSerializer>();
            using var stream = new MemoryStream();
            long Serialize(GameTick from)
            {
                stream.SetLength(0);
                var state = entities.GetComponentState(entities.EventBus, component, session, from);
                serializer.Serialize(stream, state!);
                return stream.Length;
            }

            var fullBytes = Serialize(GameTick.Zero);
            var deltaBytes = Serialize(baseline);
            var times = new double[batches];
            var allocations = new long[batches];
            for (var batch = -15; batch < batches; batch++)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                var start = Stopwatch.GetTimestamp();
                for (var n = 0; n < clients; n++)
                    Serialize(baseline);
                if (batch < 0)
                    continue;
                times[batch] = Stopwatch.GetElapsedTime(start).TotalMicroseconds;
                allocations[batch] = GC.GetAllocatedBytesForCurrentThread() - before;
            }

            Array.Sort(times);
            Array.Sort(allocations);
            var result = new { clients, chunks = component.Chunks.Count, modifiedChunks = 1, batches,
                fullBytes, deltaBytes, medianAllocatedPerBatch = allocations[batches / 2],
                medianMicrosecondsPerBatch = times[batches / 2] };
            File.WriteAllText(path!, JsonSerializer.Serialize(result));
            TestContext.Progress.WriteLine(JsonSerializer.Serialize(result));
            Assert.That(deltaBytes, Is.InRange(1L, fullBytes));
            entities.DeleteEntity(uid);
        });
        await pair.CleanReturnAsync();
    }
}
