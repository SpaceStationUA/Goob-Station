// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.WebView;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Content.Pirate.Shared.Arcade;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     Pirate WebArcade: an arcade machine whose game is a self-contained
///     JS/canvas page served from res://_Pirate/WebUI/Arcade/&lt;game&gt;/ —
///     no network, no external hosts. One player seats themselves (the
///     first opener wins the seat); everyone else gets a live mirror of
///     the player's canvas via server-relayed JPEG frames captured in the
///     player's own browser. Server CPU cost: none (dumb relay).
///     Walk over 10 tiles => the window closes (unseat / end watch).
/// </summary>
public sealed class WebArcadeWindow : DefaultWindow, IDisposable
{
    /// <summary>The game + its interactive page (res:// relative).</summary>
    private const string GameResPath = "_Pirate/WebUI/Arcade/Packabunchas/index.html";
    private const string SpectatorResPath = "_Pirate/WebUI/Arcade/spectator.html";

    private enum Mode { Pending, Player, Spectator }

    public EntityUid? CabUid;
    private NetEntity _cabNet;

    private Mode _mode = Mode.Pending;

    private readonly WebViewControl _web;
    private readonly WebUiTuiIpc _ipc;

    private readonly Label _status = new()
    {
        Text = "автомат: ...",
        FontColorOverride = Color.LightGray,
        ClipText = true,
    };

    private readonly Button _standUp = new() { Text = "Встати (кінець запуску)", Visible = false };
    private readonly Button _playSeat = new() { Text = "Почати гру", Visible = false };

    /// <summary>Right panel root; the «/» button collapses it.</summary>
    private BoxContainer? _panelBox;

    private readonly Button _panelToggle = new() { Text = "«", MinWidth = 28 };

    private bool _disposed;
    private Action? _frameUnsub;

    public WebArcadeWindow()
    {
        Title = "Ігровий автомат";
        SetSize = new Vector2i(560, 760);

        _web = new WebViewControl
        {
            AlwaysActive = true,
        };
        _ipc = new WebUiTuiIpc(OnIpcAction)
        {
            // Self-contained game: no external hosts at all, res:// is enough.
            AllowHttpHosts = new System.Collections.Generic.List<string>(),
        };
        _web.AddBeforeBrowseHandler(_ipc.HandleBeforeBrowse);

        _web.VerticalExpand = true;
        _web.HorizontalExpand = true;

        var right = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalExpand = true,
            MinWidth = 150,
            MaxWidth = 158,
            Margin = new Thickness(8),
        };
        right.AddChild(new Label
        {
            Text = "Автомат «Пірат»",
            FontColorOverride = Color.Gold,
        });
        right.AddChild(new Label
        {
            Text = "Мишка: перетягни деталі та запакуй сумки.",
            FontColorOverride = Color.Gray,
        });
        _status.ClipText = true;
        right.AddChild(_status);
        right.AddChild(_standUp);
        right.AddChild(_playSeat);

        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            VerticalExpand = true,
            HorizontalExpand = true,
        };
        box.AddChild(_web);
        box.AddChild(right);
        _panelBox = right;

        var column = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MinWidth = 30,
        };
        column.AddChild(_panelToggle);
        box.AddChild(column);

        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat(new Color(24, 20, 12)),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        panel.AddChild(box);
        Contents.AddChild(panel);

        _panelToggle.OnPressed += OnPanelToggle;
        _standUp.OnPressed += OnStandUp;
        _playSeat.OnPressed += OnPlaySeat;

        // Leaving as the player ends the run (diegetic: the seat is lost).
        // Windows only *hide* on close (Dispose is not guaranteed), so the
        // per-cabinet slot release/unsubscriptions happen HERE.
        OnClose += () =>
        {
            try { _web.AlwaysActive = false; } catch { }
            if (_mode == Mode.Player)
                SendSeat(false);
            else if (_mode == Mode.Spectator)
                SendWatch(false);
            Unregister();
            _frameUnsub?.Invoke();
            _frameUnsub = null;
            _disposed = true;
        };
    }

    /// <summary>
    ///     Opens centered: claims the seat; the state broadcast decides
    ///     whether we really own it or get flipped to the spectator view.
    /// </summary>
    public void OpenCenteredArcade()
    {
        OpenCentered();
        _status.Text = "автомат: PACKABUNCHAS (MIT, MattiaFortunati)";

        try
        {
            var ent = IoCManager.Resolve<IEntityManager>();
            if (CabUid != null && CabUid.Value.IsValid())
                _cabNet = ent.GetNetEntity(CabUid.Value);
        }
        catch { }

        try { _web.Url = "res://" + GameResPath; } catch { /* headless dev */ }

        SendSeat(true);
        WebArcadeBackend.Subscribe(_cabNet, OnBackendState);
        Register();
    }

    // ===== state mirror (server broadcast) =====

    private void OnBackendState(WebArcadeBackend.CabState s)
    {
        if (_disposed)
            return;
        var me = IoCManager.Resolve<IPlayerManager>().LocalPlayer?.Session?.Name ?? "";

        if (!s.Taken)
        {
            switch (_mode)
            {
                case Mode.Player:
                    // The run ended (we stood up): flip to viewing — do not
                    // auto-claim anything new.
                    BecomeSpectator();
                    break;
                case Mode.Spectator:
                    // Seat is free: offer the play button (explicit claim).
                    UpdateIdle("автомат вільний — можеш почати гру");
                    break;
            }
            return;
        }

        if (s.PlayerName == me)
        {
            // It's ours (or confirmation of it): interactive mode.
            if (_mode != Mode.Player)
                BecomePlayer();
            return;
        }

        // Someone else plays: mirror feed.
        if (_mode != Mode.Spectator)
            BecomeSpectator();
        else
            UpdateIdle("");
        _status.Text = "нагляд: " + s.PlayerName + " грає";
    }

    private void BecomePlayer()
    {
        if (_frameUnsub != null)
        {
            _frameUnsub();
            _frameUnsub = null;
        }
        _mode = Mode.Player;
        _standUp.Visible = true;
        _playSeat.Visible = false;
        _status.Text = "гра: PACKABUNCHAS (твій запуск)";
        try
        {
            _web.Url = "res://" + GameResPath;
            // Drives frames when someone watches (the relay only runs then).
            // The capture hook nests inside the page once it has loaded. We
            // re-inject cheaply from FrameUpdate.
        }
        catch { /* headless dev */ }
    }

    private void BecomeSpectator()
    {
        _frameUnsub?.Invoke();
        _frameUnsub = null;
        _mode = Mode.Spectator;
        _standUp.Visible = false;
        _playSeat.Visible = true;
        UpdateIdle(TakenText());
        _frameUnsub = WebArcadeBackend.SubscribeFrames(_cabNet, OnFrameRelay);
        try { _web.Url = "res://" + SpectatorResPath; } catch { /* headless dev */ }
        SendWatch(true);
    }

    private string TakenText()
    {
        var s = WebArcadeBackend.Snapshot(_cabNet);
        return s.Taken ? "чекаю трансляцію від гравця…" : "автомат вільний — можеш почати гру";
    }

    /// <summary>Maps a human hint onto the spectator page's idle line.</summary>
    private void UpdateIdle(string text)
    {
        try { _web.ExecuteJavaScript("__tuiStatus(" + '"' + text.Replace('"', '\'') + '"' + ");"); }
        catch { /* headless dev */ }
    }

    private void OnFrameRelay(string frame)
    {
        // base64 is alphabet-safe, so plain quoting is injection-proof.
        _web.ExecuteJavaScript("__tuiFrame('" + frame + "');");
    }

    // ===== capture relay (player's own page) =====

    private void OnIpcAction(string action, string? data)
    {
        if (action != "arcade_frame" || data == null || _mode != Mode.Player || !_cabNet.IsValid())
            return;
        var frame = ExtractString(data, "f");
        if (frame.Length == 0)
            return;

        IoCManager.Resolve<Robust.Shared.GameObjects.IEntityNetworkManager>()
            .SendSystemNetworkMessage(new PirateArcadeFrameEvent { Cab = _cabNet, Frame = frame });
    }

    private void SendSeat(bool seat)
        => IoCManager.Resolve<Robust.Shared.GameObjects.IEntityNetworkManager>()
            .SendSystemNetworkMessage(new PirateArcadeSeatEvent { Cab = _cabNet, Seat = seat });

    private void SendWatch(bool watch)
        => IoCManager.Resolve<Robust.Shared.GameObjects.IEntityNetworkManager>()
            .SendSystemNetworkMessage(new PirateArcadeWatchEvent { Cab = _cabNet, Watch = watch });

    private void OnStandUp(Robust.Client.UserInterface.Controls.BaseButton.ButtonEventArgs _)
    {
        // Standing up ends *our* run; the window stays open as spectator
        // view (and as a seat-offer button) instead of killing the app.
        SendSeat(false);
    }

    private void OnPlaySeat(Robust.Client.UserInterface.Controls.BaseButton.ButtonEventArgs _)
    {
        // Claim the seat; the next state broadcast confirms (Player mode)
        // or falls back to spectating whoever got in first.
        SendSeat(true);
    }

    // ===== frame =====

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _ipc.Pump();

        // Periodically (re)install the capture hook in the game page: it is
        // idempotent and pages reload on navigation, so a ~quarter-second
        // try keeps it fresh without page timers doing double duty.
        if (_mode == Mode.Player)
            InstallCaptureHook();

        RangeCheck();
    }

    private double _hookAccumulator;

    private void InstallCaptureHook()
    {
        _hookAccumulator += 1.0 / 60.0;
        if (_hookAccumulator < 0.5)
            return;
        _hookAccumulator = 0;
        try
        {
            _web.ExecuteJavaScript(HookScript);
        }
        catch { /* headless dev */ }
    }

    private const string HookScript =
        "(function(){" +
        "'use strict';" +
        "if (window.__tuiArcade) { return; }" +
        "window.__tuiArcade = true;" +
        "window.__tuiSendUi = function(action, obj) {" +
        "  var tx = 't'+Date.now()+'_'+(window.__tuiN = (window.__tuiN || 0)+1);" +
        "  var f = document.createElement('iframe');" +
        "  f.style.display = 'none';" +
        "  f.src = 'res://_Pirate/WebUI/TV/tui_bridge/' + encodeURIComponent(tx) +" +
        "    '?action=' + encodeURIComponent(action) +" +
        "    (obj ? ('&data=' + encodeURIComponent(JSON.stringify(obj))) : '');" +
        "  document.documentElement.appendChild(f);" +
        "  setTimeout(function(){ f.remove(); }, 2500);" +
        "};" +
        "setInterval(function(){" +
        "  var c = document.querySelector('canvas');" +
        "  if (!c) { return; }" +
        "  var d = c.toDataURL('image/jpeg', 0.5);" +
        "  if (d && d.length > 26) { window.__tuiSendUi('arcade_frame', {f: d.slice(23)}); }" +
        "}, 150);" +
        "})();";

    // One window per cabinet per client: re-open attempts focus the
    // existing window instead of stacking sessions.
    private static readonly System.Collections.Generic.Dictionary<int, WebArcadeWindow> _openByCab = new();

    public static bool TryGetOpen(NetEntity cab, out WebArcadeWindow? found)
    {
        lock (_openByCab)
            return _openByCab.TryGetValue(cab.GetHashCode(), out found);
    }

    public static void Forget(int key)
    {
        lock (_openByCab)
            _openByCab.Remove(key);
    }

    private void Register()
    {
        lock (_openByCab)
            _openByCab[_cabNet.GetHashCode()] = this;
    }

    private void Unregister()
    {
        var key = _cabNet.GetHashCode();
        lock (_openByCab)
            if (_openByCab.TryGetValue(key, out var w) && w == this)
                _openByCab.Remove(key);
    }

    /// <summary>Walk away? The window closes (arcade stays at the machine).</summary>
    private void RangeCheck()
    {
        if (CabUid == null || !CabUid.Value.IsValid())
            return;
        try
        {
            var ent = IoCManager.Resolve<IEntityManager>();
            var player = IoCManager.Resolve<IPlayerManager>().LocalEntity;
            if (player == null)
                return;
            if (!ent.TryGetComponent(player.Value, out Robust.Shared.GameObjects.TransformComponent? pt) ||
                !ent.TryGetComponent(CabUid.Value, out Robust.Shared.GameObjects.TransformComponent? tt))
            {
                return;
            }
            if (pt.MapID != tt.MapID)
            {
                Close();
                return;
            }
            var dv = pt.MapPosition.Position - tt.MapPosition.Position;
            if (dv.Length() > 10.0)
                Close();
        }
        catch { }
    }

    private void OnPanelToggle(BaseButton.ButtonEventArgs _)
    {
        if (_panelBox == null)
            return;
        _panelBox.Visible = !_panelBox.Visible;
        _panelToggle.Text = _panelBox.Visible ? "»" : "«";
    }

    private static string ExtractString(string json, string key)
    {
        var idx = json.IndexOf("\"" + key + "\":", StringComparison.Ordinal);
        if (idx < 0)
            return "";
        var colon = idx + key.Length + 2;
        var start = json.IndexOf('"', colon);
        if (start < 0)
            return "";
        var sb = new System.Text.StringBuilder();
        for (var i = start + 1; i < json.Length; i++)
        {
            var c = json[i];
            if (c == '\\')
            {
                if (i + 1 < json.Length)
                    sb.Append(json[++i]);
                continue;
            }
            if (c == '"')
                return sb.ToString();
            sb.Append(c);
        }
        return sb.ToString();
    }

    public void Dispose()
    {
        _disposed = true;
        Unregister();
        _frameUnsub?.Invoke();
    }
}
