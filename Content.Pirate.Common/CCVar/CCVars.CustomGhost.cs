using Robust.Shared.Configuration;

namespace Content.Pirate.Common.CCVar;

public sealed partial class PirateCVars
{
    #region Custom ghosts

    /// <summary>
    /// Maximum side of a custom ghost's sprite frame, in pixels; 0 removes the limit entirely.
    /// Replicated because the clamp is applied client side - only the client knows sprite sizes,
    /// since packaged servers ship no textures at all.
    /// </summary>
    public static readonly CVarDef<int> CustomGhostMaxSize =
        CVarDef.Create("pirate.custom_ghost_max_size", 32, CVar.SERVER | CVar.REPLICATED);

    #endregion
}
