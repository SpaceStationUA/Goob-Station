using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<float> GunLagCompRange =
        CVarDef.Create("gun.lag_comp_range", 0.6f, CVar.SERVER | CVar.ARCHIVE);
}
