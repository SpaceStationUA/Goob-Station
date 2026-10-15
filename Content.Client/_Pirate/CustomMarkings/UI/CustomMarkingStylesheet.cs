// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Robust.Client.UserInterface.StylesheetHelpers;

namespace Content.Client._Pirate.CustomMarkings.UI;

// Pirate: scope Wolfgate's creator cards to this module using our stylesheet and fonts.
[CommonSheetlet]
public sealed class CustomMarkingStylesheet : Sheetlet<PalettedStylesheet>
{
    public const string StyleClassCreatorCard = "CreatorCard";
    public const string StyleClassCreatorHeading = "CreatorHeading";
    public const string StyleClassCreatorFieldLabel = "CreatorFieldLabel";
    public const string StyleClassCreatorPrimary = "CreatorPrimary";
    public const string StyleClassCreatorToggle = "CreatorToggle";
    public const string StyleClassCreatorBackdrop = "CreatorBackdrop";
    public const string StyleClassCreatorGroup = "CreatorGroup";
    public const string StyleClassCreatorCardTitle = "CreatorCardTitle";
    public const string StyleClassCreatorIconButton = "CreatorIconButton";
    public const string StyleClassCreatorIcon = "CreatorIcon";
    public const string StyleClassCreatorIconDisabled = "CreatorIconDisabled";

    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var accent = Color.FromHex("#46D7FF");
        var text = Color.FromHex("#E6EDF3");
        var muted = Color.FromHex("#8A9BAB");
        var edge = Color.FromHex("#23303E");
        var card = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#131B24").WithAlpha(0.85f),
            BorderColor = edge,
            BorderThickness = new Thickness(1),
        };
        card.SetContentMarginOverride(StyleBox.Margin.All, 12);
        var group = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#1B2733").WithAlpha(0.35f),
            BorderColor = edge,
            BorderThickness = new Thickness(1),
        };
        group.SetContentMarginOverride(StyleBox.Margin.All, 10);
        var icon = new StyleBoxFlat(Color.White);
        icon.SetContentMarginOverride(StyleBox.Margin.All, 6);
        var tile = new StyleBoxFlat(Color.White);
        tile.SetContentMarginOverride(StyleBox.Margin.All, 4);

        return
        [
            Element<PanelContainer>().Class(StyleClassCreatorBackdrop)
                .Prop(PanelContainer.StylePropertyPanel, new StyleBoxFlat(Color.FromHex("#0A0E13").WithAlpha(0.94f))),
            Element<PanelContainer>().Class(StyleClassCreatorCard).Prop(PanelContainer.StylePropertyPanel, card),
            Element<PanelContainer>().Class(StyleClassCreatorGroup).Prop(PanelContainer.StylePropertyPanel, group),
            Element<Label>().Class(StyleClassCreatorHeading)
                .Prop(Label.StylePropertyFont, sheet.BaseFont.GetFont(16)).Prop(Label.StylePropertyFontColor, accent),
            Element<Label>().Class(StyleClassCreatorCardTitle)
                .Prop(Label.StylePropertyFont, sheet.BaseFont.GetFont(13)).Prop(Label.StylePropertyFontColor, accent),
            Element<Label>().Class(StyleClassCreatorFieldLabel).Prop(Label.StylePropertyFontColor, muted),
            Element<Button>().Class(StyleClassCreatorPrimary).Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(Control.StylePropertyModulateSelf, Color.FromHex("#157A56")),
            Element<Button>().Class(StyleClassCreatorPrimary).Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(Control.StylePropertyModulateSelf, Color.FromHex("#1E9E6E")),
            Element<Button>().Class(StyleClassCreatorPrimary).Pseudo(ContainerButton.StylePseudoClassDisabled)
                .Prop(Control.StylePropertyModulateSelf, Color.FromHex("#0C4632")),
            Element<ContainerButton>().Class(StyleClassCreatorToggle).Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(Control.StylePropertyModulateSelf, Color.FromHex("#2A8FB0")),
            Element<ContainerButton>().Class(ContainerButton.StyleClassButton).Class(StyleClassCreatorIconButton)
                .Prop(ContainerButton.StylePropertyStyleBox, icon),
            Element<TextureRect>().Class(StyleClassCreatorIcon).Prop(Control.StylePropertyModulateSelf, text),
            Element<TextureRect>().Class(StyleClassCreatorIconDisabled).Prop(Control.StylePropertyModulateSelf, muted.WithAlpha(0.5f)),
            Element<ContainerButton>().Class(CustomMarkingTile.StyleClassTile).Prop(ContainerButton.StylePropertyStyleBox, tile),
            Element<ContainerButton>().Class(CustomMarkingTile.StyleClassTile).Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(Control.StylePropertyModulateSelf, edge),
            Element<ContainerButton>().Class(CustomMarkingTile.StyleClassTile).Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(Control.StylePropertyModulateSelf, Color.FromHex("#425A72")),
            Element<ContainerButton>().Class(CustomMarkingTile.StyleClassTile).Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(Control.StylePropertyModulateSelf, Color.FromHex("#2A8FB0")),
            Element<ContainerButton>().Class(CustomMarkingTile.StyleClassTile).Pseudo(ContainerButton.StylePseudoClassDisabled)
                .Prop(Control.StylePropertyModulateSelf, Color.FromHex("#0A0E13")),
            Element<ContainerButton>().Class(CustomMarkingColorPicker.StyleClassSwatch)
                .Prop(ContainerButton.StylePropertyStyleBox, new StyleBoxFlat(Color.White)),
        ];
    }
}
