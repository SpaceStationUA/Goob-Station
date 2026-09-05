// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Fonts;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Pirate.Knowledge.UI;

public static class KnowledgeStyleClasses
{
    public const string CategoryHeader = "KnowledgeCategoryHeader";
    public const string CategoryName = "KnowledgeCategoryName";
    public const string SkillRow = "KnowledgeSkillRow";
    public const string SkillName = "KnowledgeSkillName";
    public const string SkillDetails = "KnowledgeSkillDetails";
    public const string SkillExperience = "KnowledgeSkillExperience";
}

[CommonSheetlet]
public sealed class KnowledgeSheetlet : Sheetlet<PalettedStylesheet>
{
    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var categoryHeader = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#292C34"),
            BorderColor = Color.FromHex("#B59A65"),
            BorderThickness = new Thickness(3, 0, 0, 0),
        };
        categoryHeader.SetContentMarginOverride(StyleBox.Margin.Horizontal, 9);
        categoryHeader.SetContentMarginOverride(StyleBox.Margin.Vertical, 5);

        var skillRow = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#20232A"),
            BorderColor = Color.FromHex("#343944"),
            BorderThickness = new Thickness(1),
        };
        skillRow.SetContentMarginOverride(StyleBox.Margin.Horizontal, 9);
        skillRow.SetContentMarginOverride(StyleBox.Margin.Vertical, 7);

        return
        [
            E<PanelContainer>().Class(KnowledgeStyleClasses.CategoryHeader).Panel(categoryHeader),
            E<Label>()
                .Class(KnowledgeStyleClasses.CategoryName)
                .Font(sheet.BaseFont.GetFont(13, FontKind.Bold))
                .FontColor(Color.FromHex("#E3D3B2")),
            E<PanelContainer>().Class(KnowledgeStyleClasses.SkillRow).Panel(skillRow),
            E<Label>()
                .Class(KnowledgeStyleClasses.SkillName)
                .Font(sheet.BaseFont.GetFont(12, FontKind.Bold))
                .FontColor(Color.FromHex("#F1F2F4")),
            E<Label>()
                .Class(KnowledgeStyleClasses.SkillDetails)
                .Font(sheet.BaseFont.GetFont(10))
                .FontColor(Color.FromHex("#B4BAC4")),
            E<Label>()
                .Class(KnowledgeStyleClasses.SkillExperience)
                .Font(sheet.BaseFont.GetFont(10, FontKind.Bold))
                .FontColor(Color.FromHex("#9198A5")),
        ];
    }
}
