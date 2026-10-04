// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared._Pirate.CCVars;

[CVarDefs]
public static class MentorHelpCVars
{
    public static readonly CVarDef<string> DiscordWebhook =
        CVarDef.Create("pirate.discord_mentorhelp_webhook", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<string> DiscordOnCallWebhook =
        CVarDef.Create("pirate.discord_mentorhelp_on_call_webhook", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<string> DiscordOnCallPing =
        CVarDef.Create("pirate.discord_mentorhelp_on_call_ping", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);
}
