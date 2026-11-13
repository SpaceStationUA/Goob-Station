// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<float> PirateDecalPreviewOpacity =
        CVarDef.Create("pirate.decal_preview_opacity", 0.75f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> PirateCrayonPreview =
        CVarDef.Create("pirate.crayon_preview", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
