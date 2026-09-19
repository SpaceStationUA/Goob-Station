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
    /// <summary>
    ///     Arbitrary scheme host; the content-pack prefix lives in the URL
    ///     PATH (res://webres/_Pirate/...) so the stock upstream Web module
    ///     (which drops the host) resolves it as /_Pirate/... directly.
    /// </summary>
    public const string ResPrefix = "res://webres/";

    private const string SpectatorResPath = "_Pirate/WebUI/Arcade/spectator.html";

    /// <summary>Cabinet-loaded game id (mirror of the server's state).</summary>
    private string _game = "";

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

    /// <summary>Diegetic game picker: one button per shipped game, the
    /// loaded one highlighted. Free cabinet: anybody can pick; seated:
    /// only the player.</summary>
    private readonly BoxContainer _gameList = new()
    {
        Orientation = BoxContainer.LayoutOrientation.Vertical,
        Margin = new Thickness(0, 4),
    };

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
            Text = "Обери гру, керуй кнопками Панелі.",
            FontColorOverride = Color.Gray,
        });
        _status.ClipText = true;
        right.AddChild(_status);
        // The title list grows with every shipped game; keep it scrollable so
        // it never pushes the seat buttons off-screen.
        var gameScroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
        };
        gameScroll.AddChild(_gameList);
        right.AddChild(gameScroll);
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
        RebuildGameList("");

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
            UnsubscribeKeyups();
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
        _status.Text = $"автомат: {GameLabel(_game)}";

        try
        {
            var ent = IoCManager.Resolve<IEntityManager>();
            if (CabUid != null && CabUid.Value.IsValid())
                _cabNet = ent.GetNetEntity(CabUid.Value);
        }
        catch { }

        try { _web.Url = ResPrefix + GamePath(); } catch { /* headless dev */ }

        // Key-driven games need keyboard focus right away; if the seat
        // gets taken by someone else the state flips us to the mirror,
        // which doesn't mind having focus either.
        ReturnKeysToGame();

        // Mac-CEF upstream never delivers keyups into the page; the client
        // side bridges Robust's own key stream into the game window.
        SubscribeKeyups();

        SendSeat(true);
        WebArcadeBackend.Subscribe(_cabNet, OnBackendState);
        Register();
    }

    // ===== state mirror (server broadcast) =====

    private void OnBackendState(WebArcadeBackend.CabState s)
    {
        if (_disposed)
            return;
        ApplyGame(s.Game);
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
        _status.Text = $"гра: {GameLabel(_game)} (твій запуск)";
        try
        {
            _web.Url = ResPrefix + GamePath();
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
        try { _web.Url = ResPrefix + SpectatorResPath; } catch { /* headless dev */ }
        SendWatch(true);
    }

    private string TakenText()
    {
        var s = WebArcadeBackend.Snapshot(_cabNet);
        return s.Taken ? "чекаю трансляцію від гравця…" : "автомат вільний — можеш почати гру";
    }

    // ===== game switcher =====

    private string GamePath()
    {
        var def = PirateArcadeGames.List[0].Path;
        foreach (var (i, _, p) in PirateArcadeGames.List)
            if (i == _game)
                return p;
        return def;
    }

    private string GameLabel(string id)
    {
        var def = PirateArcadeGames.List[0].Label;
        foreach (var (i, l, _) in PirateArcadeGames.List)
            if (i == id)
                return l;
        return def;
    }

    private static string? IdOf(string labelOrId)
    {
        foreach (var (i, _, _) in PirateArcadeGames.List)
            if (i == labelOrId)
                return i;
        return null;
    }

    private void ApplyGame(string id)
    {
        if (id == "" || id == _game || IdOf(id) == null)
            return;
        _game = id;
        RebuildGameList(_game);
        _status.Text = _mode == Mode.Player
            ? $"гра: {GameLabel(id)} (твій запуск)"
            : $"автомат: {GameLabel(id)}";
        if (_mode == Mode.Player)
        {
            try
            {
                _web.Url = ResPrefix + GamePath();
            }
            catch { /* headless dev */ }
        }
    }

    /// <summary>One button per shipped title; the loaded one turns green.</summary>
    private void RebuildGameList(string currentId)
    {
        _gameList.RemoveAllChildren();
        _gameList.AddChild(new Label
        {
            Text = "Гру на автоматі:",
            FontColorOverride = Color.Gray,
        });
        foreach (var (id, label, _) in PirateArcadeGames.List)
        {
            var cur = label;
            var gameId = id;
            var b = new Button { Text = cur, HorizontalExpand = true };
            if (id == currentId)
                b.AddStyleClass("StyleClass.Positive");
            b.OnPressed += _ =>
            {
                SendGame(gameId);
                ReturnKeysToGame();
            };
            _gameList.AddChild(b);
        }
    }

    private void SendGame(string id) =>
        IoCManager.Resolve<Robust.Shared.GameObjects.IEntityNetworkManager>()
            .SendSystemNetworkMessage(new PirateArcadeGameEvent { Cab = _cabNet, Game = id });

    // ===== engine-free keyboard bridge (mac CEF forwarding is upstream-
    // broken: keyups never reach the page) — the client watches Robust's
    // raw key stream and synthesizes missing DOM keyups in the page =====

    private Robust.Client.Input.KeyEventAction? _keyHook;

    private void SubscribeKeyups()
    {
        if (_keyups != null)
            return;
        var mgr = IoCManager.Resolve<Robust.Client.Input.IInputManager>();
        mgr.FirstChanceOnKeyEvent += OnFirstChanceKey;
        _keyups = mgr;
    }

    private void UnsubscribeKeyups()
    {
        if (_keyups == null)
            return;
        _keyups.FirstChanceOnKeyEvent -= OnFirstChanceKey;
        _keyups = null;
    }

    private Robust.Client.Input.IInputManager? _keyups;

    /// <summary>Robust key -> (DOM key, DOM code); games often match
    /// KeyboardEvent.code ("KeyW"), so the bridge must carry both.</summary>
    private static readonly System.Collections.Generic.Dictionary<Robust.Client.Input.Keyboard.Key, (string Key, string Code)> KeyNames =
        new()
        {
            [Robust.Client.Input.Keyboard.Key.W] = ("w", "KeyW"),
            [Robust.Client.Input.Keyboard.Key.A] = ("a", "KeyA"),
            [Robust.Client.Input.Keyboard.Key.S] = ("s", "KeyS"),
            [Robust.Client.Input.Keyboard.Key.D] = ("d", "KeyD"),
            [Robust.Client.Input.Keyboard.Key.J] = ("j", "KeyJ"),
            [Robust.Client.Input.Keyboard.Key.K] = ("k", "KeyK"),
            [Robust.Client.Input.Keyboard.Key.Escape] = ("Escape", "Escape"),
            [Robust.Client.Input.Keyboard.Key.Space] = (" ", "Space"),
            [Robust.Client.Input.Keyboard.Key.Return] = ("Enter", "Enter"),
            [Robust.Client.Input.Keyboard.Key.BackSpace] = ("Backspace", "Backspace"),
            [Robust.Client.Input.Keyboard.Key.Up] = ("ArrowUp", "ArrowUp"),
            [Robust.Client.Input.Keyboard.Key.Down] = ("ArrowDown", "ArrowDown"),
            [Robust.Client.Input.Keyboard.Key.Left] = ("ArrowLeft", "ArrowLeft"),
            [Robust.Client.Input.Keyboard.Key.Right] = ("ArrowRight", "ArrowRight"),
            [Robust.Client.Input.Keyboard.Key.Shift] = ("Shift", "ShiftLeft"),
            [Robust.Client.Input.Keyboard.Key.Control] = ("Control", "ControlLeft"),
        };

    private void OnFirstChanceKey(Robust.Client.Input.KeyEventArgs args, Robust.Client.Input.KeyEventType type)
    {
        if (type != Robust.Client.Input.KeyEventType.Up)
            return;
        if (!KeyNames.TryGetValue(args.Key, out var kc))
            return;
        try
        {
            _web.ExecuteJavaScript("window.__tuiKeyUp && window.__tuiKeyUp('" + kc.Key + "','" + kc.Code + "');") ;
        }
        catch { /* headless dev */ }
    }

    /// <summary>
    ///     Puts keyboard focus back on the web view and tells the page to
    ///     release any keys it thinks are still held (they got stuck when
    ///     focus moved to our panel buttons mid-press).
    /// </summary>
    private void ReturnKeysToGame()
    {
        try
        {
            if (!_web.HasKeyboardFocus())
                _web.GrabKeyboardFocus();
            _web.ExecuteJavaScript("window.__tuiDrop && window.__tuiDrop();");
        }
        catch { /* headless dev */ }
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
        if (!NetConnected())
            return;

        IoCManager.Resolve<Robust.Shared.GameObjects.IEntityNetworkManager>()
            .SendSystemNetworkMessage(new PirateArcadeFrameEvent { Cab = _cabNet, Frame = frame });
    }

    private bool NetConnected()
        => IoCManager.Resolve<Robust.Shared.Network.INetManager>().IsConnected;

    private void SendSeat(bool seat)
    {
        if (!NetConnected())
            return;
        IoCManager.Resolve<Robust.Shared.GameObjects.IEntityNetworkManager>()
            .SendSystemNetworkMessage(new PirateArcadeSeatEvent { Cab = _cabNet, Seat = seat });
    }

    private void SendWatch(bool watch)
    {
        if (!NetConnected())
            return;
        IoCManager.Resolve<Robust.Shared.GameObjects.IEntityNetworkManager>()
            .SendSystemNetworkMessage(new PirateArcadeWatchEvent { Cab = _cabNet, Watch = watch });
    }

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

        // Server went away (disconnect/title back to lobby): the window is
        // meaningless and its sources are dead — close it (OnClose cleans
        // up; sends are connection-gated, so this is safe while offline).
        if (!NetConnected())
        {
            Close();
            return;
        }

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
        // Self-heal: if the page loses focus mid-press (UI click, camera
        // move), the page never sees keyup => "stuck" key. Watchdog
        // releases anything still held as soon as focus is gone.
        "window.__tuiKeys = {};" +
        "window.__tuiDowns = {};" +
        // Window-level capture filter: CEF-mac delivers key events WITHOUT
        // `repeat` flags and letter keys arrive as "Unidentified" (mac
        // nativeKeyCode mapping is broken upstream). Swallow every
        // duplicate keydown (the game tracks 'held' from a single press)
        // and rewrite Unidentified keys by keyCode into real ones.
        "window.__tuiKeyUp = function(k, c) {" +
        "  try { document.dispatchEvent(new KeyboardEvent('keyup', {key: k, code: c, bubbles: true})); } catch (e) {}" +
        "  delete window.__tuiDowns[k]; delete window.__tuiKeys[k];" +
        "  delete window.__tuiDowns['unidentified']; delete window.__tuiKeys['unidentified'];" +
        // Latch: CEF's event queue flushes stale OS-repeat keydowns AFTER
        // the release lands; latch swallows them so the game doesn't
        // re-add the key as held right after a clean release. (Lowercase:
        // the keydown filter compares against lowercased e.key.)
        "  var kl = k.toLowerCase();" +
        "  window.__tuiLatch[kl] = Date.now() + 300;" +
        "  window.__tuiLatch['unidentified'] = Date.now() + 300;" +
        "};" +
        "var __kmap = {87:['w','KeyW'], 65:['a','KeyA'], 83:['s','KeyS'], 68:['d','KeyD'], 74:['j','KeyJ'], 75:['k','KeyK'], 38:['ArrowUp','ArrowUp'], 40:['ArrowDown','ArrowDown'], 37:['ArrowLeft','ArrowLeft'], 39:['ArrowRight','ArrowRight'], 27:['Escape','Escape'], 32:[' ','Space'], 13:['Enter','Enter'], 8:['Backspace','Backspace'], 16:['Shift','ShiftLeft'], 17:['Control','ControlLeft']};" +
        "window.__tuiLatch = {};" +
        "window.addEventListener('keydown', function(e) {" +
        "  var k = (e.key || '').toLowerCase();" +
        "  if (window.__tuiDowns[k] === 1) { e.stopImmediatePropagation(); return; }" +
        "  if (window.__tuiLatch[k] && Date.now() < window.__tuiLatch[k]) { e.stopImmediatePropagation(); return; }" +
        "  window.__tuiDowns[k] = 1;" +
        "  if ((e.key === 'Unidentified' || e.key === undefined) && e.keyCode && __kmap[e.keyCode]) {" +
        "    var m = __kmap[e.keyCode];" +
        "    e.preventDefault(); e.stopImmediatePropagation();" +
        "    window.dispatchEvent(new KeyboardEvent('keydown', {key: m[0], code: m[1], keyCode: e.keyCode, which: e.keyCode, bubbles: true}));" +
        "  }" +
        "}, true);" +
        "document.addEventListener('keydown', function(e) {" +
        "  var k = (e.key || '').toLowerCase();" +
        "  if (window.__tuiKeys[k]) window.__tuiKeys[k] = 1;" +
        "}, true);" +
        "document.addEventListener('keyup', function(e) {" +
        "  var k = (e.key || '').toLowerCase();" +
        "  delete window.__tuiDowns[k]; delete window.__tuiKeys[k];" +
        "}, true);" +
        "window.__tuiDrop = function() {" +
        "  for (var k in window.__tuiKeys) {" +
        "    try { document.dispatchEvent(new KeyboardEvent('keyup', {key: k.length==1 ? k.toUpperCase() : k})); } catch (e) {}" +
        "  }" +
        "  window.__tuiKeys = {};" +
        "  window.__tuiDowns = {};" +
        "};" +
        "document.addEventListener('blur', window.__tuiDrop);" +
        "window.__tuiSendUi = function(action, obj) {" +
        "  var tx = 't'+Date.now()+'_'+(window.__tuiN = (window.__tuiN || 0)+1);" +
        "  var f = document.createElement('iframe');" +
        "  f.style.display = 'none';" +
        "  f.src = 'res://webres/_Pirate/WebUI/TV/tui_bridge/' + encodeURIComponent(tx) +" +
        "    '?action=' + encodeURIComponent(action) +" +
        "    (obj ? ('&data=' + encodeURIComponent(JSON.stringify(obj))) : '');" +
        "  document.documentElement.appendChild(f);" +
        "  setTimeout(function(){ f.remove(); }, 2500);" +
        "};" +
        // Downscale encode: 10 fps on a ~520px-wide copy — the wire saw
        // ~4x fewer bytes than the previous 150ms full-size interval.
        "var oc = document.createElement('canvas');" +
        "var ox = oc.getContext('2d');" +
        "setInterval(function(){" +
        "  try {" +
        "    var c = document.querySelector('canvas');" +
        "    if (!c) { return; }" +
        "    var w = Math.min(520, c.width);" +
        "    var h = Math.round(c.height * (w / c.width));" +
        "    if (oc.width != w || oc.height != h) { oc.width = w; oc.height = h; }" +
        "    ox.drawImage(c, 0, 0, w, h);" +
        "    var d = oc.toDataURL('image/jpeg', 0.45);" +
        "    if (d && d.length > 26) { window.__tuiSendUi('arcade_frame', {f: d.slice(23)}); }" +
        "  } catch (e) {}" +
        "}, 100);" +
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
