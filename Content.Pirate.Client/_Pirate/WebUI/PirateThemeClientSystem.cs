// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Pirate.Client._Pirate.WebUI;
using Content.Pirate.Shared.WebUi;
using Robust.Client.UserInterface.Controls;
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
    }

    private readonly Dictionary<NetEntity, Host> _hosts = new();
    private readonly Dictionary<NetEntity, (string Current, List<string> Allowed)> _lastState = new();
    private readonly List<NetEntity> _dead = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PirateThemeStateEvent>(OnState);
        // Bridge for Content.Client (it cannot reference this assembly):
        // the PDA settings tab asks through the static provider.
        Content.Pirate.UIKit.PdaThemeHost.Provider = ToggleThemeHost;
        Content.Pirate.UIKit.PdaThemeHost.CloseAll = DetachAll;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        foreach (var (pda, host) in _hosts)
        {
            try { host.Ipc.Pump(); }
            catch { _dead.Add(pda); }
        }
        foreach (var pda in _dead)
            _hosts.Remove(pda);
        _dead.Clear();
    }

    /// <summary>PdaMenu theme button: attach (once) and toggle the host page.</summary>
    public void ToggleThemeHost(Container parent, EntityUid pda)
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
                // the cached snapshot on the (proven) action reply path.
                SyncDispatch = (action, _) => action == "list" ? host.Snapshot : null,
            };
            host.Web.AddBeforeBrowseHandler(host.Ipc.HandleBeforeBrowse);
            host.Web.Url = WebThemeWindow.ResPrefix + "_Pirate/WebUI/ThemePicker/index.html";
            parent.AddChild(host.Web);
            _hosts[net] = host;
            RequestState(net);
        }

        var h = _hosts[net];
        h.Visible = !h.Visible;
        h.Web.Visible = h.Visible;
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
                RequestState(pda);
                break;
            case "set":
                if (data is { Length: > 0 })
                    IoCManager.Resolve<IEntityNetworkManager>().SendSystemNetworkMessage(
                        new PirateThemeSetEvent { Pda = pda, ThemeId = data });
                break;
        }
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
