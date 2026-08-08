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
}
