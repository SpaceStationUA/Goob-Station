using Robust.Shared.Configuration;

namespace Content.Shared._Pirate.CCVars;

public sealed partial class PirateVars
{
    public static readonly CVarDef<string> CharacterPreviewBackground =
        CVarDef.Create("ui.character_preview_background", "default", CVar.CLIENT | CVar.ARCHIVE);
}
