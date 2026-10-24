using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Pirate.Stylesheets;

[CommonSheetlet]
public sealed class IdrisLogoSheetlet : Sheetlet<NanotrasenStylesheet>
{
    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        return
        [
            E<TextureRect>()
                .Class("IdrisLogoDark")
                .Prop(TextureRect.StylePropertyTexture, sheet.GetTextureOr(
                    new ResPath("_Pirate/Interface/Nano/idrislogo.svg.png"),
                    new ResPath("/Textures")))
                .Prop(Control.StylePropertyModulateSelf, Color.FromHex("#757575")),
        ];
    }
}
