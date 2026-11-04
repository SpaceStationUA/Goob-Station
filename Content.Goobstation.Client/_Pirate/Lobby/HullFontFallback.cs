using System.Linq;
using System.Numerics;
using System.Text;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Maths;

namespace Content.Goobstation.Client._Pirate.Lobby;

internal static class HullFontFallback
{
    public static Font Create(IResourceCache cache, Font primary, Font fallback, bool bold, bool small)
    {
        var path = bold ? "/Fonts/_Pirate/PixeloidSans-Bold.ttf" : "/Fonts/_Pirate/PixeloidSans.ttf";
        var cyrillic = cache.GetFont(path, small ? 6 : 12);

        // Match Pixeloid's 9-pixel grid to Freezeframe's 8-pixel grid.
        return new FallbackFont(primary, new ScaledFont(cyrillic, 9f / 8f), fallback);
    }

    // StackedFont uses only the primary font's line measurements. Cyrillic needs
    // more space for accents and descenders, especially in clipped input fields.
    private sealed class FallbackFont(params Font[] fonts) : Font
    {
        private readonly StackedFont _font = new(fonts);

        public override int GetAscent(float scale) => fonts.Max(font => font.GetAscent(scale));
        public override int GetDescent(float scale) => fonts.Max(font => font.GetDescent(scale));
        public override int GetHeight(float scale) => GetAscent(scale) + GetDescent(scale);

        public override int GetLineHeight(float scale)
        {
            return Math.Max(GetHeight(scale), fonts.Max(font => font.GetLineHeight(scale)));
        }

        public override float DrawChar(
            DrawingHandleBase handle,
            Rune rune,
            Vector2 baseline,
            float scale,
            Color color,
            bool fallback = true)
        {
            return _font.DrawChar(handle, rune, baseline, scale, color, fallback);
        }

        public override float DrawCharOutline(
            DrawingHandleBase handle,
            Rune rune,
            Vector2 baseline,
            float scale,
            TextOutline outline,
            bool fallback = true)
        {
            return _font.DrawCharOutline(handle, rune, baseline, scale, outline, fallback);
        }

        public override CharMetrics? GetCharMetrics(Rune rune, float scale, bool fallback = true)
        {
            return _font.GetCharMetrics(rune, scale, fallback);
        }
    }

    private sealed class ScaledFont(Font font, float factor) : Font
    {
        // Hinting can place the breve in Й above the font's reported ascent.
        public override int GetAscent(float scale) => font.GetAscent(scale * factor) + (int) Math.Ceiling(scale);
        public override int GetHeight(float scale) => GetAscent(scale) + GetDescent(scale);
        public override int GetDescent(float scale) => font.GetDescent(scale * factor);
        public override int GetLineHeight(float scale) => font.GetLineHeight(scale * factor);

        public override float DrawChar(
            DrawingHandleBase handle,
            Rune rune,
            Vector2 baseline,
            float scale,
            Color color,
            bool fallback = true)
        {
            return font.DrawChar(handle, rune, baseline, scale * factor, color, fallback);
        }

        public override float DrawCharOutline(
            DrawingHandleBase handle,
            Rune rune,
            Vector2 baseline,
            float scale,
            TextOutline outline,
            bool fallback = true)
        {
            return font.DrawCharOutline(handle, rune, baseline, scale * factor, outline, fallback);
        }

        public override CharMetrics? GetCharMetrics(Rune rune, float scale, bool fallback = true)
        {
            return font.GetCharMetrics(rune, scale * factor, fallback);
        }
    }
}
