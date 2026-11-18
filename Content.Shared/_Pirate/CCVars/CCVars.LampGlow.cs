// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> PirateLampGlow =
        CVarDef.Create("light.lamp_glow", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> PirateLampGlowCone =
        CVarDef.Create("light.lamp_glow_cone", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> PirateLampGlowIntensity =
        CVarDef.Create("light.lamp_glow_intensity", 0.2f, CVar.CLIENTONLY | CVar.ARCHIVE);
}
