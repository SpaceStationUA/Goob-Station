using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// How long after the warning the spare ID safe waits before granting command access.
    /// </summary>
    public static readonly CVarDef<TimeSpan> SpareIdUnlockDelay =
        CVarDef.Create("game.spare_id.unlock_delay", TimeSpan.FromMinutes(5), CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// How long after round start the station waits before checking for a captain.
    /// </summary>
    public static readonly CVarDef<TimeSpan> SpareIdAlertDelay =
        CVarDef.Create("game.spare_id.alert_delay", TimeSpan.FromMinutes(15), CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether the spare ID safe automatically grants command access when there is no captain.
    /// </summary>
    public static readonly CVarDef<bool> SpareIdAutoUnlock =
        CVarDef.Create("game.spare_id.auto_unlock", true, CVar.SERVERONLY | CVar.ARCHIVE);
}
