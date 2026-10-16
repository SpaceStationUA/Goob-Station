// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Client.Parallax.Managers;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.Parallax;

/// <summary>
///     The sector arrival card: a translucent band across the screen with the sector name in caps
///     over a hairline, and the sector's description running as a marquee underneath.
/// </summary>
/// <remarks>
///     Everything is sized as a fraction of the viewport height, and the fonts follow the same
///     fractions, so the card keeps one shape at any window size.
/// </remarks>
public sealed class WarpArrivalOverlay : Overlay
{
    [Dependency] private readonly IResourceCache _resources = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private Font _font = default!;
    private Font _fontSmall = default!;

    // Layout on a 1080p reference, as fractions of the viewport height: the band spans 211..282 px,
    // the title's baseline is at 244 px, the 2 px hairline starts at 252 px and the marquee's baseline
    // is at 284 px.
    private static readonly float BandTopFrac = 211f / 1080f;
    private static readonly float BandBottomFrac = 282f / 1080f;
    private static readonly float TitleBaselineFrac = 244f / 1080f;
    private static readonly float RuleTopFrac = 252f / 1080f;
    private static readonly float RuleHeightFrac = 2f / 1080f;
    private static readonly float MarqueeBaselineFrac = 284f / 1080f;

    private static readonly float TitleFontFrac = 32f / 1080f;
    private static readonly float DescriptionFontFrac = 18f / 1080f;

    private static readonly Color BandColor = new(6, 8, 18);
    private static readonly Color RuleColor = new(205, 214, 228);
    private static readonly Color TitleColor = new(235, 240, 248, 255);
    private static readonly Color MarqueeColor = new(158, 168, 182, 235);

    private const float TickerSpeedFrac = 53f / 1080f;

    private const float FadeIn = 0.25f;
    private const float FadeOut = 0.35f;

    private string _title = string.Empty;
    private string _description = string.Empty;
    private double _shownAt = double.MinValue;
    private double _duration;
    private float _fontHeight = -1f;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public WarpArrivalOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    /// <summary>
    ///     Shows the card for a sector, replacing whatever is on screen.
    /// </summary>
    public void Show(string title, string description, float duration)
    {
        _title = title;
        _description = description;
        _duration = duration;
        _shownAt = _timing.CurTime.TotalSeconds;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _timing.CurTime.TotalSeconds - _shownAt < _duration && args.Viewport.Eye != null;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var w = (float) args.Viewport.Size.X;
        var h = (float) args.Viewport.Size.Y;
        var handle = args.ScreenHandle;

        if (MathF.Abs(_fontHeight - h) > 1f)
            LoadFonts(h);

        var elapsed = (float) (_timing.CurTime.TotalSeconds - _shownAt);
        var fade = MathF.Min(elapsed / FadeIn, 1f) * MathF.Min(1f, ((float) _duration - elapsed) / FadeOut);

        var bandTop = h * BandTopFrac;
        handle.DrawRect(new UIBox2(0f, bandTop, w, h * BandBottomFrac), BandColor.WithAlpha(0.5f * fade));

        var titleText = _title.ToUpperInvariant();
        var titleWidth = Measure(_font, titleText);
        var cx = w / 2f;

        // Both texts are placed by their baselines, so the gaps never drift with a font rebuild.
        var ruleTop = h * RuleTopFrac;
        var ruleBottom = ruleTop + MathF.Max(1f, h * RuleHeightFrac);

        // One hairline across the whole screen, drawn under both texts.
        handle.DrawRect(new UIBox2(0f, ruleTop, w, ruleBottom), RuleColor.WithAlpha(0.9f * fade));

        var titleBaseline = h * TitleBaselineFrac;
        handle.DrawString(_font, new Vector2(cx - titleWidth / 2f, titleBaseline - _font.GetAscent(1f)),
            titleText, TitleColor.WithAlpha(0.85f + 0.15f * fade));

        // The description runs right to left, repeating after a separator.
        var marqueeBaseline = h * MarqueeBaselineFrac;
        var marqueePos = new Vector2(0f, marqueeBaseline - _fontSmall.GetAscent(1f));
        var text = _description + "   ///   ";
        var textWidth = Measure(_fontSmall, text);

        // Nothing to repeat means the loop below would never advance.
        if (textWidth <= 0f)
            return;

        var scroll = (float) ((_timing.CurTime.TotalSeconds - _shownAt) * TickerSpeedFrac * w) % textWidth;
        for (var x = -scroll; x < w; x += textWidth)
        {
            handle.DrawString(_fontSmall, new Vector2(x, marqueePos.Y), text, MarqueeColor.WithAlpha(0.95f * fade));
        }
    }

    private void LoadFonts(float viewportHeight)
    {
        var titlePx = Math.Max(12, (int) MathF.Round(viewportHeight * TitleFontFrac));
        var descPx = Math.Max(10, (int) MathF.Round(viewportHeight * DescriptionFontFrac));

        _font = new VectorFont(_resources.GetResource<FontResource>("/Fonts/NotoSansDisplay/NotoSansDisplay-Bold.ttf"), titlePx);
        _fontSmall = new VectorFont(_resources.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Italic.ttf"), descPx);
        _fontHeight = viewportHeight;
    }

    private static float Measure(Font font, string text)
    {
        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            if (font.TryGetCharMetrics(rune, 1f, out var metrics))
                width += metrics.Advance;
        }
        return width;
    }
}
