using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    #region Lying Down System

    /// <summary>
    ///     When true, players can choose to crawl under tables while laying down, using the designated keybind.
    /// </summary>
    public static readonly CVarDef<bool> CrawlUnderTables =
            CVarDef.Create("rest.crawlundertables", true, CVar.SERVER | CVar.ARCHIVE);

    #endregion
}
