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

    /// <summary>
    ///     Called from the window's FrameUpdate; drives the idempotent
    ///     injection plus the room-state enforcer. Each navigation gets a
    ///     fresh frame, so the guard resets by itself and the hook installs
    ///     again ~every 500 ms.
    /// </summary>
    /// <param name="enforce">
    ///     When true, the injected script keeps the page's playback locked to
    ///     the room clock (play/pause/position/mute) via event listeners, so
    ///     local clicks/scrubbing bounce back within ~150 ms — WITHOUT
    ///     disabling the player's UI, so fullscreen, skip-ad and settings
    ///     still work. False while no channel plays.
    /// </param>
    public void Tick(double dt, double roomPos, bool roomPlaying, bool roomMuted, bool enforce)
    {
        _tickAccumulator += dt;
        if (_tickAccumulator < 0.15)
            return;
        _tickAccumulator = 0;
        try
        {
            // One inline round per tick: install (idempotent), enforce the
            // room state, then report. Chrome throttles page timers on
            // hidden pages, so the work is driven from this engine-side
            // evaluation instead of setInterval.
            var pos = roomPos.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            var enforceJs = enforce ? EnforceScript(pos, roomPlaying, roomMuted) : "";
            _web.ExecuteJavaScript(HookScript + enforceJs + TickScript);
        }
        catch (Exception e)
        {
            Logger.DebugS("webui.tv", $"tick failed: {e}");
        }
    }

    /// <summary>
    ///     Pins playback to the room without disabling the player's UI.
    ///     Installs one-shot listeners (pause/play/seeking/ratechange/
    ///     volumechange) that revert deviations, and stores the room state
    ///     (incl. "in an ad") for the per-tick snap in <c>TickScript</c>.
    ///     Fullscreen, ad-skip and the settings menu are left alone.
    /// </summary>
    private static string EnforceScript(string pos, bool playing, bool muted)
    {
        var room = "window.__tuiRoom = { t: " + pos + ", playing: " + (playing ? "true" : "false") +
                   ", muted: " + (muted ? "true" : "false") + " };";
        return "(function(){" +
               "try {" +
               "  var v = document.querySelector('video');" +
               "  if (!v) { return; }" +
               room +
               "  if (!window.__tuiPinned) {" +
               "    window.__tuiPinned = true;" +
               "    var r = function() { return window.__tuiRoom; };" +
               // Ads run on the same element and are the player's business:
               // never fight them; the tick snaps back after they end.
               "    var inAd = function() {" +
               "      var p = document.querySelector('.html5-video-player');" +
               "      return !!(p && p.classList && p.classList.contains('ad-showing'));" +
               "    };" +
               "    window.__tuiInAd = inAd;" +
               // Revert any local pause/play to the room's intent (skip ads).
               "    v.addEventListener('pause', function() {" +
               "      var q = r(); if (q && q.playing && !inAd()) { try { v.play(); } catch (e) {} }" +
               "    });" +
               "    v.addEventListener('play', function() {" +
               "      var q = r(); if (q && !q.playing && !inAd()) { try { v.pause(); } catch (e) {} }" +
               "    });" +
               // Revert scrubbing: any seek away from the room clock snaps back.
               "    v.addEventListener('seeking', function() {" +
               "      var q = r(); if (!q || inAd()) { return; }" +
               "      if (Math.abs((v.currentTime || 0) - q.t) > 3) {" +
               "        try { v.currentTime = q.t; } catch (e) {}" +
               "      }" +
               "    });" +
               // Marks that the ad just ended, so the tick snaps immediately.
               "    v.addEventListener('playing', function() { window.__tuiAdEnded = true; });" +
               "    v.addEventListener('ratechange', function() { try { v.playbackRate = 1; } catch (e) {} });" +
               "    v.addEventListener('volumechange', function() {" +
               "      var q = r(); if (q && !!v.muted !== q.muted) { try { v.muted = q.muted; } catch (e) {} }" +
               "    });" +
               "  }" +
               "} catch (e) {}" +
               "})();";
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
        // Enforce the room's transport BEFORE reporting/deduping, so a local
        // click or the video's own controls cannot desync us.
        "try {" +
        "  var v = document.querySelector('video');" +
        "  var r = window.__tuiRoom;" +
        "  var ad = window.__tuiInAd ? window.__tuiInAd() : false;" +
        "  if (v && r && !ad) {" +
        "    if (!v.paused !== r.playing) {" +
        "      if (r.playing) { v.play(); } else { v.pause(); }" +
        "    }" +
        // Snap to the room clock whenever we're not in an ad — playing or
        // paused. This covers reopening a TV (which starts at 0) and the
        // post-ad drift; 3s of slack keeps normal buffering from thrashing.
        "    if ((v.duration || 0) > 0 && Math.abs((v.currentTime || 0) - r.t) > 3) {" +
        "      try { v.currentTime = r.t; } catch (e) {}" +
        "    }" +
        "    if (!!v.muted !== r.muted) { v.muted = r.muted; }" +
        "  }" +
        "} catch (e) {}" +
        "try {" +
        "  var v = document.querySelector('video');" +
        "  if (!v) { return; }" +
        "  var bars = document.querySelectorAll('.ytp-pause-overlay,.ytp-miniplayer-ui');" +
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
