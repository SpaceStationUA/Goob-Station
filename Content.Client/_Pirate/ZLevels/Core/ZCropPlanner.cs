// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Client._Pirate.ZLevels.Core;

/// <summary>Bounded greedy clustering of visible rectangles into stock viewport crops.</summary>
internal sealed class ZCropPlanner
{
    public const int MaxCrops = 4;
    private const int MaxInputs = ZVisibilityMask.MaxRegions;
    // These are conservative planning heuristics, not measured GPU timing coefficients.
    private const float PassPenalty = 0.05f;
    private const float Budget = 0.75f;
    private readonly Cluster[] _clusters = new Cluster[MaxInputs];
    private readonly float[] _mergeCosts = new float[MaxInputs * MaxInputs];
    private readonly List<Crop> _crops = new(MaxCrops);
    public IReadOnlyList<Crop> Crops => _crops;
    private readonly List<UIBox2> _lastRegions = new(MaxInputs);
    private Vector2i _lastSize;
    private float _lastPadding;
    private int _lastCap;
    private bool _lastResult;
    private bool _hasLastPlan;

    internal readonly record struct Crop(UIBox2i RenderBounds, UIBox2 OutputBounds);
    private record struct Cluster(bool Active, UIBox2 Bounds, UIBox2i Crop, float Cost);

    public bool TryPlan(IReadOnlyList<UIBox2> regions, Vector2i fullSize, float padding, int maxCrops)
    {
        maxCrops = Math.Clamp(maxCrops, 1, MaxCrops);
        var same = _hasLastPlan && _lastSize == fullSize && _lastPadding == padding &&
                   _lastCap == maxCrops && _lastRegions.Count == regions.Count;
        for (var i = 0; same && i < regions.Count; i++)
            same = _lastRegions[i].Equals(regions[i]);
        if (same)
            return _lastResult;

        _hasLastPlan = false;
        var result = BuildPlan(regions, fullSize, padding, maxCrops);
        if (regions.Count > MaxInputs)
            return result;

        // Reuse only identical inputs. Visibility itself is still evaluated every frame,
        // so tile edits, moving apertures, zoom/resize and cap changes take effect immediately.
        _lastRegions.Clear();
        for (var i = 0; i < regions.Count; i++)
            _lastRegions.Add(regions[i]);
        _lastSize = fullSize;
        _lastPadding = padding;
        _lastCap = maxCrops;
        _lastResult = result;
        _hasLastPlan = true;
        return result;
    }

    private bool BuildPlan(IReadOnlyList<UIBox2> regions, Vector2i fullSize, float padding, int maxCrops)
    {
        _crops.Clear();
        if (regions.Count == 0 || regions.Count > MaxInputs)
            return false;

        maxCrops = Math.Clamp(maxCrops, 1, MaxCrops);
        var hasSingle = ZViewportCrop.TryGetCrop(regions, fullSize, padding, out var single);
        if (maxCrops == 1)
        {
            if (hasSingle)
                _crops.Add(new Crop(single, ToBox(single)));
            return hasSingle;
        }

        var fullArea = (double) fullSize.X * fullSize.Y;
        var count = regions.Count;
        var totalCost = 0f;
        for (var i = 0; i < regions.Count; i++)
        {
            if (!ZViewportCrop.TryGetCrop(regions[i], fullSize, padding, out var crop))
                return false;
            var cost = Cost(crop, fullArea);
            _clusters[i] = new Cluster(true, regions[i], crop, cost);
            totalCost += cost;
        }

        for (var i = 0; i < regions.Count; i++)
        for (var j = i + 1; j < regions.Count; j++)
            UpdateMergeCost(i, j, fullSize, padding, fullArea);

        while (count > 1)
        {
            var best = float.PositiveInfinity;
            var left = -1;
            var right = -1;
            for (var i = 0; i < regions.Count; i++)
            {
                if (!_clusters[i].Active)
                    continue;
                for (var j = i + 1; j < regions.Count; j++)
                {
                    if (!_clusters[j].Active || _mergeCosts[i * MaxInputs + j] >= best)
                        continue;
                    best = _mergeCosts[i * MaxInputs + j];
                    left = i;
                    right = j;
                }
            }

            if (left < 0 || (count <= maxCrops && best >= 0))
                break;

            var bounds = Union(_clusters[left].Bounds, _clusters[right].Bounds);
            if (!ZViewportCrop.TryGetCrop(bounds, fullSize, padding, out var merged))
                return false;
            _clusters[left] = new Cluster(true, bounds, merged, Cost(merged, fullArea));
            _clusters[right].Active = false;
            count--;
            totalCost += best;
            for (var i = 0; i < regions.Count; i++)
            {
                if (i != left && _clusters[i].Active)
                    UpdateMergeCost(Math.Min(i, left), Math.Max(i, left), fullSize, padding, fullArea);
            }
        }

        if (hasSingle && (count > maxCrops || Cost(single, fullArea) <= totalCost))
        {
            _crops.Add(new Crop(single, ToBox(single)));
            return true;
        }
        if (count > maxCrops || totalCost > Budget)
            return false;

        for (var i = 0; i < regions.Count; i++)
        {
            if (_clusters[i] is { Active: true } cluster)
                _crops.Add(new Crop(cluster.Crop, cluster.Bounds));
        }
        return true;
    }

    private void UpdateMergeCost(int i, int j, Vector2i size, float padding, double area)
    {
        var bounds = Union(_clusters[i].Bounds, _clusters[j].Bounds);
        _mergeCosts[i * MaxInputs + j] = ZViewportCrop.TryGetCrop(bounds, size, padding, out var crop)
            ? Cost(crop, area) - _clusters[i].Cost - _clusters[j].Cost
            : float.PositiveInfinity;
    }

    private static float Cost(UIBox2i crop, double fullArea) =>
        (float) ((double) crop.Width * crop.Height / fullArea) + PassPenalty;

    private static UIBox2 Union(UIBox2 a, UIBox2 b) => new(MathF.Min(a.Left, b.Left),
        MathF.Min(a.Top, b.Top), MathF.Max(a.Right, b.Right), MathF.Max(a.Bottom, b.Bottom));

    private static UIBox2 ToBox(UIBox2i crop) => new(crop.Left, crop.Top, crop.Right, crop.Bottom);
}
