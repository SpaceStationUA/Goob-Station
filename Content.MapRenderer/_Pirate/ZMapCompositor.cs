using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.MapRenderer.Painters;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Content.MapRenderer._Pirate;

/// <summary>
/// Rendered grids for one z-level.
/// </summary>
public sealed class ZLayerRender
{
    public required int Depth;
    public required IReadOnlyList<RenderedGridImage<Rgba32>> Grids;
}

/// <summary>
/// Per-level z-map composites.
/// </summary>
public sealed class ZMapComposite : IDisposable
{
    public required List<(int Depth, Image<Rgba32> Image)> Layers;

    public void Dispose()
    {
        foreach (var (_, image) in Layers)
            image.Dispose();
    }
}

/// <summary>
/// Builds per-level stacked images using the in-game z offset and depth dimming.
/// </summary>
public static class ZMapCompositor
{
    private const float PixelsPerMeter = TilePainter.TileImageSize;

    /// <summary>
    /// Brightness multiplier per level below the viewed depth.
    /// </summary>
    private const float DepthDimFactor = 0.6f;

    public static ZMapComposite Compose(IReadOnlyList<ZLayerRender> layers)
    {
        if (layers.SelectMany(l => l.Grids).Any() == false)
            throw new InvalidOperationException("No grids were rendered for any z-level.");

        var ordered = layers.OrderBy(l => l.Depth).ToList();
        var images = new List<(int Depth, Image<Rgba32> Image)>();

        // Viewed level is the anchor; lower levels are shifted and dimmed beneath it.
        foreach (var viewed in ordered)
        {
            var stack = ordered.Where(l => l.Depth <= viewed.Depth).ToList();

            var bounds = GetBounds(stack, viewed.Depth);
            var width = Math.Max(1, (int) MathF.Ceiling((bounds.MaxRight - bounds.MinLeft) * PixelsPerMeter));
            var height = Math.Max(1, (int) MathF.Ceiling((bounds.MaxTop - bounds.MinBottom) * PixelsPerMeter));

            var canvas = new Image<Rgba32>(width, height);

            // Deepest first so the viewed level ends up on top.
            foreach (var layer in stack)
            {
                var brightness = MathF.Pow(DepthDimFactor, viewed.Depth - layer.Depth);
                DrawLayer(canvas, layer, viewed.Depth, bounds.MinLeft, bounds.MaxTop, brightness);
            }

            images.Add((viewed.Depth, canvas));
        }

        return new ZMapComposite { Layers = images };
    }

    private static void DrawLayer(
        Image<Rgba32> canvas,
        ZLayerRender layer,
        int referenceLevel,
        float minLeft,
        float maxTop,
        float brightness)
    {
        foreach (var grid in layer.Grids)
        {
            var bottomLeft = GetShiftedBottomLeft(grid, layer.Depth, referenceLevel);
            var pxLeft = (int) MathF.Round((bottomLeft.X - minLeft) * PixelsPerMeter);
            var worldTop = bottomLeft.Y + grid.Image.Height / PixelsPerMeter;
            var pxTop = (int) MathF.Round((maxTop - worldTop) * PixelsPerMeter);

            if (brightness >= 1f)
            {
                canvas.Mutate(o => o.DrawImage(grid.Image, new Point(pxLeft, pxTop), 1f));
            }
            else
            {
                using var dimmed = grid.Image.Clone(o => o.Brightness(brightness));
                canvas.Mutate(o => o.DrawImage(dimmed, new Point(pxLeft, pxTop), 1f));
            }
        }
    }

    private static (float MinLeft, float MinBottom, float MaxRight, float MaxTop) GetBounds(
        IReadOnlyList<ZLayerRender> stack,
        int referenceLevel)
    {
        var grids = stack
            .SelectMany(l => l.Grids.Select(g => (Grid: g, Bl: GetShiftedBottomLeft(g, l.Depth, referenceLevel))))
            .ToList();

        var minLeft = grids.Min(e => e.Bl.X);
        var minBottom = grids.Min(e => e.Bl.Y);
        var maxRight = grids.Max(e => e.Bl.X + e.Grid.Image.Width / PixelsPerMeter);
        var maxTop = grids.Max(e => e.Bl.Y + e.Grid.Image.Height / PixelsPerMeter);

        return (minLeft, minBottom, maxRight, maxTop);
    }

    private static Vector2 GetShiftedBottomLeft(RenderedGridImage<Rgba32> grid, int depth, int referenceLevel)
    {
        var visualDepth = depth - referenceLevel;
        return grid.WorldBottomLeft + new Vector2(0f, CESharedZLevelsSystem.ZLevelVisualOffset * visualDepth);
    }
}
