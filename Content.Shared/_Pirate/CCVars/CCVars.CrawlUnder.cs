using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// When true, players can choose to crawl under tables while standing state is downed.
    /// </summary>
    public static readonly CVarDef<bool> CrawlUnderTables =
            CVarDef.Create("rest.crawlundertables", true, CVar.SERVER | CVar.ARCHIVE);
}
