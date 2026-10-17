using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Fonts;
using Content.Client._Pirate.Profile.UI;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Pirate.Jobs.UI;

[CommonSheetlet]
public sealed class JobsSheetlet : Sheetlet<PalettedStylesheet>
{
    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var jobNameFont = sheet.BaseFont.GetFont(12);
        var unavailableFont = sheet.BaseFont.GetFont(12, FontKind.Bold);

        return
        [
            E<Label>().Class("ProfileJobNameLabel").Font(jobNameFont).FontColor(ProfileEditorStyles.TextPrimary),
            E<Label>().Class("ProfileUnavailableLabel").Font(unavailableFont).FontColor(ProfileEditorStyles.TextPrimary),
        ];
    }
}
