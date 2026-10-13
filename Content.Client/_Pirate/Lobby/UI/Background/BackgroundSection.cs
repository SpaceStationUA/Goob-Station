// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Pirate.Lobby.UI.Background;

public sealed class BackgroundSection : BoxContainer
{
    public readonly BoxContainer Body;

    private readonly Label _arrow;
    private readonly Label _stats;
    private readonly PanelContainer _content;

    public int Filled { get; private set; }
    public int Total { get; private set; }

    public BackgroundSection(string title, Color accent)
    {
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;

        var header = new PanelContainer { StyleClasses = { "TraitsCategoryHeader" } };
        var toggle = new Button { StyleClasses = { "TraitsCategoryHeaderButton" }, HorizontalExpand = true };
        var row = new BoxContainer { Orientation = LayoutOrientation.Horizontal, Margin = new Thickness(8, 6) };
        _arrow = new Label { Text = "▼", StyleClasses = { "TraitsCategoryExpandIcon" }, Margin = new Thickness(0, 0, 8, 0) };
        row.AddChild(_arrow);
        row.AddChild(new Label { Text = title, StyleClasses = { "TraitsCategoryNameLabel" } });
        row.AddChild(new Control { HorizontalExpand = true });
        _stats = new Label { StyleClasses = { "TraitsCategoryStatsLabel" }, Margin = new Thickness(0, 0, 4, 0) };
        row.AddChild(_stats);
        toggle.AddChild(row);
        toggle.OnPressed += _ => SetExpanded(!_content!.Visible);
        header.AddChild(toggle);
        AddChild(header);

        AddChild(new PanelContainer { SetHeight = 3, PanelOverride = new StyleBoxFlat(accent) });

        _content = new PanelContainer { StyleClasses = { "TraitsCategoryContent" } };
        Body = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 8,
            Margin = new Thickness(10, 8),
        };
        _content.AddChild(Body);
        AddChild(_content);
    }

    public void SetExpanded(bool expanded)
    {
        _content.Visible = expanded;
        _arrow.Text = expanded ? "▼" : "▶";
    }

    public void SetProgress(int filled, int total)
    {
        Filled = filled;
        Total = total;
        SetStats(Loc.GetString("background-tab-section-filled", ("filled", filled), ("total", total)));
    }

    public void SetStats(string text)
    {
        _stats.Text = text;
    }
}
