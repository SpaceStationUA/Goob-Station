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
///     Client half of the station records console evidence board tab,
///     embedded inside the console BUI window (hosting recipe proven on
///     the theme picker + radio: create view, attach, then
///     AlwaysActive; one host per console; scale rebuilds).
/// </summary>
public sealed class PirateEvidenceBoardClientSystem : EntitySystem
{
    private sealed class Host
    {
        public WebViewControl Web = default!;
        public WebUiTuiIpc Ipc = default!;
        public string Snapshot = "";
        public bool Visible;
        public Control? Parent;
        public DateTimeOffset ReadyDue;
        public bool ReadySeen;
        public int Retries;
    }

    private readonly Dictionary<NetEntity, Host> _hosts = new();
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
        SubscribeNetworkEvent<EvidenceBoardStateEvent>(OnState);
        // Bridge for Content.Client (it cannot reference this assembly):
        // the records console requests through the static provider.
        Content.Pirate.UIKit.EvidenceBoardHost.Provider = ToggleHost;
        Content.Pirate.UIKit.EvidenceBoardHost.CloseAll = DetachAll;
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
        foreach (var (net, host) in _hosts)
        {
            if (host.Web.Disposed)
            {
                _dead.Add(net);
                continue;
            }
            try { host.Ipc.Pump(); }
            catch { _dead.Add(net); }
        }
        foreach (var net in _dead)
            _hosts.Remove(net);
        _dead.Clear();
    }

    /// <summary>UI scale changed: dispose + recreate the board webview
    /// (fresh browser = fresh bitmap scale mapping).</summary>
    private void RebuildForScale(bool fromWatchdog = false)
    {
        List<NetEntity> keys = new(_hosts.Keys);
        foreach (var net in keys)
        {
            if (!_hosts.TryGetValue(net, out var host))
                continue;
            if (fromWatchdog && (host.ReadySeen || host.Retries > 2))
                continue;
            var parent = host.Parent;
            var visible = host.Visible;
            if (parent == null)
                continue; // window closed; recreate on next toggle
            try { host.Web.Dispose(); }
            catch { /* ignore */ }

            host.Web = new WebViewControl
            {
                HorizontalExpand = true,
                VerticalExpand = true,
            };
            host.Ipc = new WebUiTuiIpc((action, data) => HandleAction(net, action, data))
            {
                AllowHttpHosts = new List<string>(),
                SyncDispatch = (action, _) => action == "sync"
                    ? "{\"status\":\"ok\",\"data\":" + (host.Snapshot.Length > 0 ? host.Snapshot : "null") + "}"
                    : null,
            };
            host.Web.AddBeforeBrowseHandler(host.Ipc.HandleBeforeBrowse);
            host.Web.Url = BuildUrl();
            parent.AddChild(host.Web);
            host.Web.AlwaysActive = true;
            host.Visible = visible;
            host.Web.Visible = visible;
            host.ReadySeen = false;
            host.ReadyDue = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(2.5);
            RetriesBump(host, fromWatchdog);
            _nudger.Queue(host.Web);
            if (visible)
                SyncRequest(net);
        }
    }

    private static void RetriesBump(Host host, bool fromWatchdog)
    {
        if (fromWatchdog)
            host.Retries++;
        else
            host.Retries = 0;
    }

    private static string BuildUrl()
    {
        // WebThemeWindow.ResPrefix == "res://webres/_Pirate/WebUI/" — the
        // evidence board app folder sits next to ThemePicker.
        return WebThemeWindow.ResPrefix + "_Pirate/WebUI/EvidenceBoard/index.html";
    }

    /// <summary>Console board button: attach (once per window panel) and
    /// toggle. A console window can be closed and REOPENED (fresh panel):
    /// the old webview is either disposed by the window close or orphaned
    /// with the dead panel — either way the board must be rehosted into
    /// the parent that is actually visible, or reopening yields a blank
    /// page.</summary>
    public void ToggleHost(Control parent, EntityUid console)
    {
        var net = _entMan.GetNetEntity(console);
        if (!_hosts.TryGetValue(net, out var host))
        {
            host = new Host();
            _hosts[net] = host;
        }
        if (host.Web == null || host.Web.Disposed || host.Parent != parent)
        {
            if (host.Web != null && !host.Web.Disposed)
            {
                try { host.Web.Dispose(); }
                catch { /* ignore */ }
            }
            host.Web = new WebViewControl
            {
                HorizontalExpand = true,
                VerticalExpand = true,
            };
            host.Ipc = new WebUiTuiIpc((action, data) => HandleAction(net, action, data))
            {
                AllowHttpHosts = new List<string>(),
                SyncDispatch = (action, _) => action == "sync"
                    ? "{\"status\":\"ok\",\"data\":" + (host.Snapshot.Length > 0 ? host.Snapshot : "null") + "}"
                    : null,
            };
            host.Web.AddBeforeBrowseHandler(host.Ipc.HandleBeforeBrowse);
            host.Web.Url = BuildUrl();
            parent.AddChild(host.Web);
            // Browser started at attach with the real root UIScale; pin
            // AlwaysActive afterwards.
            host.Web.AlwaysActive = true;
            host.Parent = parent;
            host.Visible = false; // reset: toggle below flips it open
            host.ReadySeen = false;
            host.ReadyDue = DateTimeOffset.MinValue;
        }

        var h = _hosts[net];
        h.Visible = !h.Visible;
        h.Web.Visible = h.Visible;
        parent.Visible = h.Visible; // the XAML host panel starts hidden
        if (h.Visible)
            SyncRequest(net);
    }

    [Dependency] private readonly IEntityManager _entMan = default!;

    private void SyncRequest(NetEntity console)
    {
        IoCManager.Resolve<IEntityNetworkManager>()
            .SendSystemNetworkMessage(new EvidenceBoardRequestEvent { Console = console, Action = "sync" });
    }

    private void OnState(EvidenceBoardStateEvent msg)
    {
        if (!_hosts.TryGetValue(msg.Console, out var host) || host.Web.Disposed)
            return;
        host.Snapshot = msg.Snapshot;
        host.ReadySeen = true;
        host.ReadyDue = DateTimeOffset.MinValue;
        if (!host.Visible)
            return;
        // Dual-channel intake (push lane proven dead in standalone hosts,
        // alive in-window; pull lane covers the rest).
        try
        {
            host.Web.ExecuteJavaScript("window.__evidenceSetState && window.__evidenceSetState(" +
                WebUiSpikeBridge.JsonString(msg.Snapshot) + ");");
        }
        catch { /* disposed */ }
        host.Ipc.Push("board-state", msg.Snapshot);
    }

    private void HandleAction(NetEntity console, string action, string? data)
    {
        switch (action)
        {
            case "ready":
                if (_hosts.TryGetValue(console, out var readyHost))
                {
                    readyHost.ReadySeen = true;
                    readyHost.ReadyDue = DateTimeOffset.MinValue;
                    readyHost.Retries = 0;
                    SyncRequest(console);
                }
                return;
            case "sync":
                // pulled by the page through SyncDispatch; nothing to do.
                return;
            default:
                // Mutating ops are forwarded (pipe-encoded payload). The
                // bridge packs it as a JSON value, so a string arrives
                // quoted ("2|3|text") — strip the envelope or every int
                // parse server-side fails silently.
                if (_hosts.ContainsKey(console))
                {
                    IoCManager.Resolve<IEntityNetworkManager>().SendSystemNetworkMessage(
                        new EvidenceBoardRequestEvent
                        {
                            Console = console,
                            Action = action,
                            Data = StripJsonQuotes(data),
                        });
                }
                return;
        }
    }

    private static string StripJsonQuotes(string? data)
    {
        if (string.IsNullOrEmpty(data))
            return "";
        if (data.Length >= 2 && data[0] == '"' && data[data.Length - 1] == '"')
            data = data.Substring(1, data.Length - 2);
        var outb = new System.Text.StringBuilder(data.Length);
        for (var i = 0; i < data.Length; i++)
        {
            if (data[i] == '\\' && i + 1 < data.Length)
            {
                i++;
                switch (data[i])
                {
                    case 'n': outb.Append('\n'); break;
                    case 'r': outb.Append('\r'); break;
                    case 't': outb.Append('\t'); break;
                    default: outb.Append(data[i]); break;
                }
            }
            else
            {
                outb.Append(data[i]);
            }
        }
        return outb.ToString();
    }

    /// <summary>Console window closed: kill the page context.</summary>
    public void DetachAll()
    {
        foreach (var host in _hosts.Values)
        {
            try { host.Web.Dispose(); } catch { /* ignore */ }
        }
        _hosts.Clear();
    }
}
