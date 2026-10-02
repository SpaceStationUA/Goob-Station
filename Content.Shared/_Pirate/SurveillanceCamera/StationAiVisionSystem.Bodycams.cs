// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.SurveillanceCamera;
using Content.Shared.StationAi;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;

namespace Content.Shared.Silicons.StationAi;

public sealed partial class StationAiVisionSystem
{
    /// <summary>
    /// The component-filtered broadphase lookup cannot discover a camera inside a
    /// container that does not itself have StationAiVision. Include those cameras
    /// by their inherited grid position, including nested storage.
    /// </summary>
    private void AddCameraSeedsInContainers(Entity<BroadphaseComponent, MapGridComponent> grid, Box2 bounds)
    {
        var query = EntityQueryEnumerator<CameraActiveVisionComponent, StationAiVisionComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var vision, out var xform))
        {
            if (xform.GridUid != grid.Owner)
                continue;

            var localPosition = _maps.WorldToLocal(grid.Owner, grid.Comp2, _xforms.GetWorldPosition(xform));
            if (bounds.Contains(localPosition))
                _seeds.Add((uid, vision));
        }
    }
}
