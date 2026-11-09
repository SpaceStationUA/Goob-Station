// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    // A new CVar avoids inheriting the old option’s saved value.
    public static readonly CVarDef<bool> PirateSpriteAmbientOcclusion =
        CVarDef.Create("light.sprite_ambient_occlusion", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
