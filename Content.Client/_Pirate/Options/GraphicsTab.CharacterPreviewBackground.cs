using Content.Shared._Pirate.CCVars;

namespace Content.Client.Options.UI.Tabs;

public sealed partial class GraphicsTab
{
    private void InitializeCharacterPreviewBackgroundOption()
    {
        Control.AddOptionDropDown(
            PirateVars.CharacterPreviewBackground,
            DropDownCharacterPreviewBackground,
            [
                new OptionDropDownCVar<string>.ValueOption("default", Loc.GetString("ui-options-character-preview-background-default")),
                new OptionDropDownCVar<string>.ValueOption("dark-gray", Loc.GetString("ui-options-character-preview-background-dark-gray")),
                new OptionDropDownCVar<string>.ValueOption("gray", Loc.GetString("ui-options-character-preview-background-gray")),
                new OptionDropDownCVar<string>.ValueOption("light", Loc.GetString("ui-options-character-preview-background-light")),
                new OptionDropDownCVar<string>.ValueOption("checkerboard", Loc.GetString("ui-options-character-preview-background-checkerboard")),
            ]);
    }
}
