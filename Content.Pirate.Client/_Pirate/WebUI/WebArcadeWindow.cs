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
using K = Robust.Client.Input.Keyboard.Key;

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

        // CEF refuses to instantiate a WebAssembly module unless the resource
        // is served as application/wasm (the engine's MIME table has no wasm
        // entry), so WASM games (FRI3) silently fail to load. Register it
        // once, before the control exists.
        try
        {
            IoCManager.Resolve<Robust.Client.WebView.IWebViewManager>()
                .SetResourceMimeType("wasm", "application/wasm");
        }
        catch { /* headless dev */ }

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

    private static string DefaultGameId()
        => PirateArcadeGames.DefaultGame()?.ID ?? "";

    private string GamePath()
    {
        var proto = PirateArcadeGames.Get(_game);
        if (proto != null)
            return proto.Path;
        return PirateArcadeGames.DefaultGame()?.Path ?? "";
    }

    private static string GameLabel(string id)
        => PirateArcadeGames.Get(id)?.Label ?? id;

    private static string? IdOf(string id)
        => PirateArcadeGames.Exists(id) ? id : null;

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
        foreach (var proto in PirateArcadeGames.All)
        {
            var gameId = proto.ID;
            var b = new Button { Text = proto.Label, HorizontalExpand = true };
            if (proto.ID == currentId)
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

    /// <summary>Robust key -> (DOM key, DOM code, legacy keyCode). Covers the
    /// whole alphanumeric block plus the control/navigation keys games use, so
    /// a game needing a new letter doesn't need a bridge change. Explicit
    /// table (no reflection: Enum.TryParse trips the sandbox via
    /// ReadOnlySpan&lt;char&gt;).</summary>
    private static readonly System.Collections.Generic.Dictionary<
        Robust.Client.Input.Keyboard.Key, (string Key, string Code, int KeyCode)> KeyNames = BuildKeyNames();

    private static System.Collections.Generic.Dictionary<
        Robust.Client.Input.Keyboard.Key, (string Key, string Code, int KeyCode)> BuildKeyNames()
    {
        var m = new System.Collections.Generic.Dictionary<            Robust.Client.Input.Keyboard.Key, (string Key, string Code, int KeyCode)>();

        m[K.A] = ("a", "KeyA", 65); m[K.B] = ("b", "KeyB", 66); m[K.C] = ("c", "KeyC", 67);
        m[K.D] = ("d", "KeyD", 68); m[K.E] = ("e", "KeyE", 69); m[K.F] = ("f", "KeyF", 70);
        m[K.G] = ("g", "KeyG", 71); m[K.H] = ("h", "KeyH", 72); m[K.I] = ("i", "KeyI", 73);
        m[K.J] = ("j", "KeyJ", 74); m[K.K] = ("k", "KeyK", 75); m[K.L] = ("l", "KeyL", 76);
        m[K.M] = ("m", "KeyM", 77); m[K.N] = ("n", "KeyN", 78); m[K.O] = ("o", "KeyO", 79);
        m[K.P] = ("p", "KeyP", 80); m[K.Q] = ("q", "KeyQ", 81); m[K.R] = ("r", "KeyR", 82);
        m[K.S] = ("s", "KeyS", 83); m[K.T] = ("t", "KeyT", 84); m[K.U] = ("u", "KeyU", 85);
        m[K.V] = ("v", "KeyV", 86); m[K.W] = ("w", "KeyW", 87); m[K.X] = ("x", "KeyX", 88);
        m[K.Y] = ("y", "KeyY", 89); m[K.Z] = ("z", "KeyZ", 90);

        m[K.Num0] = ("0", "Digit0", 48); m[K.Num1] = ("1", "Digit1", 49);
        m[K.Num2] = ("2", "Digit2", 50); m[K.Num3] = ("3", "Digit3", 51);
        m[K.Num4] = ("4", "Digit4", 52); m[K.Num5] = ("5", "Digit5", 53);
        m[K.Num6] = ("6", "Digit6", 54); m[K.Num7] = ("7", "Digit7", 55);
        m[K.Num8] = ("8", "Digit8", 56); m[K.Num9] = ("9", "Digit9", 57);

        m[K.Escape] = ("Escape", "Escape", 27);
        m[K.Space] = (" ", "Space", 32);
        m[K.Return] = ("Enter", "Enter", 13);
        m[K.NumpadEnter] = ("Enter", "NumpadEnter", 13);
        m[K.BackSpace] = ("Backspace", "Backspace", 8);
        m[K.Tab] = ("Tab", "Tab", 9);
        m[K.Up] = ("ArrowUp", "ArrowUp", 38);
        m[K.Down] = ("ArrowDown", "ArrowDown", 40);
        m[K.Left] = ("ArrowLeft", "ArrowLeft", 37);
        m[K.Right] = ("ArrowRight", "ArrowRight", 39);
        m[K.Shift] = ("Shift", "ShiftLeft", 16);
        m[K.Control] = ("Control", "ControlLeft", 17);
        m[K.Alt] = ("Alt", "AltLeft", 18);
        m[K.Menu] = ("ContextMenu", "ContextMenu", 93);
        m[K.PageUp] = ("PageUp", "PageUp", 33);
        m[K.PageDown] = ("PageDown", "PageDown", 34);
        m[K.Home] = ("Home", "Home", 36);
        m[K.End] = ("End", "End", 35);
        m[K.Insert] = ("Insert", "Insert", 45);
        m[K.Delete] = ("Delete", "Delete", 46);
        m[K.CapsLock] = ("CapsLock", "CapsLock", 20);
        m[K.Comma] = (",", "Comma", 188);
        m[K.Period] = (".", "Period", 190);
        m[K.Slash] = ("/", "Slash", 191);
        m[K.SemiColon] = (";", "Semicolon", 186);
        m[K.Apostrophe] = ("'", "Quote", 222);
        m[K.LBracket] = ("[", "BracketLeft", 219);
        m[K.RBracket] = ("]", "BracketRight", 221);
        m[K.BackSlash] = ("\\", "Backslash", 220);
        m[K.Tilde] = ("`", "Backquote", 192);
        m[K.Equal] = ("=", "Equal", 187);
        m[K.Minus] = ("-", "Minus", 189);

        m[K.F1] = ("F1", "F1", 112); m[K.F2] = ("F2", "F2", 113);
        m[K.F3] = ("F3", "F3", 114); m[K.F4] = ("F4", "F4", 115);
        m[K.F5] = ("F5", "F5", 116); m[K.F6] = ("F6", "F6", 117);
        m[K.F7] = ("F7", "F7", 118); m[K.F8] = ("F8", "F8", 119);
        m[K.F9] = ("F9", "F9", 120); m[K.F10] = ("F10", "F10", 121);
        m[K.F11] = ("F11", "F11", 122); m[K.F12] = ("F12", "F12", 123);

        return m;
    }

    // Keys we have told the page are currently held (Down/Repeat seen, real
    // Up not yet). An Up with no matching Down is spurious (focus/repeat
    // boundary glitch) and must not end a hold.
    private readonly System.Collections.Generic.HashSet<Robust.Client.Input.Keyboard.Key> _heldKeys = new();

    private void OnFirstChanceKey(Robust.Client.Input.KeyEventArgs args, Robust.Client.Input.KeyEventType type)
    {
        // Map once; unmapped keys are not forwarded.
        if (!KeyNames.TryGetValue(args.Key, out var kc))
            return;

        if (type == Robust.Client.Input.KeyEventType.Up)
        {
            // Ignore a release for a key we never saw pressed: synthesizing it
            // would stop held movement mid-hold.
            if (!_heldKeys.Remove(args.Key))
                return;
            DispatchKey("keyup", kc, false);
            return;
        }

        // Down or Repeat: forward both. Held-state games ignore repeats;
        // tap/repeat games (which only listen for keydown) need them.
        _heldKeys.Add(args.Key);
        DispatchKey("keydown", kc, args.IsRepeat);
    }

    private void DispatchKey(string domType, (string Key, string Code, int KeyCode) kc, bool repeat)
    {
        try
        {
            _web.ExecuteJavaScript(
                "window.__tuiKey && window.__tuiKey('" + domType + "','" +
                kc.Key + "','" + kc.Code + "'," + (repeat ? "true" : "false") + "," + kc.KeyCode + ");");
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
            _heldKeys.Clear();
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
        // Keys the page currently believes are held (for the blur watchdog).
        "window.__tuiKeys = {};" +
        // C# feeds us the engine's real key stream (mac CEF drops keyups and
        // mislabels keys). We replay it as genuine DOM events so pages see
        // exactly what a normal browser would: keydown, keydown+repeat, keyup.
        // This is why we no longer swallow repeats — tap/repeat-driven games
        // (movement via onkeydown) need them, and held-state games ignore them.
        "window.__tuiKey = function(domType, k, c, repeat, keyCode) {" +
        "  try {" +
        "    var ev = new KeyboardEvent(domType, {key: k, code: c, repeat: !!repeat, bubbles: true, cancelable: true});" +
        // keyCode/which are read-only on KeyboardEvent; define them so games
        // that still read e.keyCode (older js13k entries) work.
        "    if (keyCode !== undefined && keyCode !== null) {" +
        "      try { Object.defineProperty(ev, 'keyCode', {get: function(){ return keyCode; }});" +
        "            Object.defineProperty(ev, 'which', {get: function(){ return keyCode; }}); } catch (e2) {}" +
        "    }" +
        "    document.dispatchEvent(ev);" +
        "  } catch (e) {}" +
        "  var kl = (k || '').toLowerCase();" +
        "  if (domType === 'keyup') { delete window.__tuiKeys[kl]; }" +
        "  else { window.__tuiKeys[kl] = 1; }" +
        "};" +
        // CEF also delivers its own native key events (some keydowns, few/no
        // keyups), which would double up with our faithful replay. Swallow
        // every *trusted* (browser-generated) key event so the page sees only
        // our complete, consistent stream. Our own synthetic events are
        // untrusted and pass through untouched.
        "var __suppress = function(e) {" +
        "  if (e.isTrusted) { e.stopImmediatePropagation(); e.preventDefault(); }" +
        "};" +
        "window.addEventListener('keydown', __suppress, true);" +
        "window.addEventListener('keyup', __suppress, true);" +
        "window.addEventListener('keypress', __suppress, true);" +
        // Self-heal: if the page loses focus mid-press, the engine may never
        // deliver the keyup, leaving a key "stuck" held. Release everything.
        "window.__tuiDrop = function() {" +
        "  for (var k in window.__tuiKeys) {" +
        "    try { document.dispatchEvent(new KeyboardEvent('keyup', {key: k.length==1 ? k.toUpperCase() : k, bubbles: true})); } catch (e) {}" +
        "  }" +
        "  window.__tuiKeys = {};" +
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
