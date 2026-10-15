// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Pirate.Lobby.UI.Background;

public sealed class BackgroundTextField : PanelContainer
{
    public readonly TextEdit Input;

    private readonly Label _counter;
    private readonly int _maxLength;
    private int _shownLength = -1;

    public BackgroundTextField(string name, string hint, Color accent, int maxLength)
    {
        _maxLength = maxLength;
        HorizontalExpand = true;
        PanelOverride = BackgroundStyle.Card(accent);

        var body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            Margin = new Thickness(10, 8),
        };

        var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        header.AddChild(new Label
        {
            Text = name,
            StyleClasses = { "TraitsEntryNameLabel" },
            HorizontalExpand = true,
        });
        _counter = new Label { StyleClasses = { "TraitsCategoryStatsLabel" }, VerticalAlignment = VAlignment.Center };
        header.AddChild(_counter);
        body.AddChild(header);

        var hintLabel = new RichTextLabel { StyleClasses = { "TraitsEntryDescriptionLabel" }, HorizontalExpand = true };
        hintLabel.SetMessage(FormattedMessage.FromUnformatted(hint));
        body.AddChild(hintLabel);

        Input = new TextEdit { HorizontalExpand = true, MinHeight = 110 };
        var frame = new PanelContainer { PanelOverride = BackgroundStyle.InputFrame(), Margin = new Thickness(0, 2, 0, 0) };
        frame.AddChild(Input);
        body.AddChild(frame);

        AddChild(body);
        UpdateCounter();
    }

    public bool IsFilled => Input.TextLength > 0;

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        // Code-set text doesn't raise OnTextChanged.
        UpdateCounter();
        BackgroundStyle.FitScrollBar(Input);
    }

    private void UpdateCounter()
    {
        var length = Input.TextLength;
        if (length == _shownLength)
            return;

        _shownLength = length;
        _counter.Text = $"{length}/{_maxLength}";
        _counter.FontColorOverride = length > _maxLength ? BackgroundStyle.Bad : null;
    }
}
