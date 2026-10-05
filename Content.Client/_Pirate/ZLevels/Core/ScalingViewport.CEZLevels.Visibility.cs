// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client._Pirate.ZLevels.Core;
using Content.Shared._Pirate.ZLevels.Apertures.Components;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;

namespace Content.Client.Viewport;

public sealed partial class ScalingViewport
{
    private readonly HashSet<int> _zApertureRequiredTargets = new();
    // Reused per frame: explicit stackalloc is rejected by the client sandbox.
    private readonly Vector2[] _zVisibilityCorners = new Vector2[4];

    // Only cull when one grid demonstrably covers the complete current viewport. Missing
    // grids, extreme zoom and uncertain geometry retain the existing lower-deck passes.
    private bool CanCullLowerZLevels(EntityUid mapUid, IClydeViewport viewport)
    {
        if (_fallbackEye is not { } eye || !_mapQuery!.Value.TryComp(mapUid, out var map) ||
            eye.Position.MapId != map.MapId ||
            HasVisibleZLevelAperture(mapUid, eye))
            return false;

        var target = viewport.RenderTarget;
        var size = target.Size;
        if (size.X <= 0 || size.Y <= 0)
            return false;

        var center = target.LocalToWorld(eye, size / 2f, viewport.RenderScale);
        if (!_mapManager.TryFindGridAt(mapUid, center, out var gridUid, out var grid))
            return false;

        _transform ??= _entityManager.System<SharedTransformSystem>();
        var corners = _zVisibilityCorners;
        corners[0] = target.LocalToWorld(eye, Vector2.Zero, viewport.RenderScale);
        corners[1] = target.LocalToWorld(eye, new Vector2(size.X, 0), viewport.RenderScale);
        corners[2] = target.LocalToWorld(eye, new Vector2(0, size.Y), viewport.RenderScale);
        corners[3] = target.LocalToWorld(eye, new Vector2(size.X, size.Y), viewport.RenderScale);

        if (!TryGetZVisibilityTileBounds(corners, _transform.GetInvWorldMatrix(gridUid), grid.TileSize,
                out var start, out var end))
            return false;

        // This cache already invalidates on tile changes/LastTileModifiedTick. A missing tile
        // or ZTransparent tile anywhere in the conservative rectangle prevents culling.
        return !_zLevels!.HasVisualOpeningInTileBounds((gridUid, grid), start, end);
    }

    internal static bool TryGetZVisibilityTileBounds(Vector2[] corners, Matrix3x2 worldToGrid,
        float tileSize, out Vector2i start, out Vector2i end)
    {
        start = end = default;
        if (corners.Length != 4 || !float.IsFinite(tileSize) || tileSize <= 0)
            return false;

        var min = new Vector2(float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity);
        foreach (var corner in corners)
        {
            var local = Vector2.Transform(corner, worldToGrid) / tileSize;
            if (!float.IsFinite(local.X) || !float.IsFinite(local.Y) ||
                MathF.Abs(local.X) > 1_000_000 || MathF.Abs(local.Y) > 1_000_000)
                return false;

            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }

        // One tile of padding covers rasterization and sub-tile camera movement at the edge.
        start = new Vector2i((int) MathF.Floor(min.X) - 1, (int) MathF.Floor(min.Y) - 1);
        end = new Vector2i((int) MathF.Floor(max.X) + 1, (int) MathF.Floor(max.Y) + 1);
        return (long) (end.X - start.X + 1) * (end.Y - start.Y + 1) <= 4096;
    }

    private bool HasVisibleZLevelAperture(EntityUid mapUid, IEye eye)
    {
        if (_viewport == null)
            return true;

        _transform ??= _entityManager.System<SharedTransformSystem>();
        var bounds = UIBox2.FromDimensions(Vector2.Zero, _viewport.Size);
        var query = _entityManager.EntityQueryEnumerator<CEZLevelApertureComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var aperture, out var xform))
        {
            if (xform.MapUid != mapUid || aperture.TargetDepth != -1 ||
                aperture.SpritePixelSize <= 0 || aperture.PixelSize.X <= 0 || aperture.PixelSize.Y <= 0)
                continue;

            // Use the same sprite rotation, offset, scale and eye projection as the aperture draw.
            if (GetApertureViewportQuad(uid, aperture, xform, eye).Bounds.Intersects(bounds))
                return true;
        }

        return false;
    }

    private void CollectRequiredZLevelApertureTargets(TransformComponent playerXform,
        EntityUid? effectiveGridUid, int lowestDepth, int highestDepth)
    {
        for (var depth = lowestDepth + 1; depth <= highestDepth; depth++)
        {
            var mapUid = playerXform.MapUid!.Value;
            IEye eye = _fallbackEye!;
            if (depth != 0)
            {
                if (!TryResolveZMapEntity(mapUid, effectiveGridUid, depth, out mapUid, out var mapId, out var peer))
                    continue;

                eye = new ZEye(lowestDepth, depth, highestDepth)
                {
                    Position = GetResolvedEyePosition(playerXform, effectiveGridUid, peer, mapId),
                    Offset = _fallbackEye!.Offset + (-_fallbackEye.Rotation).ToWorldVec() *
                        CEClientZLevelsSystem.ZLevelOffset * depth,
                    Rotation = _fallbackEye.Rotation,
                    Scale = _fallbackEye.Scale,
                };
            }

            if (HasVisibleZLevelAperture(mapUid, eye))
                _zApertureRequiredTargets.Add(depth - 1);
        }
    }
}
