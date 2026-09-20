// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Pirate.Client._Pirate.WebUI;
using Robust.Client.WebView;

namespace Content.Pirate.Client.Radio;

/// <summary>
///     Page-side bridge for the PDA radio. The page owns the actual
///     &lt;audio&gt; element and its own play/pause/volume; C# only pushes the
///     catalog and the server's selected-station state.
/// </summary>
public sealed class WebRadioDriver
{
    private WebViewControl? _web;
    private string _lastCatalog = "";
    private string _lastState = "";

    public WebRadioDriver(WebUiTuiIpc ipc)
    {
        _ipc = ipc;
    }

    private readonly WebUiTuiIpc _ipc;

    public void Attach(WebViewControl web)
    {
        _web = web;
    }

    /// <summary>Push the station catalog; only re-sends when it changed.</summary>
    public void SetCatalog(IReadOnlyList<(string Id, string Label, string Genre, string Url, bool Featured)> stations)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("{\"stations\":[");
        for (var i = 0; i < stations.Count; i++)
        {
            var s = stations[i];
            if (i > 0)
                sb.Append(',');
            sb.Append("{\"id\":").Append(WebUiSpikeBridge.JsonString(s.Id))
              .Append(",\"label\":").Append(WebUiSpikeBridge.JsonString(s.Label))
              .Append(",\"genre\":").Append(WebUiSpikeBridge.JsonString(s.Genre))
              .Append(",\"url\":").Append(WebUiSpikeBridge.JsonString(s.Url))
              .Append(",\"featured\":").Append(s.Featured ? "true" : "false")
              .Append('}');
        }
        sb.Append("]}");

        var json = sb.ToString();
        if (json == _lastCatalog)
            return;
        _lastCatalog = json;
        _web?.ExecuteJavaScript("window.__radioSetCatalog && window.__radioSetCatalog(" +
            WebUiSpikeBridge.JsonString(json) + ");");
    }

    /// <summary>Push the current played station; only re-sends when changed.</summary>
    public void SetState(string stationId, bool playing)
    {
        var json = "{\"stationId\":" + WebUiSpikeBridge.JsonString(stationId) +
            ",\"playing\":" + (playing ? "true" : "false") + "}";
        if (json == _lastState)
            return;
        _lastState = json;
        _web?.ExecuteJavaScript("window.__radioSetState && window.__radioSetState(" +
            WebUiSpikeBridge.JsonString(json) + ");");
    }
}
