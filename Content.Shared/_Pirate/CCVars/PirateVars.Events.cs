using Robust.Shared.Configuration;

namespace Content.Shared._Pirate.CCVars;

public sealed partial class PirateVars
{
    /// <summary>
    /// Event/game-rule start-delay override in seconds: -1 = use prototype delay (default), 0 = instant,
    /// >0 = force this delay. Server-only testing aid: cvar pirate.events.delay_override 0
    /// </summary>
    public static readonly CVarDef<float> EventsDelayOverride =
        CVarDef.Create("pirate.events.delay_override", -1f, CVar.SERVERONLY);
}
