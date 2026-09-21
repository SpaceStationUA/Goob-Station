// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using Content.Pirate.Shared.Radio;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Network;

namespace Content.Pirate.Client.Radio;

/// <summary>
///     Program-facing façade for the PID radio. State is keyed by the
///     installed program entity (its NetEntity) so two PDAs don't collide.
///     The server is authoritative for the selected station; the page owns
///     actual playback, so this mirror is advisory.
/// </summary>
public static class PirateRadioClientState
{
    public sealed class Entry
    {
        public string StationId = "";
        public bool Playing;

        /// <summary>The station is server-transcoded (relay chunks, MSE).</summary>
        public bool Relay;
    }

    private static readonly Dictionary<NetEntity, Entry> _entries = new();
    private static readonly Dictionary<NetEntity, List<PirateRadioStationEntry>> _catalogs = new();
    private static readonly Dictionary<NetEntity, string> _themes = new();
    private static readonly Dictionary<NetEntity, List<string>> _themeLists = new();
    private static readonly Dictionary<NetEntity, bool> _requested = new();

    public static Entry Get(NetEntity marker)
        => _entries.TryGetValue(marker, out var e) ? e : new Entry();

    public static IReadOnlyList<PirateRadioStationEntry>? Catalog(NetEntity marker)
        => _catalogs.TryGetValue(marker, out var c) ? c : null;

    /// <summary>Ask the server for the catalog (throttled to once per marker).</summary>
    public static void RequestCatalog(NetEntity marker)
    {
        if (_catalogs.ContainsKey(marker) || _requested.ContainsKey(marker))
            return;
        _requested[marker] = true;
        IoCManager.Resolve<IEntityNetworkManager>()
            .SendSystemNetworkMessage(new PirateRadioCatalogRequestEvent { Marker = marker });
    }

    public static void Send(string op, NetEntity marker, string stationId = "")
    {
        IoCManager.Resolve<IEntityNetworkManager>()
            .SendSystemNetworkMessage(new PirateRadioCommandEvent
            {
                Marker = marker,
                Op = op,
                StationId = stationId,
            });
    }

    /// <summary>"theme" op: swap the device skin (currently only NT or
    /// Syndicate flavors exist; PDA Settings gates which themes a device
    /// can reach).</summary>
    public static void SendTheme(NetEntity marker, string themeId)
    {
        IoCManager.Resolve<IEntityNetworkManager>()
            .SendSystemNetworkMessage(new PirateRadioCommandEvent
            {
                Marker = marker,
                Op = "theme",
                Theme = themeId,
            });
    }

    public static void OnCatalog(PirateRadioCatalogEvent msg)
    {
        _catalogs[msg.Marker] = msg.Stations;
        _themes[msg.Marker] = string.IsNullOrEmpty(msg.Theme) ? "PirateNtWeb" : msg.Theme;
        _themeLists[msg.Marker] = msg.Themes;
        _themeChanged = true;
    }

    private static bool _themeChanged;

    /// <summary>Frontier theme id for this playback (PirateNtWeb default).</summary>
    public static string Theme(NetEntity marker)
        => _themes.TryGetValue(marker, out var t) ? t : "PirateNtWeb";

    /// <summary>Switchable theme ids for this playback (server-gated).</summary>
    public static IReadOnlyList<string> ThemeList(NetEntity marker)
        => _themeLists.TryGetValue(marker, out var l) ? l : (IReadOnlyList<string>)Array.Empty<string>();

    public static void OnState(PirateRadioStateEvent msg)
    {
        // A new relay session resets what the page may have buffered.
        _entries[msg.Marker] = new Entry
        {
            StationId = msg.StationId,
            Playing = msg.Playing,
            Relay = msg.Relay,
        };
    }
}
