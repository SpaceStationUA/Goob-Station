// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._Pirate.CustomMarkings.UI;

/// <summary>
/// A window of this module. The skin's window panel is glass, which reads badly over the creator's text, so a solid
/// backdrop goes under it; the panel's own border and rounded corners stay on top.
/// </summary>
public abstract class CustomMarkingWindow : DefaultWindow
{
    protected CustomMarkingWindow()
    {
        var backdrop = new PanelContainer
        {
            StyleClasses = { CustomMarkingStylesheet.StyleClassCreatorBackdrop },
            Margin = new Thickness(3),
        };
        AddChild(backdrop);
        backdrop.SetPositionFirst();
    }
}
