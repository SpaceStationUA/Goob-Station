// SPDX-FileCopyrightText: 2026 Pirate
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared._Pirate.CCVars;

public sealed partial class PirateVars
{
    /// <summary>
    /// Client-side gain applied to announcement and alert-level sounds.
    /// </summary>
    public static readonly CVarDef<float> AnnouncementVolume =
        CVarDef.Create("pirate.announcement_volume", 1f, CVar.ARCHIVE | CVar.CLIENTONLY);

    /// <summary>
    /// Whether alert-level changes use the longer recordings that speak the code information.
    /// </summary>
    public static readonly CVarDef<bool> TranscribedAnnouncementSounds =
        CVarDef.Create("pirate.transcribed_announcement_sounds", true, CVar.ARCHIVE | CVar.CLIENTONLY);
}
