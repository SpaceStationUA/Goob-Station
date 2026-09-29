// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Pirate.Common.CCVar;

public sealed partial class PirateCVars
{
    public static readonly CVarDef<bool> VoiceChatAdmins =
        CVarDef.Create("voicechat.admins", true, CVar.SERVERONLY | CVar.ARCHIVE, "Allow active administrators to use voice chat when voice.enabled is true.");

    public static readonly CVarDef<bool> VoiceChatWhitelisted =
        CVarDef.Create("voicechat.whitelisted", false, CVar.SERVERONLY | CVar.ARCHIVE, "Allow players whitelisted for the Captain job to use voice chat when voice.enabled is true.");

    public static readonly CVarDef<bool> VoiceChatForAll =
        CVarDef.Create("voicechat.forall", false, CVar.SERVERONLY | CVar.ARCHIVE, "Allow every player to use voice chat when voice.enabled is true.");
}
