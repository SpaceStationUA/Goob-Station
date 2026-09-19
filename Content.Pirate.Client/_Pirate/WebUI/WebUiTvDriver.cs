// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Globalization;
using Robust.Client.WebView;
using Robust.Shared.Log;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     Engine-side half of the Pirate TV. Injects a small hook into the
///     top-level YouTube page (we own the browser, so plain DOM access
///     works), keeps playback pinned to the room clock without disabling
///     the player's own UI, and reports playback state back through the
///     tui_bridge.
///
///     The scripts are raw string literals on purpose: the old piecewise
///     "..." + "..." concatenation had accumulated a syntax error that
///     silently killed the whole injected script (no reports ever arrived,
///     so every TV drifted on its own).
/// </summary>
public sealed class WebUiTvDriver
{
    private readonly WebViewControl _web;

    public WebUiTvDriver(WebViewControl web, WebUiTuiIpc ipc)
    {
        _web = web;
        ipc.SyncDispatch = HandleChannelAction;
    }

    private double _tickAccumulator;

    /// <summary>
    ///     Called every frame by the window. Installs the hook (idempotent),
    ///     applies the room transport, and reports state — all engine-driven,
    ///     because Chrome throttles page timers on hidden pages.
    /// </summary>
    public void Tick(double dt, double roomPos, bool roomPlaying, bool roomMuted, bool enforce)
    {
        _tickAccumulator += dt;
        if (_tickAccumulator < 0.25)
            return;
        _tickAccumulator = 0;
        try
        {
            var pos = roomPos.ToString("0.##", CultureInfo.InvariantCulture);
            var playing = roomPlaying ? "true" : "false";
            var muted = roomMuted ? "true" : "false";
            // Only report and pin *mute* from JS; play/pause/position are
            // driven from C# once the reports are reliable. The old JS
            // play/pause/seeking listeners fought the player and stalled it
            // (video froze while reporting playing=true).
            _web.ExecuteJavaScript(enforce
                ? PinScript(pos, playing, muted) + ReportScript
                : ReportScript);
        }
        catch (Exception e)
        {
            Logger.DebugS("webui.tv", $"tick failed: {e}");
        }
    }

    public void ApplyCommand(string commandJson)
    {
        try
        {
            _web.ExecuteJavaScript(ControlScript(commandJson));
        }
        catch (Exception e)
        {
            Logger.DebugS("webui.tv", $"command failed: {e}");
        }
    }

    private string? HandleChannelAction(string action, string? data)
    {
        if (action == "tv_state" && data != null)
            State?.Invoke(data);

        return "{\"status\":\"ok\",\"data\":null}";
    }

    public event Action<string>? State;

    /// <summary>Sends a control op to the page's &lt;video&gt;.</summary>
    private static string ControlScript(string commandJson) => $$"""
        (function(){
          try {
            var cmd = {{commandJson}};
            if (cmd.k !== 'control') { return; }
            var v = document.querySelector('video');
            if (!v) { return; }
            if (cmd.op === 'play') v.play();
            else if (cmd.op === 'pause') v.pause();
            else if (cmd.op === 'seekTo') v.currentTime = Math.max(0, cmd.arg || 0);
            else if (cmd.op === 'mute') v.muted = !!cmd.arg;
          } catch (e) {}
        })();
        """;

    /// <summary>
    ///     Stores the room state for the C# loop and keeps mute in step. No
    ///     play/pause/seek listeners here: they fought YouTube's player
    ///     (repeated pause/play and micro-seeks) and could stall it outright.
    ///     Only records whether an ad is showing so C# backs off.
    /// </summary>
    private static string PinScript(string pos, string playing, string muted) => $$"""
        (function(){
          try {
            var v = document.querySelector('video');
            if (!v) { return; }
            window.__tuiRoom = { t: {{pos}}, playing: {{playing}}, muted: {{muted}} };
            if (!window.__tuiInAd) {
              window.__tuiInAd = function(){
                var p = document.querySelector('.html5-video-player');
                return !!(p && p.classList && p.classList.contains('ad-showing'));
              };
            }
            if (window.__tuiRoom.muted !== undefined && !!v.muted !== window.__tuiRoom.muted) {
              try { v.muted = window.__tuiRoom.muted; } catch (e) {}
            }
          } catch (e) {}
        })();
        """;

    /// <summary>
    ///     Reports the video state back as a `tv_state` bridge action. Kept
    ///     separate from pinning so a change in one never breaks the other.
    /// </summary>
    private const string ReportScript = """
        (function(){
          try {
            var v = document.querySelector('video');
            if (!v) { return; }
            var cur = {
              t: Math.round((v.currentTime || 0) * 2) / 2,
              dur: Math.round((v.duration || 0) * 2) / 2,
              playing: !v.paused,
              muted: !!v.muted,
              ended: !!v.ended,
              ad: !!(window.__tuiInAd && window.__tuiInAd()),
              title: (function(){
                var m = document.querySelector('meta[property="og:title"]');
                var t = (m && m.content) || '';
                if (!t) { t = (document.title || '').replace(/\s*(?:-|—)\s*YouTube\s*$/i, ''); }
                if (!t || t.indexOf('_') >= 0 || (v.duration || 0) <= 0) { return ''; }
                return t;
              })(),
              err: (v.error && v.error.code) || null
            };
            var ser = JSON.stringify(cur);
            if (ser === window.__tuiSt) { return; }
            window.__tuiSt = ser;

            // Hash bridge: works even where a res:// iframe is CSP-blocked.
            // The IPC intercepts the fragment navigation and cancels it, so
            // the page's URL is unchanged. The iframe send is a fallback for
            // engines that don't intercept fragment navigations.
            try { location.hash = 'tuireport=tv_state|' + encodeURIComponent(ser); } catch (e) {}
            try {
              if (!window.__tuiSendUi) {
                window.__tuiSendUi = function(action, obj){
                  var tx = 't' + Date.now() + '_' + (window.__tuiN = (window.__tuiN || 0) + 1);
                  var f = document.createElement('iframe');
                  f.style.display = 'none';
                  f.src = 'res://webres/_Pirate/WebUI/TV/tui_bridge/' + encodeURIComponent(tx) +
                    '?action=' + encodeURIComponent(action) +
                    (obj ? ('&data=' + encodeURIComponent(JSON.stringify(obj))) : '');
                  document.documentElement.appendChild(f);
                  setTimeout(function(){ f.remove(); }, 2500);
                };
              }
              window.__tuiSendUi('tv_state', cur);
            } catch (e) {}
          } catch (e) {}
        })();
        """;


}
