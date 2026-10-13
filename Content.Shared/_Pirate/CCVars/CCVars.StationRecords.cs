using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    // Printed paper has no reader context; empty uses the server zone.
    public static readonly CVarDef<string> RecordsPrintTimeZone =
        CVarDef.Create("records.print_timezone", "Europe/Kyiv", CVar.SERVERONLY | CVar.ARCHIVE);
    public static readonly CVarDef<bool> RecordsHistoryPersistence =
        CVarDef.Create("records.history_persistence", true, CVar.SERVERONLY | CVar.ARCHIVE);
    public static readonly CVarDef<int> RecordsHistoryLimit =
        CVarDef.Create("records.history_limit", 100, CVar.SERVERONLY | CVar.ARCHIVE);
}
