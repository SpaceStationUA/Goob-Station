// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Robust.Client.WebView;
using Robust.Shared.Log;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     TUI IPC over before-browse navigation (Nova-style deferred iframe).
///     JS signals an action by navigating a hidden iframe to
///     <c>res://.../tui_bridge/&lt;tx&gt;?action=X[&amp;data=Y]</c>; we cancel the
///     navigation and queue work for the main thread. Replies arrive as
///     ExecuteJavaScript calls into <c>window.__tuiDispatch(tx, payloadJson)</c>.
///     Transport is entirely content-side: <c>res</c> is already a standard
///     scheme in stock engine 277, so plain (unpatched) engine builds work —
///     no scheme flags, no resource-handler patching.
/// </summary>
public sealed class WebUiTuiIpc
{
    private const string Marker = "/tui_bridge/";

    private readonly ConcurrentQueue<(string tx, string action, string? data)> _pending = new();

    private readonly Action<string, string?> _onAction;
    private readonly bool _allowExternal;
    private WebViewControl? _web;

    /// <summary>
    ///     Optional synchronous provider: action + data → response JSON
    ///     (e.g. the mock uplink backend). Runs on the main thread inside
    ///     <see cref="Pump"/>; returning null defers to the async action path.
    /// </summary>
    public Func<string, string?, string?>? SyncDispatch;

    /// <summary>
    ///     Window-scoped whitelist of http(s) hosts that may be navigated.
    ///     Used by the TV window to allow curated channel sites (iframe of a
    ///     third-party player inside our own app page). Everything else stays
    ///     fenced so a page cannot drive arbitrary browsing.
    /// </summary>
    public List<string>? AllowHttpHosts;

    public WebUiTuiIpc(Action<string, string?> onAction, bool allowExternal = false)
    {
        _onAction = onAction;
        // External windows ("webuiopen <url>") allow http/https navigation;
        // resource windows stay fenced so only our pages can load.
        _allowExternal = allowExternal;
    }

    public void Attach(WebViewControl web)
    {
        _web = web;
    }

    /// <summary>
    ///     Before-browse hook: cancel and capture bridge navigations, fence
    ///     everything that is not a resource scheme URL.
    /// </summary>
    public void HandleBeforeBrowse(IBeforeBrowseContext ctx)
    {
        var url = ctx.Url;

        if (TryParseBridge(url, out var tx, out var action, out var data))
        {

            // Navigation to a custom scheme without a valid action is not a
            // real page load; always cancel and carry the payload over the queue.
            ctx.DoCancel();
            _pending.Enqueue((tx, action, data));
            return;
        }

        // Hash-based report bridge (cross-origin pages whose CSP blocks the
        // res:// iframe): "#tuireport=<action>|<json>". Put the hash back so
        // the page's URL looks unchanged, and carry the payload.
        var hashIdx = url.IndexOf("#tuireport=", StringComparison.OrdinalIgnoreCase);
        if (hashIdx >= 0)
        {
            var payload = url[(hashIdx + "#tuireport=".Length)..];
            var bar = payload.IndexOf('|');
            var hAction = bar < 0 ? payload : payload[..bar];
            var hData = bar < 0 ? null : payload[(bar + 1)..];
            ctx.DoCancel();
            _pending.Enqueue(("h" + _hashSeq++, Uri.UnescapeDataString(hAction),
                hData == null ? null : Uri.UnescapeDataString(hData)));
            return;
        }

        if (url.StartsWith("res://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("usr://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
            return; // Normal in-page navigation; let it through.

        if ((url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
             url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) &&
            IsUrlAllowed(url))
            return; // Whitelisted channel host (TV): navigation is the feature.

        ctx.DoCancel();
    }

    private int _hashSeq;

    private bool IsUrlAllowed(string url)
    {
        if (_allowExternal)
            return true;

        if (AllowHttpHosts == null)
            return false;

        foreach (var pattern in AllowHttpHosts)
        {
            if (pattern.StartsWith("*", StringComparison.Ordinal))
            {
                var suffix = pattern[1..]; // "*.cdn.example" → ".cdn.example"
                if (url.Contains("://" + suffix.TrimStart('.'), StringComparison.OrdinalIgnoreCase) ||
                    ExtraContains(url, suffix))
                    return true;
            }
            else if (url.Contains("://" + pattern, StringComparison.OrdinalIgnoreCase) ||
                     ExtraContains(url, "." + pattern))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ExtraContains(string url, string needle)
    {
        return url.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Dispatches queued actions on the main thread. Call every frame.
    /// </summary>
    public void Pump()
    {
        while (_pending.TryDequeue(out var item))
        {
            try
            {
    
                var response = SyncDispatch?.Invoke(item.action, item.data);
                if (response != null)
                    Respond(item.tx, response);
                else if (SyncDispatch == null)
                    Respond(item.tx, "{\"status\":\"ok\",\"data\":null}");
                _onAction(item.action, item.data);
            }
            catch (Exception e)
            {
                Logger.ErrorS("webui.tui", $"Bridge dispatch failure: {e}");
                Respond(item.tx, "{\"status\":\"error\",\"error\":\"dispatch-failure\"}");
            }
        }
    }

    private void Respond(string tx, string json)
    {
        // The receiver is embedded per-response so it also works when the engine
        // ExecuteJavaScript lands before the page installed its own listener.
        var code = "(function (tx, payloadJson) {\"use strict\"; " +
            "window.dispatchEvent(new CustomEvent('tui-dispatch', { detail: { tx: tx, payload: JSON.parse(payloadJson) } }));" +
            "})(" + WebUiSpikeBridge.JsonString(tx) + ", " + WebUiSpikeBridge.JsonString(json) + ");";

        _web?.ExecuteJavaScript(code);
    }

    /// <summary>
    ///     Pushes an unsolicited update to the page (e.g. server state change).
    /// </summary>
    public void Push(string eventName, string json)
    {
        _web?.ExecuteJavaScript(
            "(function (name, payloadJson) {" +
            "  window.dispatchEvent(new CustomEvent('tui-push', {" +
            "    detail: { name: name, payload: JSON.parse(payloadJson) } }));" +
            "})(" + WebUiSpikeBridge.JsonString(eventName) + ", " + WebUiSpikeBridge.JsonString(json) + ");");
    }

    private static bool TryParseBridge(string url, out string tx, out string action, out string? data)
    {
        tx = "";
        action = "";
        data = null;

        var marker = url.IndexOf(Marker, StringComparison.OrdinalIgnoreCase);
        if (marker < 0)
            return false;

        var tail = url.Substring(marker + Marker.Length);
        var query = "";
        var qIdx = tail.IndexOf('?');
        if (qIdx >= 0)
        {
            query = tail[(qIdx + 1)..];
            tail = tail[..qIdx];
        }

        tx = Uri.UnescapeDataString(tail);
        if (tx.Length == 0)
            return false;

        if (query.Length > 0)
        {
            foreach (var pair in query.Split('&'))
            {
                var kv = pair.Split('=', 2);
                if (kv.Length != 2)
                    continue;
                var value = Uri.UnescapeDataString(kv[1]);
                if (kv[0] == "action")
                    action = value;
                else if (kv[0] == "data")
                    data = value;
            }
        }

        return action.Length != 0;
    }
}
