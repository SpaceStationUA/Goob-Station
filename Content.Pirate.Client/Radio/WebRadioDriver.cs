// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Pirate.Client._Pirate.WebUI;
using Robust.Client.WebView;

namespace Content.Pirate.Client.Radio;

/// <summary>
///     Page-side bridge for the PDA radio over the shared TUI IPC
///     (tui-push events). The page owns the actual &lt;audio&gt; element
///     and its own play/pause/volume; C# only pushes the catalog and the
///     server's selected-station state.
/// </summary>
public sealed class WebRadioDriver
{
    private readonly WebUiTuiIpc _ipc;
    private WebViewControl? _web;
    private string _lastCatalog = "";
    private string _lastState = "";

    public WebRadioDriver(WebUiTuiIpc ipc)
    {
        _ipc = ipc;
    }

    /// <summary>Clear the dedupe caches so the next catalog/state push is
    /// delivered for sure. Needed when a push may have been consumed while
    /// the webview was hidden (e.g. theme switched in the picker while the
    /// radio fragment was detached): the page never saw that payload, so
    /// equality against the cache would swallow every retry forever.</summary>
    public void ResetDedupe()
    {
        _lastCatalog = "";
        _lastState = "";
        _lastNow = "";
    }

    /// <summary>Push the now-playing title; only re-sends when changed.</summary>
    public void SetNow(string stationId, string title)
    {
        var json = "{\"stationId\":" + WebUiSpikeBridge.JsonString(stationId) +
            ",\"title\":" + WebUiSpikeBridge.JsonString(title) + "}";
        if (json == _lastNow)
            return;
        _lastNow = json;
        _web?.ExecuteJavaScript("window.__radioSetNow && window.__radioSetNow(" +
            WebUiSpikeBridge.JsonString(json) + ");");
        _ipc.Push("radio-now", json);
    }

    private string _lastNow = "";

    public void Attach(WebViewControl web)
    {
        _web = web;
    }

    /// <summary>Push the station catalog; only re-sends when it changed.</summary>
    public void SetCatalog(IReadOnlyList<(string Id, string Label, string Genre, string Url, bool Featured, bool Relay)> stations, string theme, IReadOnlyList<string> themes)
    {
        List<string> parts = new(stations.Count);
        for (var i = 0; i < stations.Count; i++)
        {
            var s = stations[i];
            parts.Add(
                "{\"id\":" + WebUiSpikeBridge.JsonString(s.Id) +
                ",\"label\":" + WebUiSpikeBridge.JsonString(s.Label) +
                ",\"genre\":" + WebUiSpikeBridge.JsonString(s.Genre) +
                ",\"url\":" + WebUiSpikeBridge.JsonString(s.Url) +
                ",\"featured\":" + (s.Featured ? "true" : "false") +
                ",\"relay\":" + (s.Relay ? "true" : "false") + "}");
        }

        var themesArr = string.Join(",", themes.Select(t => WebUiSpikeBridge.JsonString(t)));
        var json = "{\"theme\":" + WebUiSpikeBridge.JsonString(theme) +
            ",\"themes\":[" + themesArr + "] " +
            ",\"stations\":[" + string.Join(",", parts) + "]}";
        if (json == _lastCatalog)
            return;
        _lastCatalog = json;

        // Belt and braces: the tui-push CustomEvent channel is new; the
        // direct window call is the hand page's battle-tested path.
        _web?.ExecuteJavaScript("window.__radioSetCatalog && window.__radioSetCatalog(" +
            WebUiSpikeBridge.JsonString(json) + ");");
        _ipc.Push("radio-catalog", json);
    }

    /// <summary>Push the current played station; only re-sends when changed.</summary>
    public void SetState(string stationId, bool playing, bool relay = false)
    {
        var json = "{\"stationId\":" + WebUiSpikeBridge.JsonString(stationId) +
            ",\"playing\":" + (playing ? "true" : "false") +
            ",\"relay\":" + (relay ? "true" : "false") + "}";
        if (json == _lastState)
            return;
        _lastState = json;

        _web?.ExecuteJavaScript("window.__radioSetState && window.__radioSetState(" +
            WebUiSpikeBridge.JsonString(json) + ");");
        _ipc.Push("radio-state", json);
    }

    /// <summary>
    ///     Drop the last-sent catalog cache so the next Update re-pushes it.
    ///     Used after the page reports it is ready: pushes fired during the
    ///     load window never reached its listeners. Catalog only - the page
    ///     knows its own playback state and re-pushing the possibly stale
    ///     entry would fight the local player (caused bogus stops/aborts).
    /// </summary>
    public void ResetCaches()
    {
        _lastCatalog = "";
    }

    /// <summary>
    ///     Push a transcoded stream chunk to the page's MediaSource. Chunks
    ///     arrive in order; no dedupe, MSE append is order-sensitive.
    /// </summary>
    public void SetRelayChunk(string base64)
    {
        _web?.ExecuteJavaScript("window.__radioRelayChunk && window.__radioRelayChunk(" +
            WebUiSpikeBridge.JsonString(base64) + ");");
        _ipc.Push("radio-relay-chunk", WebUiSpikeBridge.JsonString(base64));
    }
}
