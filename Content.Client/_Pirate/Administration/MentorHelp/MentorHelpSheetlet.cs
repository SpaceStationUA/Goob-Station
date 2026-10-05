// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Palette;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Pirate.Administration.MentorHelp;

[CommonSheetlet]
public sealed class MentorHelpSheetlet<T> : Sheetlet<T> where T : PalettedStylesheet
{
    public override StyleRule[] GetRules(T sheet, object config)
    {
        var panel = new StyleBoxFlat
        {
            BackgroundColor = Color.Transparent,
            BorderColor = sheet.SecondaryPalette.Element,
            BorderThickness = new Thickness(0, 2, 0, 0),
        };

        var header = new StyleBoxTexture
        {
            Texture = ResCache.GetTexture("/Textures/_Pirate/Interface/Windows/window_header_mentor.png"),
            PatchMarginBottom = 3,
            ExpandMarginBottom = 3,
            ContentMarginBottomOverride = 0,
        };

        var rules = new List<StyleRule>
        {
            // Must out-rank the base "windowHeader" rule, which every window header also carries.
            E<PanelContainer>()
                .Class(DefaultWindow.StyleClassWindowHeader, HelpTabs.StyleClassMentorWindowHeader)
                .Panel(header),

            E<PanelContainer>().Class(HelpTabs.StyleClassPanel).Panel(panel),

            E<PanelContainer>().Class(HelpTabs.StyleClassTab).Panel(TabBox(sheet.SecondaryPalette.Background)),
            E<PanelContainer>()
                .Class(HelpTabs.StyleClassTab, HelpTabs.StyleClassTabActive)
                .Panel(TabBox(sheet.SecondaryPalette.Element)),
            E<PanelContainer>()
                .Class(HelpTabs.StyleClassTab, HelpTabs.StyleClassTabUnreadAdmin)
                .Panel(TabBox(HelpTabs.AdminColor)),
            E<PanelContainer>()
                .Class(HelpTabs.StyleClassTab, HelpTabs.StyleClassTabUnreadMentor)
                .Panel(TabBox(HelpTabs.MentorColor)),
        };

        AddButtonTint(rules, MentorPalette, HelpTabs.StyleClassMentorUnread);
        AddButtonTint(rules, sheet.NegativePalette, StyleClass.Negative, HelpTabs.StyleClassMentorUnread);

        return rules.ToArray();
    }

    private static readonly ColorPalette MentorPalette =
        ColorPalette.FromHexBase(HelpTabs.MentorColor.ToHex(), element: HelpTabs.MentorColor);

    private static void AddButtonTint(List<StyleRule> rules, ColorPalette palette, params string[] classes)
    {
        rules.Add(E().Class(classes).PseudoNormal().Prop(Control.StylePropertyModulateSelf, palette.Element));
        rules.Add(E().Class(classes).PseudoHovered().Prop(Control.StylePropertyModulateSelf, palette.HoveredElement));
        rules.Add(E().Class(classes).PseudoPressed().Prop(Control.StylePropertyModulateSelf, palette.PressedElement));
        rules.Add(E().Class(classes).PseudoDisabled().Prop(Control.StylePropertyModulateSelf, palette.DisabledElement));
    }

    private static StyleBoxFlat TabBox(Color color)
    {
        var box = new StyleBoxFlat(color);
        box.SetContentMarginOverride(StyleBox.Margin.Horizontal, 5);
        return box;
    }
}
