// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Pirate.Lobby.UI.Background;

public sealed class BackgroundWarning : PanelContainer
{
    private static readonly Color Warn = Color.FromHex("#e0823a");

    public BackgroundWarning()
    {
        HorizontalExpand = true;
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = Warn.WithAlpha(0.12f),
            BorderColor = Warn,
            BorderThickness = new Thickness(3, 1, 1, 1),
        };

        var text = new RichTextLabel { HorizontalExpand = true, Margin = new Thickness(10, 7) };
        var message = new FormattedMessage();
        message.PushColor(Color.FromHex("#f0b48a"));
        message.AddText(Loc.GetString("background-tab-warning"));
        message.Pop();
        text.SetMessage(message);
        AddChild(text);
    }
}
