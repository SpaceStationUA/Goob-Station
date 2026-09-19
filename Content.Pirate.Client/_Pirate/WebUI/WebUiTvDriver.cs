// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Robust.Client.WebView;
using Robust.Shared.Log;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     Engine-side half of the Pirate TV. Injects a tiny video-element hook
///     into the TV window's main frame (a YouTube embed or a Twitch page —
///     both top-level in our own browser, so plain DOM access works) and
///     reports playback state back through the tui_bridge, exactly like a
///     packaged page does. Remote/sync commands land via <c>__tuiCmd</c>.
/// </summary>
public sealed class WebUiTvDriver
{
    private readonly WebViewControl _web;

    public WebUiTvDriver(WebViewControl web, WebUiTuiIpc ipc)
    {
        _web = web;
        ipc.SyncDispatch = HandleChannelAction;
    }

    /// <summary>
    ///     Called from the window's FrameUpdate; drives the idempotent
    ///     injection. Each navigation gets a fresh frame, so the guard
    ///     resets by itself and the hook installs <see cref="InjectScript"/>
    ///     again ~every 500 ms.
    /// </summary>
    private double _tickAccumulator;

    public void Tick(double dt)
    {
        _tickAccumulator += dt;
        if (_tickAccumulator < 0.5)
            return;
        _tickAccumulator = 0;
        try
        {
            // One inline round per tick: install (idempotent), then report.
            // Chrome throttles page timers on hidden pages, so the work is
            // driven from this engine-side evaluation instead of setInterval.
            _web.ExecuteJavaScript(HookScript + TickScript);
        }
        catch (Exception e)
        {
            Logger.DebugS("webui.tv", $"tick failed: {e}");
        }
    }

    public void ApplyCommand(string commandJson)
    {
        // Run the command inline instead of queueing window.__tuiCmd:
        // background CEF pages throttle setInterval to ~1/min, so a
        // page-side consumer would starve.
        try
        {
            _web.ExecuteJavaScript(ControlScript(commandJson));
        }
        catch (Exception e)
        {
            Logger.DebugS("webui.tv", $"command failed: {e}");
        }
    }

    private static string ControlScript(string commandJson)
    {
        return "(function(){" +
               "try {" +
               "  var cmd = " + commandJson + ";" +
               "  if (cmd.k !== 'control') { return; }" +
               "  var v = document.querySelector('video');" +
               "  if (!v) { return; }" +
               "  if (cmd.op === 'play') v.play();" +
               "  else if (cmd.op === 'pause') v.pause();" +
               "  else if (cmd.op === 'seekTo') v.currentTime = Math.max(0, cmd.arg || 0);" +
               "  else if (cmd.op === 'seek') v.currentTime = Math.max(0, (v.currentTime || 0) + (cmd.arg || 0));" +
               "  else if (cmd.op === 'mute') v.muted = !!cmd.arg;" +
               "} catch (e) {}" +
               "})();";
    }

    private string? HandleChannelAction(string action, string? data)
    {
        if (action == "tv_state" && data != null)
            State?.Invoke(data);

        return "{\"status\":\"ok\",\"data\":null}";
    }

    public event Action<string>? State;

    /// <summary>
    ///     Installs nothing but the sendUi helper: the hook frame marker,
    ///     then the report function — all engine-driven, no page timers.
    /// </summary>
    private const string HookScript =
        "(function(){" +
        "'use strict';" +
        "if (!window.__tuiTv) {" +
        "  window.__tuiTv = true;" +
        "  window.__tuiSendUi = function(action, obj) {" +
        "    var tx = 't'+Date.now()+'_'+(window.__tuiN = (window.__tuiN || 0)+1);" +
        "    var f = document.createElement('iframe');" +
        "    f.style.display = 'none';" +
        "    f.src = 'res://webres/_Pirate/WebUI/TV/tui_bridge/' + encodeURIComponent(tx) +" +
        "      '?action=' + encodeURIComponent(action) +" +
        "      (obj ? ('&data=' + encodeURIComponent(JSON.stringify(obj))) : '');" +
        "    document.documentElement.appendChild(f);" +
        "    setTimeout(function(){ f.remove(); }, 2500);" +
        "  };" +
        "}" +
        "})();";

    /// <summary>Inline per-tick report: dedupes and sends tv_state; also all
    /// fire hide of the YouTube's own "go away" overlays/controls; the page
    /// remains its own session but our TV keeps the viewer pinned.</summary>
    private const string TickScript =
        "(function(){" +
        "try {" +
        "  var v = document.querySelector('video');" +
        "  if (!v) { return; }" +
        "  var bars = document.querySelectorAll('.ytp-chrome-top,.ytp-pause-overlay,.ytp-size-toggle,.ytp-miniplayer-ui');" +
        "  for (var i = 0; i < bars.length; i++) { bars[i].style.display = 'none'; }" +
        "} catch (e) {}" +
        "try {" +
        "  var v = document.querySelector('video');" +
        "  if (!v) { return; }" +
        "  var cur = { t: v.currentTime || 0, dur: v.duration || 0," +
        "              playing: !v.paused, muted: !!v.muted," +
        "              ended: !!v.ended," +
        "              title: (function(){" +
        "                 var m = document.querySelector('meta[property=\"og:title\"]');" +
        "                 var t = (m && m.content) || '';" +
        "                 if (!t) {" +
        "                   t = (document.title || '').replace(/\\s*(?:-|—)\\s*YouTube\\s*$/i, '');" +
        "                 }" +
        "                 // Ignore SPA interim titles (e.g. 'title_select') before" +
        "                 // the player settles; those are element ids, not names." +
        "                 if (!t || t.indexOf('_') >= 0 || (v.duration || 0) <= 0) { return ''; }" +
        "                 return t;" +
        "               })()," +
        "              err: (v.error && v.error.code) || null };" +
        "  var ser = JSON.stringify(cur);" +
        "  if (ser === window.__tuiSt) { return; }" +
        "  window.__tuiSt = ser;" +
        "  window.__tuiSendUi('tv_state', cur);" +
        "} catch (e) {}" +
        "})();";
}
