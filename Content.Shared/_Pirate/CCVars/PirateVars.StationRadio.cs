// SPDX-FileCopyrightText: 2026 Pirate
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared._Pirate.CCVars;

public sealed partial class PirateVars
{
    /// <summary>
    /// Client-side gain applied to audio emitted by station radio receivers.
    /// </summary>
    public static readonly CVarDef<float> StationRadioReceiverVolume =
        CVarDef.Create("pirate.station_radio_receiver_volume", 0.5f, CVar.ARCHIVE | CVar.CLIENTONLY);

    /// <summary>
    /// Server: merge unfiltered internet-radio stations discovered through
    /// radio-browser.info into the PDA radio picker. Off by default — the
    /// remote catalog is community-submitted and includes anything (e.g.
    /// political talk streams), so a curated pinned list is the default.
    /// </summary>
    public static readonly CVarDef<bool> RadioRemoteCatalog =
        CVarDef.Create("pirate.radio_remote_catalog", false, CVar.SERVERONLY);

    /// <summary>
    ///     Server: path to an ffmpeg binary. When set, radio stations that the
    ///     client's CEF cannot decode (MP3/AAC) are transcoded server-side and
    ///     relayed through the game connection as WebM/Opus. Empty disables
    ///     the feature. Audio-only transcodes cost a few percent of one core
    ///     per station with measurable CPU headroom on typical hosts.
    /// </summary>
    public static readonly CVarDef<string> RadioFfmpegPath =
        CVarDef.Create("pirate.radio_ffmpeg_path", "", CVar.SERVERONLY);
}
