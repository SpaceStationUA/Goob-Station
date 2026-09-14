using Robust.Shared.Configuration;

namespace Content.Shared._Pirate.CCVar;

[CVarDefs]
public sealed class PirateCVars
{
    public static readonly CVarDef<string> UkraineAlarmApiToken =
        CVarDef.Create("pirate.ukraine_alarm.api_token", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<int> UkraineAlarmUpdateInterval =
        CVarDef.Create("pirate.ukraine_alarm.update_interval", 20, CVar.SERVERONLY);

    public static readonly CVarDef<bool> UkraineAlarmNotifications =
        CVarDef.Create("pirate.ukraine_alarm.notifications", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
