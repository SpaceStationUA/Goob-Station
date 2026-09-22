// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using Content.Pirate.Client._Pirate.WebUI;
using Content.Pirate.Shared.WebUi;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Client.WebView;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     Client half of the device WebUI theme picker, embedded in the PDA
///     settings tab (same hosting shape as the radio webview, where
///     engine->page traffic is proven). One host per PDA, created on first
///     open; the page pulls state through bridge replies so the hosting
///     shape is irrelevant for delivery.
/// </summary>
public sealed class PirateThemeClientSystem : EntitySystem
{
    private sealed class Host
    {
        public WebViewControl Web = default!;
        public WebUiTuiIpc Ipc = default!;
        public string Snapshot = "";
        public string Current = "";
        public List<string> Allowed = new();
        public bool Visible;
        public Control? Parent;
        public DateTimeOffset ReadyDue;
        public bool ReadySeen;
        public int Retries;
    }

    private readonly Dictionary<NetEntity, Host> _hosts = new();
    private readonly Dictionary<NetEntity, (string Current, List<string> Allowed)> _lastState = new();
    private readonly List<NetEntity> _dead = new();

    private readonly PirateWebViewNudger _nudger = new();
    private float _watchScale;
    private bool _scaleDirty;

    public override void Initialize()
    {
        base.Initialize();
        PirateWebViewScaleWatcher.Watch(v =>
        {
            if (Math.Abs(v - _watchScale) < 0.01f)
                return;
            _watchScale = v;
            _scaleDirty = true;
        });
        _watchScale = PirateWebViewScaleWatcher.Current;
        SubscribeNetworkEvent<PirateThemeStateEvent>(OnState);
        // Bridge for Content.Client (it cannot reference this assembly):
        // the PDA settings tab asks through the static provider.
        Content.Pirate.UIKit.PdaThemeHost.Provider = ToggleThemeHost;
        Content.Pirate.UIKit.PdaThemeHost.CloseAll = DetachAll;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        _nudger.Tick();
        var watchdogDue = false;
        foreach (var host in _hosts.Values)
        {
            if (host.ReadyDue != DateTimeOffset.MinValue && DateTimeOffset.UtcNow > host.ReadyDue && !host.ReadySeen)
                watchdogDue = true;
        }
        if (_scaleDirty)
        {
            _scaleDirty = false;
            RebuildForScale();
        }
        else if (watchdogDue)
        {
            RebuildForScale(fromWatchdog: true);
        }
        foreach (var (pda, host) in _hosts)
        {
            try { host.Ipc.Pump(); }
            catch { _dead.Add(pda); }
        }
        foreach (var pda in _dead)
            _hosts.Remove(pda);
        _dead.Clear();
    }

    /// <summary>UI scale changed: dispose + recreate every picker webview
    /// (fresh browser = fresh bitmap scale mapping); the host parent panel
    /// keeps its identical anchor.</summary>
    private void RebuildForScale(bool fromWatchdog = false)
    {
        List<NetEntity> keys = new(_hosts.Keys);
        foreach (var pda in keys)
        {
            if (fromWatchdog && _hosts.TryGetValue(pda, out var skipHost) && (skipHost.ReadySeen || skipHost.Retries > 2))
                continue;
            var host = _hosts[pda];
            var parent = host.Parent;
            var visible = host.Visible;
            var url = WebThemeWindow.ResPrefix + "_Pirate/WebUI/ThemePicker/index.html";
            try { host.Web.Dispose(); } catch { /* ignore */ }
            host.Web = new WebViewControl
            {
                HorizontalExpand = true,
                VerticalExpand = true,
                AlwaysActive = true,
            };
            host.Ipc = new WebUiTuiIpc((action, data) => HandleAction(pda, action, data))
            {
                AllowHttpHosts = new List<string>(),
                SyncDispatch = (action, _) => action == "list"
                    ? "{\"status\":\"ok\",\"data\":" + (host.Snapshot.Length > 0 ? host.Snapshot : "null") + "}"
                    : null,
            };
            host.Web.AddBeforeBrowseHandler(host.Ipc.HandleBeforeBrowse);
            host.Web.Url = url;
            parent?.AddChild(host.Web);
            if (visible)
                RequestState(pda);
            host.Visible = visible;
            host.Web.Visible = visible;
            host.ReadySeen = false;
            host.ReadyDue = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(2.5);
            _nudger.Queue(host.Web);
        }
    }

    /// <summary>PdaMenu theme button: attach (once) and toggle the host page.</summary>
    public void ToggleThemeHost(Control parent, EntityUid pda)
    {
        var net = _entMan.GetNetEntity(pda);
        if (!_hosts.TryGetValue(net, out var host) || host.Web.Disposed)
        {
            host = new Host();
            host.Web = new WebViewControl
            {
                HorizontalExpand = true,
                VerticalExpand = true,
                AlwaysActive = true,
            };
            host.Ipc = new WebUiTuiIpc((action, data) => HandleAction(net, action, data))
            {
                AllowHttpHosts = new List<string>(),
                // Pull-based state: page postAction("list") is answered from
                // the cached snapshot on the (proven) action reply path
                // (full ok envelope: the bridge unwraps status/data).
                SyncDispatch = (action, _) => action == "list"
                    ? "{\"status\":\"ok\",\"data\":" + (host.Snapshot.Length > 0 ? host.Snapshot : "null") + "}"
                    : null,
            };
            host.Web.AddBeforeBrowseHandler(host.Ipc.HandleBeforeBrowse);
            host.Web.Url = WebThemeWindow.ResPrefix + "_Pirate/WebUI/ThemePicker/index.html";
            parent.AddChild(host.Web);
            _hosts[net] = host;
            host.Parent = parent;
            RequestState(net);
        }

        var h = _hosts[net];
        h.Visible = !h.Visible;
        h.Web.Visible = h.Visible;
        parent.Visible = h.Visible; // the XAML host panel starts hidden
    }

    [Dependency] private readonly IEntityManager _entMan = default!;

    private void RequestState(NetEntity pda)
    {
        IoCManager.Resolve<IEntityNetworkManager>()
            .SendSystemNetworkMessage(new PirateThemeListRequestEvent { Pda = pda });
    }

    private void OnState(PirateThemeStateEvent msg)
    {
        if (!_hosts.TryGetValue(msg.Pda, out var host) || host.Web.Disposed)
            return;
        host.Current = msg.Current;
        host.Allowed = msg.Allowed;
        host.Snapshot = "{\"current\":" + WebUiSpikeBridge.JsonString(msg.Current) +
            ",\"allowed\":[" + string.Join(",", ListJson(msg.Allowed)) + "]}";
        // Best-effort push lane too (works in fragment hosting).
        if (host.Visible)
            host.Web.ExecuteJavaScript("window.__themeSetState && window.__themeSetState(" +
                WebUiSpikeBridge.JsonString(host.Snapshot) + ");");
        host.Ipc.Push("theme-state", host.Snapshot);
    }

    private static List<string> ListJson(List<string> items)
    {
        var outList = new List<string>(items.Count);
        foreach (var item in items)
            outList.Add(WebUiSpikeBridge.JsonString(item));
        return outList;
    }

    private void HandleAction(NetEntity pda, string action, string? data)
    {
        switch (action)
        {
            case "ready":
                if (_hosts.TryGetValue(pda, out var readyHost))
                {
                    readyHost.ReadySeen = true;
                    readyHost.ReadyDue = DateTimeOffset.MinValue;
                }
                RequestState(pda);
                break;
            case "set":
                if (ExtractTheme(data) is { Length: > 0 } id)
                    IoCManager.Resolve<IEntityNetworkManager>().SendSystemNetworkMessage(
                        new PirateThemeSetEvent { Pda = pda, ThemeId = id });
                break;
        }
    }

    /// <summary>Data payload of the {"theme":"<id>"} bridge call.</summary>
    private static string? ExtractTheme(string? data)
    {
        if (string.IsNullOrEmpty(data))
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
        var outBase = new System.Text.StringBuilder();
        while (i < data.Length && data[i] != '"')
        {
            if (data[i] == '\\' && i + 1 < data.Length)
                i++;
            outBase.Append(data[i]);
            i++;
        }
        return outBase.ToString();
    }

    /// <summary>Called when the PDA's BUI closes: kill the page contexts.</summary>
    public void DetachAll()
    {
        foreach (var host in _hosts.Values)
        {
            try { host.Web.Dispose(); } catch { /* ignore */ }
        }
        _hosts.Clear();
    }
}
