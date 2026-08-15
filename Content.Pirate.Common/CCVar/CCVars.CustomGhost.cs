using Robust.Shared.Configuration;

namespace Content.Pirate.Common.CCVar;

public sealed partial class PirateCVars
{
    #region Custom ghosts

    /// <summary>Maximum side of drawn content, in pixels; 0 removes the limit entirely.</summary>
    public static readonly CVarDef<int> CustomGhostMaxSize =
        CVarDef.Create("pirate.custom_ghost_max_size", 28, CVar.SERVERONLY);

    #endregion
}
