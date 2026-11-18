// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> PirateBloom =
        CVarDef.Create("light.bloom", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> PirateBloomIntensity =
        CVarDef.Create("light.bloom_intensity", 0.3f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> PirateBloomThreshold =
        CVarDef.Create("light.bloom_threshold", 0.65f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> PirateBloomSpread =
        CVarDef.Create("light.bloom_spread", 0.85f, CVar.CLIENTONLY | CVar.ARCHIVE);
}
