// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client._Pirate.ZLevels.Core;
using Content.Shared._Pirate.ZLevels.Apertures.Components;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;
using Robust.Shared.Map.Components;

namespace Content.Client.Viewport;

public sealed partial class ScalingViewport
{
    // One viewport per crop size bucket (ZViewportCrop quantizes crops to eighths of the view).
    private const int MaxPooledCropViewports = 8;

    private readonly ZVisibilityMask _zVisibilityMask = new();
    private readonly Dictionary<int, List<UIBox2>> _zScreenRegions = new();
    private readonly HashSet<int> _zRegionalDepths = new();
    private readonly List<UIBox2> _zRegionApertures = new();
    private readonly List<ZCropViewport> _zCropViewports = new();
    private long _zCropUseCounter;
    private IClydeViewport? _zActiveCropViewport;
    private Vector2i _zCropFullSize;

    private sealed class ZCropViewport(IClydeViewport viewport)
    {
        public readonly IClydeViewport Viewport = viewport;
        public long LastUsed;
        public bool InUse;
    }

    private void PrepareLowerZRenderRegions(IClydeViewport viewport, TransformComponent viewer,
        EntityUid? effectiveGrid, int lowestDepth, bool enabled)
    {
        _zRegionalDepths.Clear();
        if (_zCropFullSize != viewport.Size || !enabled)
        {
            DisposeZCropViewports();
            _zCropFullSize = viewport.Size;
        }
        if (!enabled || lowestDepth >= 0 ||
            !_zVisibilityMask.Reset(viewport.RenderTarget.Size))
            return;

        _transform ??= _entityManager.System<SharedTransformSystem>();
        for (var depth = -1; depth >= lowestDepth; depth--)
        {
            // Walk from the viewer downwards, intersecting visibility at each intervening floor.
            // Projection uses that floor's eye, including its per-depth parallax and linked grid offset.
            var above = depth + 1;
            var mapUid = viewer.MapUid!.Value;
            var gridUid = effectiveGrid;
            IEye eye = _fallbackEye!;
            if (above != 0)
            {
                if (!TryResolveZMapEntity(mapUid, effectiveGrid, above, out mapUid, out var mapId, out gridUid))
                    continue; // Unknown geometry cannot prove occlusion.

                eye = new ZEye(lowestDepth, above, 0)
                {
                    Position = GetResolvedEyePosition(viewer, effectiveGrid, gridUid, mapId),
                    Offset = _fallbackEye!.Offset + (-_fallbackEye.Rotation).ToWorldVec() *
                        CEClientZLevelsSystem.ZLevelOffset * above,
                    Rotation = _fallbackEye.Rotation,
                    Scale = _fallbackEye.Scale,
                };
            }

            if (!_mapQuery!.Value.TryComp(mapUid, out var map) || eye.Position.MapId != map.MapId)
                continue;

            MapGridComponent? grid = null;
            if (gridUid is { } candidate && _xformQuery!.Value.TryComp(candidate, out var gridXform) &&
                gridXform.MapUid == mapUid)
                _entityManager.TryGetComponent(candidate, out grid);
            if (grid == null)
            {
                var center = viewport.RenderTarget.LocalToWorld(eye, viewport.RenderTarget.Size / 2f, viewport.RenderScale);
                if (!_mapManager.TryFindGridAt(mapUid, center, out var found, out grid))
                    continue;
                gridUid = found;
            }

            var inverse = _transform.GetInvWorldMatrix(gridUid!.Value);
            var screenToWorld = GetZScreenToWorld(eye, viewport.RenderTarget.Size, viewport.RenderScale);
            CollectRegionApertures(mapUid, eye);
            for (var y = 0; y < _zVisibilityMask.Rows; y++)
            {
                for (var x = 0; x < _zVisibilityMask.Columns; x++)
                {
                    if (!_zVisibilityMask.IsVisible(x, y))
                        continue;

                    var cell = _zVisibilityMask.CellBounds(x, y);
                    var apertureVisible = false;
                    foreach (var aperture in _zRegionApertures)
                    {
                        if (!aperture.Intersects(cell))
                            continue;
                        apertureVisible = true;
                        break;
                    }
                    if (apertureVisible)
                        continue;

                    FillZRegionCorners(screenToWorld, cell);
                    if (TryGetZVisibilityTileBounds(_zVisibilityCorners, inverse, grid.TileSize, out var start, out var end) &&
                        !_zLevels!.HasVisualOpeningInTileBounds((gridUid.Value, grid), start, end))
                        _zVisibilityMask.Hide(x, y);
                }
            }

            if (!_zScreenRegions.TryGetValue(depth, out var regions))
                _zScreenRegions[depth] = regions = new List<UIBox2>();
            if (_zVisibilityMask.TryGetRegions(regions))
                _zRegionalDepths.Add(depth);
        }
    }

    private void CollectRegionApertures(EntityUid mapUid, IEye eye)
    {
        _zRegionApertures.Clear();
        var query = _entityManager.EntityQueryEnumerator<CEZLevelApertureComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var aperture, out var xform))
        {
            if (xform.MapUid != mapUid || aperture.TargetDepth != -1 ||
                aperture.SpritePixelSize <= 0 || aperture.PixelSize.X <= 0 || aperture.PixelSize.Y <= 0)
                continue;

            var bounds = GetApertureViewportQuad(uid, aperture, xform, eye).Bounds;
            // Keep a cell of guard pixels for interpolation and texture sampling at aperture edges.
            _zRegionApertures.Add(new UIBox2(bounds.Left - ZVisibilityMask.CellSize,
                bounds.Top - ZVisibilityMask.CellSize, bounds.Right + ZVisibilityMask.CellSize,
                bounds.Bottom + ZVisibilityMask.CellSize));
        }
    }

    internal static Matrix3x2 GetZScreenToWorld(IEye eye, Vector2i size, Vector2 renderScale)
    {
        // Match IRenderTarget.LocalToWorld, calculating the inverse view once per deck rather
        // than recalculating its rotation/scale for every corner of every visibility cell.
        eye.GetViewMatrixInv(out var inverse, renderScale);
        return Matrix3x2.CreateTranslation(-size / 2f) *
               Matrix3x2.CreateScale(new Vector2(1, -1) / EyeManager.PixelsPerMeter) * inverse;
    }

    private void FillZRegionCorners(Matrix3x2 screenToWorld, UIBox2 region)
    {
        _zVisibilityCorners[0] = Vector2.Transform(new Vector2(region.Left, region.Top), screenToWorld);
        _zVisibilityCorners[1] = Vector2.Transform(new Vector2(region.Right, region.Top), screenToWorld);
        _zVisibilityCorners[2] = Vector2.Transform(new Vector2(region.Left, region.Bottom), screenToWorld);
        _zVisibilityCorners[3] = Vector2.Transform(new Vector2(region.Right, region.Bottom), screenToWorld);
    }

    private bool TryRenderLowerZRegion(IClydeViewport viewport, DrawingHandleScreen handle, IEye eye, int depth)
    {
        if (eye is not ZEye zEye || !_zRegionalDepths.Contains(depth))
            return false;

        var regions = _zScreenRegions[depth];
        if (regions.Count == 0)
        {
            // No pixels of this deck can survive the floors above. Still initialize the
            // composite if this is its first pass, so no previous-frame pixels can leak in.
            if (viewport.ClearColor != null)
                handle.RenderInRenderTarget(viewport.RenderTarget, () => { }, viewport.ClearColor);
            return true;
        }

        // Profiling showed each extra crop pass costs more than the pixels it saves.
        var pixelsPerMeter = eye.Scale * viewport.RenderScale * EyeManager.PixelsPerMeter;
        var padding = MathF.Max(pixelsPerMeter.X, pixelsPerMeter.Y) * 2 + 8;
        if (!ZViewportCrop.TryGetCrop(regions, viewport.Size, padding, out var crop))
            return false;

        if (viewport.ClearColor != null)
            handle.RenderInRenderTarget(viewport.RenderTarget, () => { }, viewport.ClearColor);

        var cropped = RentZCropViewport(crop.Size);
        cropped.RenderScale = viewport.RenderScale;
        cropped.ClearColor = null;
        cropped.Eye = CreateZCropEye(zEye, viewport.Size, viewport.RenderScale, crop);
        var localBox = UIBox2.FromDimensions(Vector2.Zero, cropped.Size);
        var sourceBox = new UIBox2(crop.Left, crop.Top, crop.Right, crop.Bottom);

        var modulation = handle.Modulate;
        handle.Modulate = Color.White;
        try
        {
            // Seed the crop with the already composited deeper decks, so transparent
            // tiles, z-blur and aperture overlays retain the same background as the full pass.
            handle.RenderInRenderTarget(cropped.RenderTarget, () =>
            {
                handle.UseShader(null);
                handle.SetTransform(Matrix3x2.Identity);
                handle.DrawTextureRectRegion(viewport.RenderTarget.Texture, localBox, sourceBox);
            }, Color.Black);

            _zActiveCropViewport = cropped;
            cropped.Render();
            _zActiveCropViewport = null;

            handle.RenderInRenderTarget(viewport.RenderTarget, () =>
            {
                handle.UseShader(null);
                handle.SetTransform(Matrix3x2.Identity);
                handle.DrawTextureRect(cropped.RenderTarget.Texture, sourceBox);
            }, null);
        }
        finally
        {
            handle.Modulate = modulation;
            _zActiveCropViewport = null;
            ReleaseZCropViewports();
        }
        return true;
    }

    internal static ZEye CreateZCropEye(ZEye eye, Vector2i fullSize, Vector2 renderScale, UIBox2i crop)
    {
        var center = new Vector2(crop.Left + crop.Width / 2f, crop.Top + crop.Height / 2f);
        var worldCenter = Vector2.Transform(center, GetZScreenToWorld(eye, fullSize, renderScale));
        return new ZEye(eye.LowestDepth, eye.Depth, eye.HighestDepth)
        {
            // Keep Position intact for world/parallax overlays; the projection shift is an offset.
            Position = eye.Position,
            Offset = worldCenter - eye.Position.Position,
            Rotation = eye.Rotation,
            Scale = eye.Scale,
            DrawFov = eye.DrawFov,
            DrawLight = eye.DrawLight,
        };
    }

    private void ReleaseZCropViewports()
    {
        foreach (var entry in _zCropViewports)
        {
            entry.InUse = false;
            entry.Viewport.Eye = null;
        }
    }

    private void DisposeZCropViewports()
    {
        foreach (var entry in _zCropViewports)
            entry.Viewport.Dispose();
        _zCropViewports.Clear();
        _zActiveCropViewport = null;
    }

    private IClydeViewport RentZCropViewport(Vector2i size)
    {
        ZCropViewport? oldest = null;
        foreach (var entry in _zCropViewports)
        {
            if (entry.InUse)
                continue;
            if (entry.Viewport.Size == size)
            {
                entry.InUse = true;
                entry.LastUsed = ++_zCropUseCounter;
                return entry.Viewport;
            }
            if (oldest == null || entry.LastUsed < oldest.LastUsed)
                oldest = entry;
        }

        // Exact size reuse keeps the rendered area equal to the planned crop.
        if (_zCropViewports.Count >= MaxPooledCropViewports && oldest != null)
        {
            oldest.Viewport.Dispose();
            _zCropViewports.Remove(oldest);
        }
        var viewport = _clyde.CreateViewport(size, new TextureSampleParameters { Filter = false });
        viewport.AutomaticRender = false;
        _zCropViewports.Add(new ZCropViewport(viewport) { InUse = true, LastUsed = ++_zCropUseCounter });
        return viewport;
    }
}
