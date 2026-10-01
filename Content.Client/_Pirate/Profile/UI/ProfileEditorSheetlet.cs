using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Fonts;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Pirate.Profile.UI;

/// <summary>
/// Shared visual rules for the profile editor custom category
/// </summary>
[CommonSheetlet]
public sealed class ProfileEditorSheetlet : Sheetlet<PalettedStylesheet>
{
    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var font10 = sheet.BaseFont.GetFont(10);
        var font11 = sheet.BaseFont.GetFont(11);
        var font12 = sheet.BaseFont.GetFont(12);

        return
        [
            E<PanelContainer>().Class("ProfileCategoryHeader").Panel(ProfileEditorStyles.CreateCategoryHeader()),
            E<Button>().Class("ProfileCategoryHeaderButton").Box(new StyleBoxFlat { BackgroundColor = Color.Transparent }),
            E<Label>().Class("ProfileCategoryExpandIcon").Font(font10).FontColor(Color.FromHex("#C9D4E5")),
            E<Label>().Class("ProfileCategoryNameLabel").Font(font12).FontColor(Color.FromHex("#F0F2F5")),
            E<PanelContainer>().Class("ProfileDepartmentAccent").Panel(new StyleBoxFlat(Color.FromHex("#4ADE80"))),
            E<PanelContainer>().Class("ProfileCategoryContent").Panel(ProfileEditorStyles.CreateCategoryContent()),
            E<PanelContainer>().Class("ProfileEntryPanel").Panel(ProfileEditorStyles.CreateEntryPanel()),
            E<Label>().Class("ProfileEntryNameLabel").Font(font11).FontColor(ProfileEditorStyles.TextPrimary),
        ];
    }
}
