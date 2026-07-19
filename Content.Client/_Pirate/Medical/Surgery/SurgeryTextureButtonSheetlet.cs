// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Stylesheets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Pirate.Medical.Surgery;

[CommonSheetlet]
public sealed class SurgeryTextureButtonSheetlet<T> : Sheetlet<T> where T : PalettedStylesheet
{
    private static readonly Color DollColor = Color.FromHex("#639bff");

    public override StyleRule[] GetRules(T sheet, object config)
    {
        return
        [
            E<TextureButton>()
                .Identifier("SurgeryTextureButton")
                .PseudoHovered()
                .Modulate(sheet.PositivePalette.HoveredElement),
            E<TextureButton>()
                .Identifier("SurgeryTextureButton")
                .PseudoPressed()
                .Modulate(sheet.PositivePalette.PressedElement),
            E<TextureButton>()
                .Identifier("SurgeryTextureButton")
                .PseudoNormal()
                .Modulate(DollColor),

            E<TextureButton>()
                .Identifier("OpenIncision")
                .PseudoHovered()
                .Modulate(sheet.PositivePalette.HoveredElement),
            E<TextureButton>()
                .Identifier("OpenIncision")
                .PseudoPressed()
                .Modulate(sheet.PositivePalette.PressedElement),
            E<TextureButton>()
                .Identifier("OpenIncision")
                .PseudoNormal()
                .Modulate(sheet.HighlightPalette.HoveredElement),
        ];
    }
}
