// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Pirate.Lobby.UI.Background;

public static class BackgroundStyle
{
    public static readonly Color Panel = Color.FromHex("#20242e");
    public static readonly Color Row = Color.FromHex("#242834");
    public static readonly Color RowHover = Color.FromHex("#2a2f3b");
    public static readonly Color Head = Color.FromHex("#262b37");
    public static readonly Color Line = Color.FromHex("#343a47");
    public static readonly Color LineSoft = Color.FromHex("#2b313d");
    public static readonly Color Input = Color.FromHex("#1c2028");
    public static readonly Color Text = Color.FromHex("#e4e7ec");
    public static readonly Color TextDim = Color.FromHex("#98a0ac");
    public static readonly Color TextFaint = Color.FromHex("#6a7280");

    public static readonly Color Accent = Color.FromHex("#4f93d6");
    public static readonly Color AccentFill = Color.FromHex("#253649");
    public static readonly Color Good = Color.FromHex("#58bd7c");
    public static readonly Color Bad = Color.FromHex("#df6152");

    public static readonly Color Ic = Color.FromHex("#33b3c7");
    public static readonly Color Ooc = Color.FromHex("#b155c9");
    public static readonly Color Records = Color.FromHex("#dcb63c");

    public static StyleBoxFlat Card(Color accent)
    {
        return new StyleBoxFlat
        {
            BackgroundColor = Row,
            BorderColor = accent.WithAlpha(0.75f),
            BorderThickness = new Thickness(3, 0, 0, 0),
        };
    }

    public static StyleBoxFlat InputFrame()
    {
        return new StyleBoxFlat
        {
            BackgroundColor = Input,
            BorderColor = Line,
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 6,
            ContentMarginRightOverride = 6,
            ContentMarginTopOverride = 3,
            ContentMarginBottomOverride = 3,
        };
    }

    public static void FitScrollBar(TextEdit edit)
    {
        foreach (var child in edit.Children)
        {
            if (child is VScrollBar bar)
                bar.Visible = bar.MaxValue > bar.Page + 1f;
        }
    }

    public static LineEdit LineEdit()
    {
        return new LineEdit { StyleBoxOverride = InputFrame() };
    }
}
