// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Pirate.CartridgeLoader.Cartridges;

/// <summary>Styling for the D.E.T.O.M.A.T.I.X. interface.</summary>
public static class DetomatixPalette
{
    public static readonly Color Panel = Color.FromHex("#211a1c");
    public static readonly Color Head = Color.FromHex("#2a1f22");
    public static readonly Color RowAlt = Color.FromHex("#241c1e");
    public static readonly Color RowHover = Color.FromHex("#33262a");
    public static readonly Color Line = Color.FromHex("#4a2f36");
    public static readonly Color LineSoft = Color.FromHex("#38272c");
    public static readonly Color Track = Color.FromHex("#2c2125");

    public static readonly Color Text = Color.FromHex("#ece2e4");
    public static readonly Color TextDim = Color.FromHex("#ae969b");
    public static readonly Color TextFaint = Color.FromHex("#806a6f");

    public static readonly Color Accent = Color.FromHex("#c3384c");
    public static readonly Color AccentFill = Color.FromHex("#3d1c24");

    public static readonly Color Bad = Color.FromHex("#df6152");
    public static readonly Color Zero = Color.FromHex("#8a7a7e");

    public static readonly Color DangerBorder = Color.FromHex("#7a3630");
    public static readonly Color DangerBg = Color.FromHex("#3a221f");
    public static readonly Color DangerBgHover = Color.FromHex("#47271f");
    public static readonly Color DangerText = Color.FromHex("#f0a89f");

    public static readonly Color ConfirmBorder = Color.FromHex("#b04a3c");
    public static readonly Color ConfirmBg = Color.FromHex("#5a2620");
    public static readonly Color ConfirmBgHover = Color.FromHex("#6b2d25");
    public static readonly Color ConfirmText = Color.FromHex("#ffd0c6");

    public static StyleBoxFlat Fill(Color background)
    {
        return new StyleBoxFlat { BackgroundColor = background };
    }

    public static StyleBoxFlat Band(Color background, Color border, Thickness thickness, float padding = 0f)
    {
        var box = new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border,
            BorderThickness = thickness,
        };

        if (padding <= 0f)
            return box;

        box.ContentMarginLeftOverride = padding;
        box.ContentMarginRightOverride = padding;
        box.ContentMarginTopOverride = padding;
        box.ContentMarginBottomOverride = padding;
        return box;
    }

    public static StyleBoxFlat Button(Color background, Color border)
    {
        var box = Band(background, border, new Thickness(1));
        box.ContentMarginLeftOverride = 12;
        box.ContentMarginRightOverride = 12;
        box.ContentMarginTopOverride = 5;
        box.ContentMarginBottomOverride = 5;
        return box;
    }

    public static Label Eyebrow(string text, Color? color = null)
    {
        return new Label
        {
            Text = text.ToUpperInvariant(),
            FontColorOverride = color ?? TextFaint,
            StyleClasses = { "LabelSubText" },
        };
    }

    public static void HoverSwap(Control trigger, PanelContainer panel, StyleBoxFlat normal, StyleBoxFlat hovered)
    {
        panel.PanelOverride = normal;
        trigger.OnMouseEntered += _ => panel.PanelOverride = hovered;
        trigger.OnMouseExited += _ => panel.PanelOverride = normal;
    }
}
