// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System;
using System.Collections.Generic;
using System.Linq;
using Content.Pirate.Client._Pirate.WebUI;
using Content.Pirate.Shared.Radio;
using Robust.Client.Player;
using Robust.Client.WebView;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.IoC;
using Robust.Shared.Log;

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
        public Content.Pirate.Client.CartridgeLoader.Cartridges.RadioUi.IRadioWebviewHost? HostPanel;
        public DateTimeOffset ReadyDue;
        public bool ReadySeen;
        public int Retries;
    }

    private readonly Dictionary<NetEntity, Playback> _playbacks = new();

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    private readonly PirateWebViewNudger _nudger = new();
    private float _watchScale;
    private bool _scaleDirty;

    public override void Initialize()
    {
        base.Initialize();
        PirateWebViewScaleWatcher.Watch(OnUiScaleChanged);
        _watchScale = PirateWebViewScaleWatcher.Current;
        SubscribeNetworkEvent<PirateRadioCatalogEvent>(OnCatalog);
        SubscribeNetworkEvent<PirateRadioStateEvent>(OnState);
        SubscribeNetworkEvent<PirateRadioRelayChunkEvent>(OnRelayChunk);
        SubscribeNetworkEvent<PirateRadioNowPlayingEvent>(OnNow);
    }

    private void OnCatalog(PirateRadioCatalogEvent msg, EntitySessionEventArgs _)
        => PirateRadioClientState.OnCatalog(msg);

    private void OnState(PirateRadioStateEvent msg, EntitySessionEventArgs _)
        => PirateRadioClientState.OnState(msg);

    private void OnUiScaleChanged(float value)
    {
        // Debounce: UI-scale slider events fire rapidly; rebuild once per
        // frame at the settled value (the FrameUpdate runs the actual swap).
        if (Math.Abs(value - _watchScale) < 0.01f)
            return;
        _watchScale = value;
        _scaleDirty = true;
    }

    /// <summary>The fragment's host panel (rebuilds re-attach a fresh
    /// webview into it, keeping program-close semantics in sync).</summary>
    public void RegisterHostPanel(NetEntity marker,
        Content.Pirate.Client.CartridgeLoader.Cartridges.RadioUi.IRadioWebviewHost host, WebViewControl? view)
    {
        if (!_playbacks.TryGetValue(marker, out var p) || view == null || p.View != view)
            return;
        p.HostPanel = host;
    }

    private void RebuildForScale(bool fromWatchdog = false)
    {
        foreach (var (marker, p) in _playbacks.ToList())
        {
            if (fromWatchdog && p.ReadySeen)
                continue;
            if (fromWatchdog && p.Retries >= 2)
                continue; // Failsafe: give up after two attempts each round
            var host = p.HostPanel;
            var keep = host?.KeepAlive;
            Control? parent = null;
            var hostValid = keep != null && !keep.Disposed && keep.Parent != null;
            if (hostValid)
            {
                parent = keep!.Parent;
                parent!.RemoveChild(p.View);
            }
            Teardown(marker);
            var fresh = EnsurePlayback(marker);
            if (fresh == null || host == null)
                continue;
            if (hostValid && fresh.Parent == null)
            {
                host.KeepAlive = fresh;
                parent!.AddChild(fresh);
            }
            // Attach done: force the engine to re-allocate the compositor
            // texture (margins pulse resizes the control by 1px, firing its
            // Resized path) - without this some rebuilds paint blank.
            _nudger.Queue(fresh);
            // Watchdog: CEF occasionally finishes the fresh browser in a
            // state where it never paints (blank until another rebuild).
            // If the page does not report ready in time, retry once.
            p.ReadySeen = false;
            p.ReadyDue = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(2.5);
        }
    }

    private void OnNow(PirateRadioNowPlayingEvent msg, EntitySessionEventArgs _)
    {
        PirateRadioClientState.OnNow(msg);
        if (_playbacks.TryGetValue(msg.Marker, out var p) && !p.View.Disposed)
            p.Driver.SetNow(msg.StationId, msg.Title);
    }

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
            // IMPORTANT: the browser must NOT start while detached (no
            // root -> UIScale 1 -> engine bakes that as the browser's
            // device scale factor, and later scale changes mismatch).
            // EnteredTree (attach) starts it; the fragment then flips
            // AlwaysActive so the keep-alive detaches don't kill it.
            var view = new WebViewControl
            {
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
            // Pull lane: the page polls "sync" on the proven nav-hook path;
            // pushes to a hidden webview can be swallowed, so the poll is
            // what keeps theme/station state eventually consistent.
            ipc.SyncDispatch = (action, _) =>
                action == "sync"
                    ? "{\"status\":\"ok\",\"data\":" + BuildSyncJson(marker) + "}"
                    : null;
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

    /// <summary>The program fragment is being (re)attached to the UI. Pushes
    /// delivered while the view was hidden may have never reached the page
    /// (theme switched in the picker is the live case), so clear the dedupe
    /// caches and re-ask the server for the catalog; the next pump resends
    /// everything the page missed.</summary>
    public void OnFragmentAttached(NetEntity marker)
    {
        if (!_playbacks.TryGetValue(marker, out var p) || p.View.Disposed)
            return;
        p.RequestedCatalog = false;
        p.Driver.ResetDedupe();
    }

    private void OnAction(NetEntity marker, string action, string? data)
    {
        Logger.DebugS("webui.radio",
            $"radio action '{action}' from {marker}" +
            (string.IsNullOrEmpty(data) ? "" : " data=" + (data.Length > 240 ? data[..240] + "..." : data)));

        switch (action)
        {
            case "play":
                PirateRadioClientState.Send("play", marker, ExtractStationId(data));
                break;
            case "stop":
                PirateRadioClientState.Send("stop", marker);
                break;
            case "sync":
                break; // answered via SyncDispatch; visibility opens the poll
            case "relayready":
                // The page's MediaSource is open: the server may start
                // pushing transcoded chunks.
                IoCManager.Resolve<IEntityNetworkManager>()
                    .SendSystemNetworkMessage(new PirateRadioRelayReadyEvent { Marker = marker });
                break;
            case "ready":
                if (_playbacks.TryGetValue(marker, out var readyP))
                {
                    readyP.ReadySeen = true;
                    readyP.ReadyDue = DateTimeOffset.MinValue;
                }
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
            case "theme":
                PirateRadioClientState.SendTheme(marker, ExtractTheme(data));
                break;
            case "volume":
                // Volume is page-local; kept for future persistence.
                break;
        }
    }

    /// <summary>Data payload {"theme":"..."} (shared event style).</summary>
    private static string ExtractTheme(string? data)
    {
        if (string.IsNullOrEmpty(data))
            return "";
        const string key = "\"theme\"";
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
            sb.Append(data[i]);
            i++;
        }
        return sb.ToString();
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

        List<NetEntity>? watchdog = null;
        foreach (var (marker, p) in _playbacks)
        {
            if (p.ReadyDue != DateTimeOffset.MinValue && DateTimeOffset.UtcNow > p.ReadyDue && !p.ReadySeen)
                watchdog ??= new List<NetEntity>();
        }
        if (watchdog != null)
        {
            foreach (var (marker, p) in _playbacks.ToList())
            {
                if (!p.ReadySeen && p.ReadyDue != DateTimeOffset.MinValue && DateTimeOffset.UtcNow > p.ReadyDue)
                    p.Retries++;
            }
            RebuildForScale(fromWatchdog: true);
        }

        _nudger.Tick();
        if (_scaleDirty)
        {
            _scaleDirty = false;
            RebuildForScale();
        }

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
                p.Driver.SetCatalog(ToTuples(catalog), PirateRadioClientState.Theme(marker),
                PirateRadioClientState.ThemeList(marker));
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

    /// <summary>Current page snapshot for the sync pull: theme + stations
    /// + playback state, in the same shapes the push lanes use.</summary>
    private static string BuildSyncJson(NetEntity marker)
    {
        var catalog = PirateRadioClientState.Catalog(marker);
        List<string> parts = new(catalog?.Count ?? 0);
        if (catalog != null)
        {
            foreach (var s in catalog)
                parts.Add(
                    "{\"id\":" + WebUiSpikeBridge.JsonString(s.Id) +
                    ",\"label\":" + WebUiSpikeBridge.JsonString(s.Label) +
                    ",\"genre\":" + WebUiSpikeBridge.JsonString(s.Genre) +
                    ",\"url\":" + WebUiSpikeBridge.JsonString(s.Url) +
                    ",\"featured\":" + (s.Featured ? "true" : "false") +
                    ",\"relay\":" + (s.Relay ? "true" : "false") + "}");
        }
        var st = PirateRadioClientState.Get(marker);
        return "{\"theme\":" + WebUiSpikeBridge.JsonString(PirateRadioClientState.Theme(marker)) +
            ",\"themes\":[" + string.Join(",", PirateRadioClientState.ThemeList(marker)
                .Select(t => WebUiSpikeBridge.JsonString(t))) + "]" +
            ",\"stations\":[" + string.Join(",", parts) + "]" +
            ",\"state\":{\"stationId\":" + WebUiSpikeBridge.JsonString(st.StationId) +
            ",\"playing\":" + (st.Playing ? "true" : "false") +
            ",\"relay\":" + (st.Relay ? "true" : "false") + "}" +
            ",\"now\":{\"stationId\":" + WebUiSpikeBridge.JsonString(st.StationId) +
            ",\"title\":" + WebUiSpikeBridge.JsonString(PirateRadioClientState.Title(marker)) + "}}";
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
