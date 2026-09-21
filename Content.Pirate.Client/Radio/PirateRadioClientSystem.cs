// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using Content.Pirate.Client._Pirate.WebUI;
using Content.Pirate.Shared.Radio;
using Robust.Client.Player;
using Robust.Client.WebView;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.IoC;

namespace Content.Pirate.Client.Radio;

/// <summary>
///     PID radio client half. Owns the persistent playback webviews: the PDA
///     program fragments only host the controls, so closing a program (or
///     the whole PDA UI) does not dispose the CEF browser and the audio
///     keeps playing. One playback slot per radio program entity, so two
///     PDAs can play simultaneously. Playback is stopped when the PDA
///     carrying the program is no longer on the local player.
/// </summary>
public sealed class PirateRadioClientSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;

    private sealed class Playback
    {
        public required WebViewControl View;
        public required WebUiTuiIpc Ipc;
        public required WebRadioDriver Driver;
        public IReadOnlyList<PirateRadioStationEntry>? LastCatalog;
        public bool RequestedCatalog;
        public bool UncarriedStopSent;
    }

    private readonly Dictionary<NetEntity, Playback> _playbacks = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PirateRadioCatalogEvent>(OnCatalog);
        SubscribeNetworkEvent<PirateRadioStateEvent>(OnState);
        SubscribeNetworkEvent<PirateRadioRelayChunkEvent>(OnRelayChunk);
    }

    private void OnCatalog(PirateRadioCatalogEvent msg, EntitySessionEventArgs _)
        => PirateRadioClientState.OnCatalog(msg);

    private void OnState(PirateRadioStateEvent msg, EntitySessionEventArgs _)
        => PirateRadioClientState.OnState(msg);

    private void OnRelayChunk(PirateRadioRelayChunkEvent msg, EntitySessionEventArgs _)
    {
        var payload = Convert.ToBase64String(msg.Data);
        if (_playbacks.TryGetValue(msg.Marker, out var p) && !p.View.Disposed)
            p.Driver.SetRelayChunk(payload);
    }

    /// <summary>
    ///     Returns the persistent playback control for this program entity,
    ///     creating it on first use. The fragment adds this to its own tree; a
    ///     later fragment disposal must detach (not dispose) it.
    /// </summary>
    public WebViewControl? EnsurePlayback(NetEntity marker)
    {
        if (_playbacks.TryGetValue(marker, out var existing))
        {
            if (!existing.View.Disposed)
                return existing.View;
            _playbacks.Remove(marker); // keep-alive contract broken; rebuild
        }

        try
        {
            var view = new WebViewControl
            {
                AlwaysActive = true,
                HorizontalExpand = true,
                VerticalExpand = true,
            };
            var ipc = new WebUiTuiIpc((a, d) => OnAction(marker, a, d))
            {
                // Streams load via media elements, not navigation; no hosts
                // need allow-listing.
                AllowHttpHosts = new List<string>(),
            };
            view.AddBeforeBrowseHandler(ipc.HandleBeforeBrowse);
            var driver = new WebRadioDriver(ipc);
            driver.Attach(view);
            view.Url = "res://webres/_Pirate/WebUI/Radio/index.html";
            _playbacks[marker] = new Playback
            {
                View = view,
                Ipc = ipc,
                Driver = driver,
            };
        }
        catch
        {
            Teardown(marker); // headless/dev
        }

        return _playbacks.TryGetValue(marker, out var p) ? p.View : null;
    }

    public WebViewControl? View(NetEntity marker)
        => _playbacks.TryGetValue(marker, out var p) && !p.View.Disposed ? p.View : null;

    private void OnAction(NetEntity marker, string action, string? data)
    {
        switch (action)
        {
            case "play":
                PirateRadioClientState.Send("play", marker, ExtractStationId(data));
                break;
            case "stop":
                PirateRadioClientState.Send("stop", marker);
                break;
            case "relayready":
                // The page's MediaSource is open: the server may start
                // pushing transcoded chunks.
                IoCManager.Resolve<IEntityNetworkManager>()
                    .SendSystemNetworkMessage(new PirateRadioRelayReadyEvent { Marker = marker });
                break;
            case "ready":
                // The page has finished loading and installed its push
                // listeners. Pushes that happened during the load window were
                // dropped, so force the dedupe caches to re-send on the next
                // Update tick.
                if (_playbacks.TryGetValue(marker, out var ready))
                {
                    ready.LastCatalog = null;
                    ready.Driver.ResetCaches();
                }
                break;
            case "volume":
                // Volume is page-local; kept for future persistence.
                break;
        }
    }

    private static string ExtractStationId(string? data)
    {
        if (string.IsNullOrEmpty(data))
            return "";

        // Deliberately no System.Text.Json: the sandbox allowlist only
        // permits the Serialization attributes. Payload is {"id":"..."}.
        const string key = "\"id\"";
        var at = data.IndexOf(key, StringComparison.Ordinal);
        if (at < 0)
            return "";
        var colon = data.IndexOf(':', at + key.Length);
        if (colon < 0)
            return "";
        var i = colon + 1;
        while (i < data.Length && (data[i] == ' ' || data[i] == '\t'))
            i++;
        if (i >= data.Length || data[i] != '"')
            return "";
        i++;
        var sb = new System.Text.StringBuilder();
        while (i < data.Length && data[i] != '"')
        {
            if (data[i] == '\\' && i + 1 < data.Length)
            {
                i++;
                sb.Append(data[i] switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    _ => data[i],
                });
            }
            else
            {
                sb.Append(data[i]);
            }
            i++;
        }
        return sb.ToString();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_playbacks.Count == 0)
            return;

        List<NetEntity>? dead = null;
        foreach (var (marker, p) in _playbacks)
        {
            if (p.View.Disposed)
            {
                dead ??= new List<NetEntity>();
                dead.Add(marker);
                continue;
            }

            // Program uninstalled / PDA destroyed: kill the playback.
            if (!Exists(GetEntity(marker)))
            {
                dead ??= new List<NetEntity>();
                dead.Add(marker);
                continue;
            }

            // Radio plays only while the PDA carrying the program is on us.
            if (!Carried(marker))
            {
                var st = PirateRadioClientState.Get(marker);
                if (st.Playing && !p.UncarriedStopSent)
                {
                    p.UncarriedStopSent = true;
                    PirateRadioClientState.Send("stop", marker);
                }
            }
            else
            {
                p.UncarriedStopSent = false;
            }

            p.Ipc.Pump();

            if (!p.RequestedCatalog)
            {
                p.RequestedCatalog = true;
                PirateRadioClientState.RequestCatalog(marker);
            }

            var catalog = PirateRadioClientState.Catalog(marker);
            if (catalog != null && !ReferenceEquals(catalog, p.LastCatalog))
            {
                // Catalog only replaces when a new event arrives; pushing per
                // frame would rebuild JSON needlessly.
                p.LastCatalog = catalog;
                p.Driver.SetCatalog(ToTuples(catalog));
            }

            var state = PirateRadioClientState.Get(marker);
            p.Driver.SetState(state.StationId, state.Playing, state.Relay);
        }

        if (dead != null)
        {
            foreach (var marker in dead)
                Teardown(marker);
        }
    }

    /// <summary>Is the program's loader chain anchored at the local player?</summary>
    private bool Carried(NetEntity marker)
    {
        var player = _players.LocalEntity;
        if (player == null)
            return true; // main menu / detached: don't fight the state

        var cur = GetEntity(marker);
        for (var depth = 0; depth < 8 && cur.IsValid(); depth++)
        {
            if (cur == player)
                return true;
            if (!_containers.TryGetContainingContainer(cur, out var container))
                return false;
            cur = container.Owner;
        }
        return false;
    }

    private void Teardown(NetEntity marker)
    {
        if (!_playbacks.Remove(marker, out var p))
            return;

        // Hard-stop the page audio before disposing: disposing the control
        // does not reliably stop the CEF browser, which kept playing.
        try
        {
            p.View.ExecuteJavaScript("window.__radioHardStop && window.__radioHardStop();");
        }
        catch { /* view may be gone */ }
        if (marker.IsValid())
            PirateRadioClientState.Send("stop", marker);
        try { p.View.Dispose(); } catch { /* already gone */ }
    }

    private static List<(string, string, string, string, bool, bool)> ToTuples(
        IReadOnlyList<PirateRadioStationEntry> catalog)
    {
        var list = new List<(string, string, string, string, bool, bool)>(catalog.Count);
        foreach (var s in catalog)
            list.Add((s.Id, s.Label, s.Genre, s.Url, s.Featured, s.Relay));
        return list;
    }
}
