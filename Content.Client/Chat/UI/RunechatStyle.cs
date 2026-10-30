using System.Runtime.CompilerServices;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Chat.UI;

public static class RunechatStyle
{
    public const string FontPath = "/Fonts/_ES/Grand9KPixelRegular.ttf";
    public const string WhisperFontPath = "/Fonts/_ES/Wormtown9k-Small.ttf";
    public const int MinFontSize = 6;
    public const int MaxFontSize = 24;
    private const float DesignPixelsPerEm = 8f;

    // Pirate: parallel test clients own separate resources and must not share fonts or stylesheets.
    private static readonly ConditionalWeakTable<IResourceCache, ClientCache> Caches = new();

    private sealed class ClientCache
    {
        public readonly Dictionary<(string, int), Font> Fonts = new();
        public readonly Dictionary<(string, int), Stylesheet> Sheets = new();
    }

    public static Font GetFont(string path, int size)
    {
        var cache = IoCManager.Resolve<IResourceCache>();
        var fonts = Caches.GetValue(cache, static _ => new ClientCache()).Fonts;
        if (fonts.TryGetValue((path, size), out var font))
            return font;

        font = new VectorFont(cache.GetResource<FontResource>(path), size);
        // Pirate: preserve Ukrainian glyphs in the configurable runechat fonts.
        var fallback = path switch
        {
            FontPath => "/Fonts/_Pirate/PixeloidSans.ttf",
            WhisperFontPath => "/Fonts/_Pirate/Tiny5-Regular.ttf",
            _ => null,
        };
        if (fallback != null)
            font = new StackedFont(font, new VectorFont(cache.GetResource<FontResource>(fallback), size));
        fonts[(path, size)] = font;
        return font;
    }

    public static Stylesheet GetStylesheet(string path, int size)
    {
        var cache = IoCManager.Resolve<IResourceCache>();
        var sheets = Caches.GetValue(cache, static _ => new ClientCache()).Sheets;
        if (sheets.TryGetValue((path, size), out var sheet))
            return sheet;

        sheet = new Stylesheet(new[]
        {
            new StyleRule(
                new SelectorElement(typeof(RichTextLabel), null, null, null),
                new[] { new StyleProperty(Label.StylePropertyFont, GetFont(path, size)) }),
        });
        sheets[(path, size)] = sheet;
        return sheet;
    }

    public static int ScaleSize(int baseSize, int fontSize)
    {
        return Math.Max(1, (int) MathF.Round(baseSize * fontSize / 12f));
    }

    public static int GetLoudSize(int fontSize)
    {
        return ScaleSize(18, fontSize);
    }

    public static float GetOutline(int pointSize, float scale)
    {
        return pointSize * 96f / 72f / DesignPixelsPerEm * scale;
    }
}
