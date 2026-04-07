using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<int> GhostRespawnDelay =
        CVarDef.Create("ghost.respawn_delay", 600, CVar.SERVER | CVar.ARCHIVE);
}
