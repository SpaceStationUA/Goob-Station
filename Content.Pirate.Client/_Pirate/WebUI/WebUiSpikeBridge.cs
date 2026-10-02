// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using Robust.Shared.Log;
using Robust.Client.WebView;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     Spike bridge endpoint for the Pirate WebUI.
///     Intercepts requests to <c>res://_Pirate/WebUI/__bridge__</c> and delivers the
///     URL-encoded action payload to the owning window.
///     Deliberately avoids System.Text.Json: the content sandbox of the stock
///     engine does not whitelist it, and this module must stay compatible with
///     official upstream engine builds.
///     The payload is a plain action name plus an optional opaque data string;
///     structured JSON payloads come in Phase 1 (hand-written writer).
/// </summary>
public sealed class WebUiSpikeBridge
{
    /// <summary>
    ///     Actions received but not yet dispatched to the game (CEF network
    ///     threads invoke the request handler; touching game state or IoC from
    ///     there is not allowed — marshaling happens via <see cref="Pump"/>).
    /// </summary>
    private readonly ConcurrentQueue<(string action, string? data)> _pending =
        new();

    private readonly Action<string, string?> _onAction;

    /// <summary>
    ///     Optional in-line responder. If set and it returns non-null, that
    ///     string is sent as the bridge response directly, synchronous with the
    ///     request. Only "pure" handlers (mock data, no game state / IoC) may
    ///     be wired here — real game state dispatch stays thread-marshaled
    ///     through <see cref="Pump"/>.
    /// </summary>
    public Func<string, string?, string?>? SyncDispatch;

    public WebUiSpikeBridge(Action<string, string?> onAction)
    {
        _onAction = onAction;
    }

    /// <summary>
    ///     Handler to pass into <see cref="Robust.Client.WebView.WebViewControl.AddResourceRequestHandler"/>.
    /// </summary>
    public void HandleRequest(IRequestHandlerContext ctx)
    {
        if (!ctx.Url.Contains("/webui/__bridge__", StringComparison.OrdinalIgnoreCase))
            return; // Not ours: fall through to default resource serving.

        if (!TryGetPayload(ctx, out var action, out var data, out var error))
        {
            RespondError(ctx, error);
            return;
        }

        if (SyncDispatch is { } dispatch && dispatch(action, data) is { } response)
        {
            DoRespond(ctx, response);
            return;
        }

        // The CEF network thread calls into us here; enqueue for main-thread
        // dispatch and acknowledge immediately.
        _pending.Enqueue((action, data));
        RespondOk(ctx);
    }

    private static void TryLogError(string message)
    {
        // Safe-guarded: the CEF thread has no working log manager.
        try
        {
            Logger.ErrorS("webui.spike", message);
        }
        catch (Exception)
        {
        }
    }

    /// <summary>
    ///     Dispatches queued actions on the main thread. Call this every frame
    ///     (e.g. from a window <c>FrameUpdate</c>).
    /// </summary>
    public void Pump()
    {
        while (_pending.TryDequeue(out var item))
        {
            try
            {
                _onAction(item.action, item.data);
            }
            catch (Exception e)
            {
                Logger.ErrorS("webui.spike", $"Bridge action failure: {e}");
            }
        }
    }

    private static bool TryGetPayload(IRequestHandlerContext ctx, out string action, out string? data, out string error)
    {
        action = "";
        data = null;
        error = "";

        var query = new Uri(ctx.Url).Query;
        if (string.IsNullOrEmpty(query))
        {
            error = "missing action";
            return false;
        }

        foreach (var pair in query.Substring(1).Split('&'))
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

        if (action == "")
        {
            error = "missing action";
            return false;
        }

        return true;
    }

    private static void RespondOk(IRequestHandlerContext ctx)
    {
        DoRespond(ctx, "{\"status\":\"ok\"}");
    }

    private static void RespondError(IRequestHandlerContext ctx, string error)
    {
        DoRespond(ctx, "{\"status\":\"error\",\"error\":" + JsonString(error) + "}");
    }

    public static string ErrorJson(string error) =>
        "{\"status\":\"error\",\"error\":" + JsonString(error) + "}";

    private static void DoRespond(IRequestHandlerContext ctx, string json)
    {
        ctx.DoRespondStream(
            new MemoryStream(Encoding.UTF8.GetBytes(json)),
            "application/json");
    }

    /// <summary>
    ///     Formats a string as a JSON string literal (with minimal escaping) so
    ///     content code can produce JSON without System.Text.Json.
    /// </summary>
    public static string JsonString(string value)
    {
        var result = new StringBuilder("\"");
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '"':
                    result.Append("\\\"");
                    break;
                case '\\':
                    result.Append("\\\\");
                    break;
                case '\n':
                    result.Append("\\n");
                    break;
                case '\r':
                    result.Append("\\r");
                    break;
                case '\t':
                    result.Append("\\t");
                    break;
                default:
                    if (ch < 0x20 || ch >= 0x7f)
                        result.Append("\\u").Append(((int) ch).ToString("x4"));
                    else
                        result.Append(ch);
                    break;
            }
        }

        result.Append('"');
        return result.ToString();
    }
}
