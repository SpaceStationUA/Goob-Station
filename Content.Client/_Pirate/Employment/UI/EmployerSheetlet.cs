using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Fonts;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Pirate.Employment.UI;

[CommonSheetlet]
public sealed class EmployerSheetlet : Sheetlet<PalettedStylesheet>
{
    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var employerLabelFont = sheet.BaseFont.GetFont(12, FontKind.Bold);

        return
        [
            E<Label>().Class("ProfileEmployerLabel").Font(employerLabelFont).FontColor(Color.FromHex("#E0E0E0")),
        ];
    }
}
