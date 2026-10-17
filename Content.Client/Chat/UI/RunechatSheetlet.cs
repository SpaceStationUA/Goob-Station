using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.Chat.UI;

[CommonSheetlet]
public sealed class RunechatSheetlet : Sheetlet<NanotrasenStylesheet>
{
    public const string FontPath = "/Fonts/_ES/Grand9KPixelRegular.ttf";
    public const string WhisperFontPath = "/Fonts/_ES/Wormtown9k-Small.ttf";
    public const string FallbackFontPath = "/Fonts/_Pirate/PixeloidSans.ttf"; // Pirate: runechat cyrillic
    public const string WhisperFallbackFontPath = "/Fonts/_Pirate/Tiny5-Regular.ttf"; // Pirate: runechat cyrillic

    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        var font = ResCache.GetResource<FontResource>(FontPath);
        var whisperFont = ResCache.GetResource<FontResource>(WhisperFontPath);
        // Pirate: runechat cyrillic - Grand9K and Wormtown lack Ukrainian glyphs.
        var fallback = ResCache.GetResource<FontResource>(FallbackFontPath); // Pirate: runechat cyrillic
        var whisperFallback = ResCache.GetResource<FontResource>(WhisperFallbackFontPath); // Pirate: runechat cyrillic

        return
        [
            E<RichTextLabel>()
                .Class(RunechatSpeechBubble.StyleClassNormal)
                .Prop(Label.StylePropertyFont, new StackedFont(new VectorFont(font, RunechatSpeechBubble.NormalSize), new VectorFont(fallback, RunechatSpeechBubble.NormalSize))), // Pirate: runechat cyrillic
            E<RichTextLabel>()
                .Class(RunechatSpeechBubble.StyleClassLoud)
                .Prop(Label.StylePropertyFont, new StackedFont(new VectorFont(font, RunechatSpeechBubble.LoudSize), new VectorFont(fallback, RunechatSpeechBubble.LoudSize))), // Pirate: runechat cyrillic
            E<RichTextLabel>()
                .Class(RunechatSpeechBubble.StyleClassWhisper)
                .Prop(Label.StylePropertyFont, new StackedFont(new VectorFont(whisperFont, RunechatSpeechBubble.NormalSize), new VectorFont(whisperFallback, RunechatSpeechBubble.NormalSize))), // Pirate: runechat cyrillic
        ];
    }
}
