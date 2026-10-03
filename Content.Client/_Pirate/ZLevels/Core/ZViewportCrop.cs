// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;

namespace Content.Client._Pirate.ZLevels.Core;

/// <summary>Conservative crop geometry for rendering through the stock viewport API.</summary>
internal static class ZViewportCrop
{
    public static bool TryGetCrop(IReadOnlyList<UIBox2> regions, Vector2i fullSize, float padding,
        out UIBox2i crop)
    {
        crop = default;
        if (regions.Count == 0 || fullSize.X <= 0 || fullSize.Y <= 0 ||
            !float.IsFinite(padding) || padding < 0)
            return false;

        var min = new Vector2(float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity);
        foreach (var region in regions)
        {
            if (!float.IsFinite(region.Left) || !float.IsFinite(region.Top) ||
                !float.IsFinite(region.Right) || !float.IsFinite(region.Bottom) ||
                region.Right < region.Left || region.Bottom < region.Top)
                return false;
            min = Vector2.Min(min, new Vector2(region.Left, region.Top));
            max = Vector2.Max(max, new Vector2(region.Right, region.Bottom));
        }

        return TryGetCrop(new UIBox2(min, max), fullSize, padding, out crop);
    }

    public static bool TryGetCrop(UIBox2 bounds, Vector2i fullSize, float padding, out UIBox2i crop)
    {
        crop = default;
        if (fullSize.X <= 0 || fullSize.Y <= 0 || !float.IsFinite(padding) || padding < 0 ||
            !float.IsFinite(bounds.Left) || !float.IsFinite(bounds.Top) ||
            !float.IsFinite(bounds.Right) || !float.IsFinite(bounds.Bottom) ||
            bounds.Right < bounds.Left || bounds.Bottom < bounds.Top)
            return false;

        var min = new Vector2(bounds.Left, bounds.Top);
        var max = new Vector2(bounds.Right, bounds.Bottom);
        min = Vector2.Clamp(min - new Vector2(padding), Vector2.Zero, fullSize);
        max = Vector2.Clamp(max + new Vector2(padding), Vector2.Zero, fullSize);
        min = new Vector2(MathF.Floor(min.X), MathF.Floor(min.Y));
        max = new Vector2(MathF.Ceiling(max.X), MathF.Ceiling(max.Y));
        // Retain the full view's aspect ratio: the stock lighting blur uses viewport height
        // to scale both axes. Eight size buckets limit allocation churn as the camera moves.
        var fraction = MathF.Max((max.X - min.X) / fullSize.X, (max.Y - min.Y) / fullSize.Y);
        fraction = MathF.Ceiling(fraction * 8) / 8;
        if (fraction <= 0 || fraction > 0.75f)
            return false; // The two texture copies are unlikely to pay for a nearly full view.

        var size = new Vector2i((int) MathF.Ceiling(fullSize.X * fraction),
            (int) MathF.Ceiling(fullSize.Y * fraction));
        crop = CenterWithin((min + max) / 2, size, fullSize);
        return true;
    }

    public static UIBox2i CenterWithin(Vector2 center, Vector2i size, Vector2i fullSize)
    {
        var left = Math.Clamp((int) MathF.Floor(center.X - size.X / 2f), 0, fullSize.X - size.X);
        var top = Math.Clamp((int) MathF.Floor(center.Y - size.Y / 2f), 0, fullSize.Y - size.Y);
        return UIBox2i.FromDimensions(new Vector2i(left, top), size);
    }
}
