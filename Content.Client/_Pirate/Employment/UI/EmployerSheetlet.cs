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
        var font10 = sheet.BaseFont.GetFont(10);
        var font14Bold = sheet.BaseFont.GetFont(14, FontKind.Bold);

        return
        [
            E<Label>().Class("ProfileEmployerLabel").Font(font14Bold).FontColor(Color.FromHex("#E0E0E0")),
            E<Label>().Class("ProfileEmployerHint").Font(font10).FontColor(Color.FromHex("#A0A0A0")),
        ];
    }
}
