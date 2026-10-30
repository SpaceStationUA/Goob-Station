using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Chat.UI;

public sealed class RunechatFontCache
{
    [Dependency] private IResourceCache _resources = default!;

    private readonly Dictionary<(string, int), Font> _fonts = new();
    private readonly Dictionary<(string, int), Stylesheet> _sheets = new();

    public Font GetFont(string path, int size)
    {
        if (_fonts.TryGetValue((path, size), out var font))
            return font;

        font = new VectorFont(_resources.GetResource<FontResource>(path), size);
        // Pirate: preserve Ukrainian glyphs in the configurable runechat fonts.
        var fallback = path switch
        {
            RunechatStyle.FontPath => "/Fonts/_Pirate/PixeloidSans.ttf",
            RunechatStyle.WhisperFontPath => "/Fonts/_Pirate/Tiny5-Regular.ttf",
            _ => null,
        };
        if (fallback != null)
            font = new StackedFont(font, new VectorFont(_resources.GetResource<FontResource>(fallback), size));
        _fonts[(path, size)] = font;
        return font;
    }

    public Stylesheet GetStylesheet(string path, int size)
    {
        if (_sheets.TryGetValue((path, size), out var sheet))
            return sheet;

        sheet = new Stylesheet(new[]
        {
            new StyleRule(
                new SelectorElement(typeof(RichTextLabel), null, null, null),
                new[] { new StyleProperty(Label.StylePropertyFont, GetFont(path, size)) }),
        });
        _sheets[(path, size)] = sheet;
        return sheet;
    }
}
