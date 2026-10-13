// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Pirate.Lobby.UI.Background;

public enum FlatButtonKind : byte
{
    Default,

    Primary,

    Danger,
}

public sealed class FlatButton : Button
{
    private FlatButtonKind _kind;

    public FlatButtonKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            DrawModeChanged();
        }
    }

    public FlatButton()
    {
        Label.Margin = new Thickness(6, 0);
        DrawModeChanged();
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();

        // The base constructor already switches draw mode, before the label exists.
        if ((object?) Label == null)
            return;

        var hover = DrawMode is DrawModeEnum.Hover or DrawModeEnum.Pressed;
        var (background, border, text) = _kind switch
        {
            FlatButtonKind.Primary => (hover ? Color.FromHex("#2e4257") : BackgroundStyle.AccentFill,
                BackgroundStyle.Accent, Color.FromHex("#dcebfa")),
            FlatButtonKind.Danger => (hover ? Color.FromHex("#47271f") : Color.FromHex("#3a221f"),
                Color.FromHex("#7a3630"), Color.FromHex("#f0a89f")),
            _ => (hover ? BackgroundStyle.RowHover : Color.FromHex("#262b36"),
                hover ? Color.FromHex("#495164") : BackgroundStyle.Line, BackgroundStyle.Text),
        };

        StyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border,
            BorderThickness = new Thickness(1),
            ContentMarginTopOverride = 2,
            ContentMarginBottomOverride = 2,
        };
        Label.FontColorOverride = text;
        Modulate = DrawMode == DrawModeEnum.Disabled ? Color.White.WithAlpha(0.4f) : Color.White;
    }
}
