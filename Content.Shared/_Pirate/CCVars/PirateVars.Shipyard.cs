using Robust.Shared.Configuration;

namespace Content.Shared._Pirate.CCVars;

public sealed partial class PirateVars
{
    /// <summary>Whether shipyard consoles may spawn vessels.</summary>
    public static readonly CVarDef<bool> Shipyard =
        CVarDef.Create("shuttle.shipyard", true, CVar.SERVERONLY);
}
