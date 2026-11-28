// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Eui;
using Content.Client.UserInterface.Controls;
using Content.Shared._Pirate.Revolutionary;
using Content.Shared.Eui;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Pirate.Revolutionary;

public sealed class RevolutionaryRecruitmentEui : BaseEui
{
    private readonly FancyWindow _window = new() { Title = Loc.GetString("pirate-rev-invite-title"), MinWidth = 360 };
    private bool _answered;

    public RevolutionaryRecruitmentEui()
    {
        var body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(12),
            SeparationOverride = 10,
        };
        body.AddChild(new RichTextLabel { Text = Loc.GetString("pirate-rev-invite-prompt"), MaxWidth = 340 });
        var buttons = new BoxContainer { SeparationOverride = 8 };
        var accept = new Button { Text = Loc.GetString("pirate-rev-invite-accept"), HorizontalExpand = true };
        var decline = new Button { Text = Loc.GetString("pirate-rev-invite-decline"), HorizontalExpand = true };
        buttons.AddChild(accept);
        buttons.AddChild(decline);
        body.AddChild(buttons);
        _window.XamlChildren.Add(body);
        accept.OnPressed += _ => Answer(new AcceptRevolutionaryRecruitmentMessage());
        decline.OnPressed += _ => _window.Close();
        _window.OnClose += () => Answer(new CloseEuiMessage());
    }

    private void Answer(EuiMessageBase message)
    {
        if (_answered)
            return;

        _answered = true;
        SendMessage(message);
    }

    public override void Opened() => _window.OpenCentered();

    public override void Closed()
    {
        _answered = true;
        _window.Close();
    }
}
