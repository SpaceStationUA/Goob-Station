// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Globalization;
using Robust.Client.WebView;
using Robust.Shared.Log;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     Engine-side half of the Pirate TV.
///
///     Design: the page installs a small self-contained agent ONCE; that
///     agent runs on its own timer — it keeps play/pause/position/mute in
///     step with the last room state it was given, and reports the video
///     state back. C# only pushes the room state when it actually changes,
///     so there is no per-frame `ExecuteJavaScript` (which stalls CEF's
///     compositor and made the video jumpy while a plain browser window
///     played the same video smoothly).
/// </summary>
public sealed class WebUiTvDriver
{
    private readonly WebViewControl _web;

    public WebUiTvDriver(WebViewControl web, WebUiTuiIpc ipc)
    {
        _web = web;
        ipc.SyncDispatch = HandleChannelAction;
    }

    // Last room state we pushed into the page; only changes trigger JS.
    private string _pushedSig = "";
    private bool _agentInstalled;

    /// <summary>
    ///     A fresh page load wiped the injected agent; force a re-push on the
    ///     next Tick so the new document gets the room state.
    /// </summary>
    public void OnNavigated()
    {
        _agentInstalled = false;
        _pushedSig = "";
    }

    /// <summary>Push the room state when it changes; make sure the page agent runs.</summary>
    public void Tick(double dt, double roomPos, bool roomPlaying, bool roomMuted, bool enforce)
    {
        var pos = roomPos.ToString("0.##", CultureInfo.InvariantCulture);
        var playing = roomPlaying ? "true" : "false";
        var muted = roomMuted ? "true" : "false";
        var sig = enforce ? $"{pos}|{playing}|{muted}" : "off";

        if (!_agentInstalled || sig != _pushedSig)
        {
            _pushedSig = sig;
            _agentInstalled = true;
            try
            {
                _web.ExecuteJavaScript(enforce
                    ? AgentScript(pos, playing, muted)
                    : "window.__tuiRoom = null;");
            }
            catch (Exception e)
            {
                Logger.DebugS("webui.tv", $"push failed: {e}");
            }
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

    /// <summary>Immediate one-off control (used for responsive button presses).</summary>
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
    ///     The page agent. Installed/updated only when room state changes.
    ///     It samples the video ~4x/s (its own timer, no engine round-trips):
    ///     reports state via the hash bridge, and nudges play/pause/position/
    ///     mute toward the room. Never fights ads and never touches the
    ///     player's own UI. Correction is deliberately gentle (deadband and a
    ///     cooldown) so it reads as "notices a change" rather than stutter.
    /// </summary>
    private static string AgentScript(string pos, string playing, string muted) => $$"""
        (function(){
          try {
            window.__tuiRoom = { t: {{pos}}, playing: {{playing}}, muted: {{muted}}, at: Date.now() };

            if (window.__tuiAgent) { return; }
            window.__tuiAgent = true;

            var inAd = function(){
              var p = document.querySelector('.html5-video-player');
              return !!(p && p.classList && p.classList.contains('ad-showing'));
            };
            window.__tuiInAd = inAd;
            window.__tuiLastSeek = 0;

            var expected = function(){
              var r = window.__tuiRoom;
              if (!r) { return -1; }
              if (!r.playing) { return r.t; }
              return r.t + Math.max(0, (Date.now() - r.at) / 1000);
            };

            window.__tuiTick = function(){
              try {
                var v = document.querySelector('video');
                if (!v) { return; }
                var r = window.__tuiRoom;
                var ad = inAd();

                if (r && !ad) {
                  // Transport: follow the room's play/pause.
                  if (!v.paused !== r.playing) {
                    if (r.playing) { try { v.play(); } catch (e) {} }
                    else { try { v.pause(); } catch (e) {} }
                  }
                  if (!!v.muted !== r.muted) { try { v.muted = r.muted; } catch (e) {} }

                  // Position: correct only on a real desync (>3.5s) and at
                  // most every 4s, so normal buffering lag is left alone.
                  var want = expected();
                  if (want >= 0 && (v.duration || 0) > 0 && !(v.currentTime <= 1 && want <= 1)) {
                    if (Math.abs((v.currentTime || 0) - want) > 3.5) {
                      var now = Date.now();
                      if (now - window.__tuiLastSeek > 4000) {
                        window.__tuiLastSeek = now;
                        try { v.currentTime = want; } catch (e) {}
                      }
                    }
                  }
                }

                // Report (quantized so the hash changes ~2x/s, not 60x/s).
                var cur = {
                  t: Math.round((v.currentTime || 0) * 2) / 2,
                  dur: Math.round((v.duration || 0) * 2) / 2,
                  playing: !v.paused,
                  muted: !!v.muted,
                  ended: !!v.ended,
                  ad: ad,
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
                try { location.hash = 'tuireport=tv_state|' + encodeURIComponent(ser); } catch (e) {}
              } catch (e) {}
            };

            window.__tuiTimer = setInterval(window.__tuiTick, 250);
            window.__tuiTick();
          } catch (e) {}
        })();
        """;
}
