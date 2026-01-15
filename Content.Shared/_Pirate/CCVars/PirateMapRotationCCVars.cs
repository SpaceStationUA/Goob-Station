using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<int> PirateMapRotationDontRepeatCount =
                CVarDef.Create("map_rotation.dont_repeat_count", 2, CVar.SERVER | CVar.ARCHIVE);
}
