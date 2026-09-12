// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Silicons.StationAi;
using Content.Shared.StationAi;
using Content.Shared.SurveillanceCamera.Components;
using Content.Shared.SurveillanceCamera;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;

namespace Content.IntegrationTests.Tests._Pirate;

[TestFixture]
public sealed class MalfAiCameraVisionTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task XrayRevealsOccludedTilesOnTranslatedAndRotatedGrids(bool moved)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        EntityUid camera = default;
        await server.WaitAssertion(() =>
        {
            var maps = entMan.System<SharedMapSystem>();
            for (var x = -8; x <= 8; x++)
            for (var y = -8; y <= 8; y++)
                maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), new Tile(1));

            if (moved)
            {
                var xforms = entMan.System<SharedTransformSystem>();
                xforms.SetWorldPosition(map.Grid.Owner, new Vector2(100, 200));
                xforms.SetWorldRotation(map.Grid.Owner, Angle.FromDegrees(90));
            }

            camera = entMan.SpawnEntity(null, new EntityCoordinates(map.Grid.Owner, 0.5f, 0.5f));
            entMan.EnsureComponent<StationAiVisionComponent>(camera);
            entMan.EnsureComponent<SurveillanceCameraComponent>(camera);
            // A full barrier keeps ordinary camera line of sight away from the target.
            for (var y = -8; y <= 8; y++)
                entMan.SpawnEntity("WallSolid", new EntityCoordinates(map.Grid.Owner, 1.5f, y + 0.5f));
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            var vision = entMan.System<StationAiVisionSystem>();
            var xforms = entMan.System<SharedTransformSystem>();
            var origin = xforms.GetWorldPosition(camera);
            var grid = new Entity<BroadphaseComponent, MapGridComponent>(map.Grid.Owner,
                entMan.GetComponent<BroadphaseComponent>(map.Grid.Owner), map.Grid.Comp);
            var target = new Vector2i(3, 0);
            Assert.That(vision.IsAccessible(grid, target), Is.False);
            Assert.That(vision.IsAccessible(grid, target, xrayCameras: true, xrayRange: 6, xrayOrigin: origin), Is.True);

            var tiles = new HashSet<Vector2i>();
            var bounds = new Box2Rotated(Box2.CenteredAround(origin, new Vector2(16, 16)));
            vision.GetView(grid, bounds, tiles);
            Assert.That(tiles, Does.Not.Contain(target));
            tiles.Clear();
            vision.GetView(grid, bounds, tiles, xrayCameras: true, xrayRange: 6, xrayOrigin: origin);
            Assert.That(tiles, Does.Contain(target));

            entMan.System<SharedSurveillanceCameraSystem>().SetActive(camera, false);
            Assert.That(vision.IsAccessible(grid, target, xrayCameras: true, xrayRange: 6, xrayOrigin: origin), Is.False);
        });
        await pair.CleanReturnAsync();
    }
}
