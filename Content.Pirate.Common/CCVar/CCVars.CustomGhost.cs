using Robust.Shared.Configuration;

namespace Content.Pirate.Common.CCVar;

public sealed partial class PirateCVars
{
    #region Custom ghosts

    /// <summary>Maximum custom ghost sprite side in pixels; 0 disables scaling.</summary>
    public static readonly CVarDef<int> CustomGhostMaxSize =
        CVarDef.Create("pirate.custom_ghost_max_size", 32, CVar.SERVER | CVar.REPLICATED);

    #endregion
}
