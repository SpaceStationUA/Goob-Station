// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client._Pirate.ZLevels.Core;
using Content.Shared._Pirate.ZLevels.Apertures.Components;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;
using Robust.Shared.Map.Components;

namespace Content.Client.Viewport;

public sealed partial class ScalingViewport
{
    private readonly ZVisibilityMask _zVisibilityMask = new();
    private readonly Dictionary<int, List<UIBox2>> _zScreenRegions = new();
    private readonly HashSet<int> _zRegionalDepths = new();
    private readonly List<UIBox2> _zRegionApertures = new();
    private readonly Dictionary<int, ZCropPlanner> _zCropPlanners = new();
    private readonly List<ZCropViewport> _zCropViewports = new();
    private readonly IClydeViewport?[] _zRentedCrops = new IClydeViewport?[ZCropPlanner.MaxCrops];
    private readonly int[] _zCropRenderOrder = new int[ZCropPlanner.MaxCrops];
    private long _zCropUseCounter;
    private IClydeViewport? _zActiveCropViewport;
    private Vector2i _zCropFullSize;

    internal ZRegionFrameStats ZRegionStats { get; private set; }
    internal readonly record struct ZRegionFrameStats(int CroppedLayers, int CropPasses, int FullLayers,
        int HiddenLayers, double LowerTargetArea, int Allocations, bool WholeStackSkipped,
        int FusedBlurPasses, int BlurFusionFallbacks, int CropSizeTransitions, string? BlurFusionBlocker);

    private sealed class ZCropViewport(IClydeViewport viewport)
    {
        public readonly IClydeViewport Viewport = viewport;
        public long LastUsed;
        public bool InUse;
    }

    /// <summary>Profiling: last frame's cull/crop decisions, for pairing with zprof screenshots.</summary>
    internal string DescribeZRegions()
    {
        var s = ZRegionStats;
        var text = $"skip={s.WholeStackSkipped} crop_passes={s.CropPasses} full={s.FullLayers} " +
                   $"hidden={s.HiddenLayers} area={s.LowerTargetArea.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)} " +
                   $"fused_blur={s.FusedBlurPasses} blur_fallbacks={s.BlurFusionFallbacks} " +
                   $"crop_size_transitions={s.CropSizeTransitions} blur_blocker={s.BlurFusionBlocker ?? "none"}";
        foreach (var depth in _zRegionalDepths)
        {
            text += $" d{depth}:regions={_zScreenRegions[depth].Count}";
            if (!_zCropPlanners.TryGetValue(depth, out var planner))
                continue;

            foreach (var crop in planner.Crops)
            {
                var r = crop.RenderBounds;
                text += $" [{r.Left},{r.Top} {r.Width}x{r.Height}]";
            }
        }

        if (s.BlurFusionFallbacks > 0)
        {
            foreach (var overlay in _overlayManager.AllOverlays)
            {
                if (overlay is CEZLevelBlurOverlay blur)
                    text += $" all_blur_blockers=[{blur.DescribeFusionBlockers()}]";
            }
        }
        return text;
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
            ZRegionStats = ZRegionStats with { HiddenLayers = ZRegionStats.HiddenLayers + 1 };
            return true;
        }

        var pixelsPerMeter = eye.Scale * viewport.RenderScale * EyeManager.PixelsPerMeter;
        var padding = MathF.Max(pixelsPerMeter.X, pixelsPerMeter.Y) * 2 + 8;
        if (!_zCropPlanners.TryGetValue(depth, out var planner))
            _zCropPlanners[depth] = planner = new ZCropPlanner();
        if (!planner.TryPlan(regions, viewport.Size, padding, _cfg.GetCVar(CCVars.ZRegionMaxCrops)))
            return false;

        if (viewport.ClearColor != null)
            handle.RenderInRenderTarget(viewport.RenderTarget, () => { }, viewport.ClearColor);

        var modulation = handle.Modulate;
        handle.Modulate = Color.White;
        try
        {
            // Seed ALL crops before compositing any of them. Overlapping crops must see the
            // same deeper-deck background, otherwise blur/tint gets applied twice at overlaps.
            for (var i = 0; i < planner.Crops.Count; i++)
            {
                var crop = planner.Crops[i].RenderBounds;
                var cropped = RentZCropViewport(crop.Size);
                _zRentedCrops[i] = cropped;
                cropped.RenderScale = viewport.RenderScale;
                cropped.ClearColor = null;
                var cropEye = CreateZCropEye(zEye, viewport.Size, viewport.RenderScale, crop);
                cropEye.AllowBlurFusion = _cfg.GetCVar(CCVars.ZFuseCropBlur);
                cropped.Eye = cropEye;
                var localBox = UIBox2.FromDimensions(Vector2.Zero, cropped.Size);
                var sourceBox = new UIBox2(crop.Left, crop.Top, crop.Right, crop.Bottom);
                handle.RenderInRenderTarget(cropped.RenderTarget, () =>
                {
                    handle.UseShader(null);
                    handle.SetTransform(Matrix3x2.Identity);
                    handle.DrawTextureRectRegion(viewport.RenderTarget.Texture, localBox, sourceBox);
                }, Color.Black);
            }

            // Rendering order may change; seed order and compositing order must not.
            // No composite copy intervenes between equal-size renders, so overlays that
            // request SCREEN_TEXTURE can reuse the engine's shared screen-buffer size.
            // Reuse a managed array: localloc/stackalloc IL is rejected by the hub sandbox.
            ZCropRenderOrder.Fill(planner.Crops, _zCropRenderOrder, _cfg.GetCVar(CCVars.ZGroupCropSizes));
            Vector2i? previousSize = null;
            for (var pass = 0; pass < planner.Crops.Count; pass++)
            {
                var i = _zCropRenderOrder[pass];
                var cropped = _zRentedCrops[i]!;
                if (previousSize != null && previousSize != cropped.Size)
                    ZRegionStats = ZRegionStats with { CropSizeTransitions = ZRegionStats.CropSizeTransitions + 1 };
                previousSize = cropped.Size;
                _zActiveCropViewport = cropped;
                cropped.Render();
                _zActiveCropViewport = null;
                var cropEye = (ZEye) cropped.Eye!;
                ZRegionStats = ZRegionStats with
                {
                    CropPasses = ZRegionStats.CropPasses + 1,
                    LowerTargetArea = ZRegionStats.LowerTargetArea +
                        (double) cropped.Size.X * cropped.Size.Y / ((double) viewport.Size.X * viewport.Size.Y),
                    FusedBlurPasses = ZRegionStats.FusedBlurPasses + (cropEye.DeferredBlurShader != null ? 1 : 0),
                    BlurFusionFallbacks = ZRegionStats.BlurFusionFallbacks + (cropEye.BlurFusionBlocker != null ? 1 : 0),
                    BlurFusionBlocker = cropEye.BlurFusionBlocker ?? ZRegionStats.BlurFusionBlocker,
                };
            }

            for (var i = 0; i < planner.Crops.Count; i++)
            {
                var cropped = _zRentedCrops[i]!;
                var plan = planner.Crops[i];
                var cropEye = (ZEye) cropped.Eye!;
                var shader = cropEye.DeferredBlurShader;
                if (shader != null)
                {
                    shader.SetParameter("SOURCE_PIXEL_SIZE", new Vector2(1f / cropped.Size.X, 1f / cropped.Size.Y));
                    shader.SetParameter("BLUR_COLOR", cropEye.DeferredBlurColor);
                }
                // Copy the visible cluster, excluding its blur guard border. A crop's clamped
                // texture edge must not overwrite another opening inside a neighboring crop.
                var localBox = new UIBox2(plan.OutputBounds.Left - plan.RenderBounds.Left,
                    plan.OutputBounds.Top - plan.RenderBounds.Top,
                    plan.OutputBounds.Right - plan.RenderBounds.Left,
                    plan.OutputBounds.Bottom - plan.RenderBounds.Top);
                handle.RenderInRenderTarget(viewport.RenderTarget, () =>
                {
                    handle.UseShader(shader);
                    handle.SetTransform(Matrix3x2.Identity);
                    handle.DrawTextureRectRegion(cropped.RenderTarget.Texture, plan.OutputBounds, localBox);
                    handle.UseShader(null);
                }, null);
            }
            ZRegionStats = ZRegionStats with { CroppedLayers = ZRegionStats.CroppedLayers + 1 };
        }
        finally
        {
            handle.UseShader(null);
            handle.Modulate = modulation;
            _zActiveCropViewport = null;
            foreach (var entry in _zCropViewports)
            {
                entry.InUse = false;
                entry.Viewport.Eye = null;
            }
            Array.Clear(_zRentedCrops);
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

    private void DisposeZCropViewports()
    {
        foreach (var entry in _zCropViewports)
            entry.Viewport.Dispose();
        _zCropViewports.Clear();
        _zCropPlanners.Clear();
        _zActiveCropViewport = null;
        Array.Clear(_zRentedCrops);
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

        // Eight entries total, shared across all decks; at most four are leased together.
        // Exact size reuse keeps actual rendered area within the planner's budget.
        if (_zCropViewports.Count >= ZCropPlanner.MaxCrops * 2 && oldest != null)
        {
            oldest.Viewport.Dispose();
            _zCropViewports.Remove(oldest);
        }
        var viewport = _clyde.CreateViewport(size, new TextureSampleParameters { Filter = false });
        viewport.AutomaticRender = false;
        _zCropViewports.Add(new ZCropViewport(viewport) { InUse = true, LastUsed = ++_zCropUseCounter });
        ZRegionStats = ZRegionStats with { Allocations = ZRegionStats.Allocations + 1 };
        return viewport;
    }
}
