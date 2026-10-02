// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared._Pirate.CCVars;

public sealed partial class PirateVars
{
    public static readonly CVarDef<bool> MentorHelpSoundEnabled =
        CVarDef.Create("pirate.mentorhelp_sound_enabled", true, CVar.ARCHIVE | CVar.CLIENTONLY);
}
