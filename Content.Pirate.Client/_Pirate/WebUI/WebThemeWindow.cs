// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.WebView;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Maths;
using Robust.Shared.Network;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     Device WebUI theme picker: a small DefaultWindow hosting the
///     res://_Pirate/WebUI/ThemePicker page. The page drives selection via
///     bridge actions; PirateWebUiSystem validates and applies server-side
///     then pushes fresh state back (allowed list is computed there too).
///     Network wiring lives in PirateThemeClientSystem so state pushes
///     reach open windows.
/// </summary>
public sealed class WebThemeWindow : DefaultWindow
{
    public const string ResPrefix = "res://webres/";
    private const string ThemeResPath = "_Pirate/WebUI/ThemePicker/index.html";

    private NetEntity _pda;
    private readonly WebViewControl _web;
    private readonly WebUiTuiIpc _ipc;
    private bool _pageReady;
    private string _lastStateJson = "";
    private readonly Action<string, string?> _onAction;

    public WebThemeWindow(NetEntity pda, Action<string, string?> onAction)
    {
        SetSize = new Vector2i(400, 280);
        TitleLabel.Text = "PDA theme";

        _pda = pda;
        _onAction = onAction;

        _web = new WebViewControl
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        _ipc = new WebUiTuiIpc(ThroughBridge)
        {
            AllowHttpHosts = new List<string>(),
        };
        _web.AddBeforeBrowseHandler(_ipc.HandleBeforeBrowse);
        _ipc.Attach(_web);
        _web.Url = ResPrefix + ThemeResPath;
        Contents.AddChild(_web);

        OnClose += () => { _onAction("closed", null); };
    }

    private void ThroughBridge(string action, string? data)
    {
        Logger.DebugS("webui.theme", $"window {_pda} action '{action}'");
        switch (action)
        {
            case "dbg":
                Logger.DebugS("webui.theme", $"picker page: {data}");
                break;
            case "ready":
                // The page has listeners up; pull the server's fresh state.
                _pageReady = true;
                _onAction("ready", null);

                if (_lastStateJson != "")
                    _ipc.Push("theme-state", _lastStateJson);
                break;
            case "set":
                if (ExtractTheme(data) is { } theme && theme.Length > 0)
                    _onAction("set", theme);
                break;
        }
    }

    private static string? ExtractTheme(string? data)
    {
        if (data == null || data.Length == 0)
            return null;
        const string key = "\"theme\"";
        var at = data.IndexOf(key, StringComparison.Ordinal);
        if (at < 0)
            return null;
        var colon = data.IndexOf(':', at + key.Length);
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
                i++;
            sb.Append(data[i]);
            i++;
        }
        return sb.ToString();
    }

    /// <summary>Pump bridge actions; call from a frame loop while open.</summary>
    public void Pump()
    {
        _ipc.Pump();
    }

    /// <summary>Push the freshest snapshot into the page.</summary>
    public void ApplyState(string current, List<string> allowed)
    {
        Logger.DebugS("webui.theme",
            $"window {_pda} state push (ready={_pageReady}, cur={current})");
        var parts = new List<string>(allowed.Count);
        foreach (var t in allowed)
            parts.Add(WebUiSpikeBridge.JsonString(t));
        _lastStateJson = "{\"current\":\"" + WebUiSpikeBridge.JsonString(current) +
            "\",\"allowed\":[" + string.Join(",", parts) + "]}";
        if (_pageReady)
        {
            _web.ExecuteJavaScript("window.__themeSetState && window.__themeSetState(" +
                WebUiSpikeBridge.JsonString(_lastStateJson) + ");");
            _ipc.Push("theme-state", _lastStateJson);
        }
    }
}
