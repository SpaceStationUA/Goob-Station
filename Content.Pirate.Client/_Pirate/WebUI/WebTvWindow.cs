// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Globalization;
using Content.Pirate.Shared.TV;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.WebView;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     Pirate TV viewer: shows the channel of its television (or, if the
///     TV is a mirror, the channel of the master it follows) with a native
///     remote (pause/play/±10s/mute). Every control goes to the server;
///     the authoritative broadcast lands back here and on all other
///     viewers of the same TV/group.
/// </summary>
public sealed class WebTvWindow : DefaultWindow, IDisposable
{
    private readonly WebViewControl _web;
    private readonly WebUiTuiIpc _ipc;
    private readonly WebUiTvDriver _driver;

    private readonly Label _status = new()
    {
        Text = "канал: ...",
        FontColorOverride = Color.LightGray,
    };
    private readonly Label _sourceChip = new()
    {
        Text = "",
        FontColorOverride = Color.Gray,
        ClipText = true,
    };

    private readonly Button _pause = new() { Text = "⏸ Пауза" };
    private readonly Button _play = new() { Text = "▶ Грати" };
    private readonly Button _back10 = new() { Text = "-10с" };
    private readonly Button _back60 = new() { Text = "-60с" };
    private readonly Button _fwd10 = new() { Text = "+10с" };
    private readonly Button _fwd60 = new() { Text = "+60с" };
    private readonly Button _mute = new() { Text = "🔇 Звук" };
    private readonly Button _next = new() { Text = "⏭ Далі" };
    private readonly Button _pick = new() { Text = "🔍 Обрати відео" };

    /// <summary>The TV entity this window watches; far away ⇒ auto-close.</summary>
    public EntityUid? TvUid;

    /// <summary>Right panel root; the «/» button collapses it to leave video space.</summary>
    private BoxContainer? _panelBox;

    private readonly Label _queueTitle = new() { Text = "Черга:", FontColorOverride = Color.Gold };
    private readonly BoxContainer _queueBox = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly Button _panelToggle = new() { Text = "«", MinWidth = 28 };

    // Our own video's last-reported truth (injection hook).
    private double _ourPos = -1000;
    private double _ourDur;
    private bool _ourPlaying;
    private bool _ourMuted;
    private bool _ourInAd;

    /// <summary>
    ///     Set after (re)navigating: we must seek to the room position, and
    ///     keep re-issuing it until the page's real (non-ad) video lands
    ///     there — a fresh load starts at 0 and a pre-roll ad eats early
    ///     seeks.
    /// </summary>
    /// <summary>
    ///     Set after a room-issued seek (Stamp change) or a (re)navigation:
    ///     keep re-issuing the seek until the page lands near the target. A
    ///     fresh load starts at 0 and a pre-roll ad swallows early seeks, so
    ///     this retries ~once a second until it takes.
    /// </summary>
    private bool _pendingRoomSeek;
    private double _pendingSeekTarget;
    private long _lastRoomSeekAtMs;

    /// <summary>Ambient drift corrections (buffering/ad), at most every 3s.</summary>
    private long _lastDriftSeekAtMs;

    // Room truth we already acted on.
    private string _shownUrl = "";
    private long _followedStamp = -1;
    private long _navigatedAtMs;
    private long _strayCheckedAtMs;
    private string _titleSent = "";
    private bool _endedPublished;
    private long _endedAtMs;

    // Queue UI is rebuilt only when it actually changes: rebuilding every
    // frame created fresh buttons under the cursor (a storm of hover
    // sounds) and churned the layout.
    private string _queueSignature = "";

    // Last transport the room told us to apply (avoids per-frame fighting).
    private bool _appliedPlaying;
    private bool _appliedMuted;
    private long _appliedSeekStamp = -1;

    private bool _disposed;

    public WebTvWindow()
    {
        Title = "Pirate TV";
        SetSize = new Vector2i(1120, 660);

        _web = new WebViewControl
        {
            AlwaysActive = true,
        };
        _ipc = new WebUiTuiIpc((_, _) => { })
        {
            AllowHttpHosts = new System.Collections.Generic.List<string>(WebTvChannel.AllowHosts),
        };
        _web.AddBeforeBrowseHandler(_ipc.HandleBeforeBrowse);
        _driver = new WebUiTvDriver(_web, _ipc);
        _driver.State += StateReceived;

        _web.VerticalExpand = true;
        _web.HorizontalExpand = true;

        var right = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalExpand = true,
            MinWidth = 360,
            MaxWidth = 380,
            Margin = new Thickness(8),
        };
        right.AddChild(new Label
        {
            Text = "Кінозал «Піратське ТБ»",
            FontColorOverride = Color.Gold,
        });
        right.AddChild(new Label
        {
            Text = "Пульт: пауза, ±10с, звук і спільний годинник зала.",
            FontColorOverride = Color.Gray,
            ClipText = true,
        });
        _sourceChip.ClipText = true;
        right.AddChild(_sourceChip);
        right.AddChild(_pick);
        var pair = new GridContainer { Columns = 2, Margin = new Thickness(4) };
        pair.AddChild(_back60);
        pair.AddChild(_fwd60);
        pair.AddChild(_back10);
        pair.AddChild(_fwd10);
        right.AddChild(pair);
        var row2 = new GridContainer { Columns = 2, Margin = new Thickness(4) };
        row2.AddChild(_pause);
        row2.AddChild(_play);
        row2.AddChild(_mute);
        row2.AddChild(_next);
        right.AddChild(row2);
        _status.ClipText = true;
        right.AddChild(_status);

        right.AddChild(_queueTitle);
        right.AddChild(_queueBox);

        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            VerticalExpand = true,
            HorizontalExpand = true,
        };
        box.AddChild(_web);
        box.AddChild(right);

        // Panel root: «/» collapses/removes it (video takes the space).
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
            PanelOverride = new StyleBoxFlat(new Color(20, 26, 34)),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        panel.AddChild(box);
        Contents.AddChild(panel);

        _pause.OnPressed += _ => Owner_Send("pause", 0);
        _play.OnPressed += _ => Owner_Send("play", 0);
        _back10.OnPressed += _ => Owner_Send("seek", -10);
        _back60.OnPressed += _ => Owner_Send("seek", -60);
        _fwd10.OnPressed += _ => Owner_Send("seek", 10);
        _fwd60.OnPressed += _ => Owner_Send("seek", 60);
        _mute.OnPressed += _ => Owner_Send("mute", Snapshot().Muted ? 0 : 1);
        _next.OnPressed += OnNextPressed;
        _panelToggle.OnPressed += OnPanelToggle;
        _pick.OnPressed += OnPickPressed;

        // AlwaysActive browsers survive window closes; drop the browser here
        // to stop background audio.
        OnClose += () =>
        {
            try { _web.AlwaysActive = false; } catch { }
            UnregisterWindow();
        };
    }

    // ===== one window per TV (client-side) =====

    private static readonly System.Collections.Generic.Dictionary<int, WebTvWindow> _openByTv = new();

    /// <summary>True when this TV already has a viewer window open.</summary>
    public static bool TryGetOpen(NetEntity tv, out WebTvWindow? found)
    {
        lock (_openByTv)
            return _openByTv.TryGetValue(tv.GetHashCode(), out found);
    }

    private void RegisterWindow()
    {
        if (TvUid == null)
            return;
        var key = PirateTvClientState.Net(TvUid.Value).GetHashCode();
        lock (_openByTv)
            _openByTv[key] = this;
    }

    private void UnregisterWindow()
    {
        if (TvUid == null)
            return;
        var key = PirateTvClientState.Net(TvUid.Value).GetHashCode();
        lock (_openByTv)
        {
            if (_openByTv.TryGetValue(key, out var w) && w == this)
                _openByTv.Remove(key);
        }
    }

    /// <summary>Opens centered and shows whatever its TV currently plays.</summary>
    public void OpenCenteredTv()
    {
        RegisterWindow();
        OpenCentered();
        RequestState();
    }

    private PirateTvClientState.Entry Snapshot()
    {
        if (TvUid == null || !TvUid.Value.IsValid())
            return new PirateTvClientState.Entry();
        return PirateTvClientState.Get(PirateTvClientState.Net(TvUid.Value));
    }

    private void RequestState()
    {
        if (TvUid == null || !TvUid.Value.IsValid())
            return;
        PirateTvClientState.Request(PirateTvClientState.Net(TvUid.Value));
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!PirateTvClientState.Connected)
        {
            Close();
            return;
        }

        if (TvUid == null || !TvUid.Value.IsValid())
        {
            Close();
            return;
        }

        RequestState();
        _ipc.Pump();

        var room = Snapshot();
        var enforce = room.Kind != WebTvChannel.WebTvKind.None && room.Url.Length > 0;
        _driver.Tick(args.DeltaSeconds,
            PirateTvClientState.VideoPos(room, room.Stamp), room.Playing, room.Muted, enforce);

        FollowRoomClock();
        StrayWanderCheck(room);
        RangeCheck();
    }

    /// <summary>Walk away? The window closes (TV view stays at the TV).</summary>
    private void RangeCheck()
    {
        if (TvUid == null || !TvUid.Value.IsValid())
            return;
        try
        {
            var ent = IoCManager.Resolve<IEntityManager>();
            var player = IoCManager.Resolve<Robust.Client.Player.IPlayerManager>().LocalEntity;
            if (player == null)
                return;
            if (!ent.TryGetComponent(player.Value, out TransformComponent? pt) ||
                !ent.TryGetComponent(TvUid.Value, out TransformComponent? tt))
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

    // ===== our own video's reported truth =====

    private void StateReceived(string json)
    {
        var t = ExtractDouble(json, "t");
        var dur = ExtractDouble(json, "dur");
        var playing = ExtractBool(json, "playing");
        var muted = ExtractBool(json, "muted");
        var ended = ExtractBool(json, "ended");
        var ad = ExtractBool(json, "ad");
        if (t != null)
            _ourPos = Math.Max(0, t.Value);
        if (dur != null)
            _ourDur = Math.Max(0, dur.Value);
        if (playing != null)
            _ourPlaying = playing.Value;
        if (muted != null)
            _ourMuted = muted.Value;
        if (ad != null)
            _ourInAd = ad.Value;

        // Finished = the element says so, or we're within half a second of
        // the end (they behave identically: the room rolls to the next).
        var finished = ended == true ||
                       (_ourDur > 30 && _ourPos >= _ourDur - 0.5 && _ourPos > 5);
        if (finished && !_endedPublished)
        {
            // Playlist auto-advance: tell the room. The server dedupes
            // simultaneous reports coming from every mirrored viewer.
            _endedPublished = true;
            _endedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            PirateTvClientState.Send(new PirateTvCommandEvent { Tv = TvNet(), Op = "ended" });
        }
        else if (!finished && _endedPublished &&
                 DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _endedAtMs > 3000)
        {
            _endedPublished = false;
        }

        // The page knows its own title; stamp the entry with it once.
        var title = ExtractString(json, "title");
        if (title != null && title.Length > 0 && title != _titleSent)
        {
            var s = Snapshot();
            if (s.Kind != WebTvChannel.WebTvKind.None && s.Url.Length > 0)
            {
                _titleSent = title;
                PirateTvClientState.Send(new PirateTvCommandEvent
                {
                    Tv = TvNet(),
                    Op = "set_title",
                    Title = title.Length > 100 ? title[..100] : title,
                });
            }
        }

        var mm = (int)(_ourPos / 60);
        var ss = (int)(_ourPos % 60);
        _status.Text = $"{StateLabel()}  {mm:00}:{ss:00}  " + (_ourPlaying ? "▶" : "⏸") + (_ourMuted ? " 🔇" : "");
    }

    private string StateLabel()
    {
        var s = Snapshot();
        return s.Kind != WebTvChannel.WebTvKind.None && s.Label.Length > 0 ? s.Label : "канал";
    }

    private NetEntity TvNet()
        => TvUid != null && TvUid.Value.IsValid() ? PirateTvClientState.Net(TvUid.Value) : NetEntity.Invalid;

    // ===== room-clock follow (shared target = Pos + wall time while playing) =====

    private void FollowRoomClock()
    {
        var s = Snapshot();
        if (s.Kind == WebTvChannel.WebTvKind.None || s.Url.Length == 0)
        {
            if (_shownUrl.Length > 0)
                ClearPage();
            UpdateSourceChip(s);
            return;
        }

        UpdateSourceChip(s);
        RefreshQueue(s);

        if (s.Url != _shownUrl)
        {
            NavigateTo(s);
            return;
        }

        var target = PirateTvClientState.VideoPos(s, s.Stamp);

        // The driver's enforcer re-asserts play/pause/position/mute every
        // tick, so we only remember the room truth here (see Tick call in
        // FrameUpdate). Apply an immediate command too, so a button press
        // feels instant rather than waiting up to a tick.
        if (s.Playing != _appliedPlaying)
        {
            _driver.ApplyCommand("{\"k\":\"control\",\"op\":\"" + (s.Playing ? "play" : "pause") + "\"}");
            _appliedPlaying = s.Playing;
        }

        // A room-issued seek (Stamp changed) must land on EVERY window: retry
        // it promptly until the page reports a position near the target.
        // Ambient drift is handled separately below with a gentler policy.
        if (s.Stamp != _appliedSeekStamp)
        {
            _appliedSeekStamp = s.Stamp;
            _pendingRoomSeek = true;
            _pendingSeekTarget = target;
            _lastRoomSeekAtMs = 0;
        }

        // Follow the room clock's *advancing* target when playing, so a
        // late-joining window stays current.
        if (_pendingRoomSeek)
            _pendingSeekTarget = s.Playing ? target : s.Pos;

        if (_pendingRoomSeek && !_ourInAd && _ourDur > 0)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var want = _pendingSeekTarget;
            if (Math.Abs(_ourPos - want) < 3)
            {
                _pendingRoomSeek = false;
            }
            else if (now - _lastRoomSeekAtMs > 1000)
            {
                _lastRoomSeekAtMs = now;
                SeekTo(want);
            }
        }

        // Gentle ambient drift correction (buffering stutter, ad spillover):
        // only when not already chasing a room seek, off by >3s, and once
        // every 3s.
        if (!_pendingRoomSeek && !_ourInAd && _ourDur > 0 && s.Playing)
        {
            if (Math.Abs(_ourPos - target) > 3)
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (now - _lastDriftSeekAtMs > 3000)
                {
                    _lastDriftSeekAtMs = now;
                    SeekTo(target);
                }
            }
        }

        if (s.Muted != _appliedMuted)
        {
            _driver.ApplyCommand("{\"k\":\"control\",\"op\":\"mute\",\"arg\":" + (s.Muted ? 1 : 0) + "}");
            _appliedMuted = s.Muted;
        }
    }

    private void UpdateSourceChip(PirateTvClientState.Entry s)
    {
        _sourceChip.Text = s.IsMirror ? "джерело: мережа" : "джерело: локально";
    }

    private void ClearPage()
    {
        _shownUrl = "";
        _followedStamp = -1;
        _ourPos = -1000;
        _ourDur = 0;
        _endedPublished = false;
        _appliedPlaying = false;
        _appliedMuted = false;
        _appliedSeekStamp = -1;
        _pendingRoomSeek = false;
        _status.Text = "канал: (нічого не грає)";
        try { _web.Url = "about:blank"; } catch { /* headless dev */ }
    }

    /// <summary>Queue rows: click jumps this TV/group to that position.</summary>
    private void RefreshQueue(PirateTvClientState.Entry s)
    {
        var signature = QueueSignature(s);
        if (signature == _queueSignature)
            return;
        _queueSignature = signature;

        _queueTitle.Text = "Черга (" + s.Queue.Count + ")" + (s.Locked ? " [замкнено]" : "");
        _queueBox.RemoveAllChildren();
        var idx = 0;
        var tv = TvNet();
        foreach (var item in s.Queue)
        {
            var i = idx++;
            _queueBox.AddChild(BuildQueueRow(i, item.Title.Length > 0 ? item.Title : item.Label,
                i == s.QueueNow, s.Locked, ev => SendQueue(ev, tv)));
        }
    }

    private static string QueueSignature(PirateTvClientState.Entry s)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(s.Locked ? '1' : '0').Append('|').Append(s.QueueNow);
        foreach (var item in s.Queue)
            sb.Append('|').Append(item.Title.Length > 0 ? item.Title : item.Label);
        return sb.ToString();
    }

    private void SendQueue(EntityEventArgs ev, NetEntity tv)
    {
        switch (ev)
        {
            case PirateTvQueueNavEvent n: n.Tv = tv; PirateTvClientState.Send(n); break;
            case PirateTvQueueMoveEvent m: m.Tv = tv; PirateTvClientState.Send(m); break;
            case PirateTvQueueRemoveEvent r: r.Tv = tv; PirateTvClientState.Send(r); break;
        }
    }

    /// <summary>
    ///     One playlist entry on a single line: the (clipped) title button
    ///     followed by ▲▼✖. The right panel is wide enough for this now, so
    ///     there is no second tool row. Locked rooms disable the whole row;
    ///     the playing entry turns green.
    /// </summary>
    /// <param name="send">Receives the queue event; the owning window stamps the TV handle.</param>
    public static Control BuildQueueRow(int i, string title, bool nowPlaying, bool locked, Action<EntityEventArgs> send)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 2,
            HorizontalExpand = true,
        };

        var nav = new Button
        {
            Text = (nowPlaying ? "▶ " : "  ") + (i + 1) + ". " + title,
            TextAlign = Label.AlignMode.Left,
            ClipText = true,
            HorizontalExpand = true,
            ToggleMode = false,
            Disabled = locked,
        };

        if (nowPlaying)
            nav.StyleClasses.Add(Content.Client.Stylesheets.StyleClass.Positive);

        nav.OnPressed += _ => send(new PirateTvQueueNavEvent { Index = i });
        row.AddChild(nav);

        row.AddChild(MiniBtn("▲", () => send(new PirateTvQueueMoveEvent { Index = i, Delta = -1 }), locked));
        row.AddChild(MiniBtn("▼", () => send(new PirateTvQueueMoveEvent { Index = i, Delta = 1 }), locked));
        row.AddChild(MiniBtn("✖", () => send(new PirateTvQueueRemoveEvent { Index = i }), locked));

        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Children = { row },
        };
    }

    public static Button MiniBtn(string label, Action act, bool disabled = false)
    {
        var b = new Button { Text = label, MinWidth = 30, MaxWidth = 32, Disabled = disabled };
        b.OnPressed += _ => act();
        return b;
    }

    private void OnNextPressed(BaseButton.ButtonEventArgs _)
        => PirateTvClientState.Send(new PirateTvCommandEvent { Tv = TvNet(), Op = "manual_next" });

    /// <summary>Opens the YouTube picker bound to the same TV (reused if open).</summary>
    private void OnPickPressed(BaseButton.ButtonEventArgs _)
    {
        if (TvUid == null || !TvUid.Value.IsValid())
            return;
        WebTvPickerWindow.OpenFor(TvUid.Value);
    }

    /// <summary>Opens (or focuses) the viewer for a TV; no duplicates.</summary>
    public static void OpenFor(EntityUid tv)
    {
        if (TryGetOpen(PirateTvClientState.Net(tv), out var existing) && existing != null)
        {
            existing.OpenCentered();
            existing.MoveToFront();
            return;
        }

        var window = new WebTvWindow { TvUid = tv };
        window.OpenCenteredTv();
    }

    private void OnPanelToggle(BaseButton.ButtonEventArgs _)
    {
        if (_panelBox == null)
            return;
        _panelBox.Visible = !_panelBox.Visible;
        _panelToggle.Text = _panelBox.Visible ? "»" : "«";
    }

    private void NavigateTo(PirateTvClientState.Entry s)
    {
        _shownUrl = s.Url;
        _followedStamp = -1;
        _ourPos = -1000;
        _ourDur = 0;
        _endedPublished = false;
        _appliedPlaying = false;
        _appliedSeekStamp = -1;
        _pendingRoomSeek = true;
        _pendingSeekTarget = s.Playing
            ? PirateTvClientState.VideoPos(s, s.Stamp)
            : s.Pos;
        _lastRoomSeekAtMs = 0;
        _ourInAd = false;
        _navigatedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        try { _web.Url = s.Url; } catch { /* headless dev */ }
    }

    /// <summary>Sends a position command to the page and marks our own seek
    /// so the scrubbing guard doesn't fight it.</summary>
    private void SeekTo(double pos)
    {
        _driver.ApplyCommand("{\"k\":\"control\",\"op\":\"seekTo\",\"arg\":" +
                             pos.ToString("0.##", CultureInfo.InvariantCulture) + "}");
    }

    /// <summary>
    ///     If a stray click navigated the page away from the room's video
    ///     (another video, the YT home logo, a channel page), bring the TV
    ///     back. Runs at most once a second and only after a short grace
    ///     period, so our own navigations and late SPA redirects don't fight.
    /// </summary>
    private void StrayWanderCheck(PirateTvClientState.Entry room)
    {
        if (room.Url.Length == 0 || _shownUrl.Length == 0)
            return;

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (now - _navigatedAtMs < 4000 || now - _strayCheckedAtMs < 1000)
            return;
        _strayCheckedAtMs = now;

        string current;
        try { current = _web.Url; }
        catch { return; }

        if (current.Length == 0 || current == "about:blank")
            return;

        // Same video? (compare the v= id, ignoring hl/utm/whatever else)
        if (SameVideo(current, room.Url))
            return;

        try { _web.Url = room.Url; } catch { /* headless dev */ }
        _navigatedAtMs = now;
    }

    /// <summary>True when both URLs point at the same YouTube video id.</summary>
    private static bool SameVideo(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
            return true;
        try
        {
            var ua = new Uri(a);
            var ub = new Uri(b);
            if (!string.Equals(ua.Host, ub.Host, StringComparison.OrdinalIgnoreCase))
                return false;
            var va = QueryValue(ua.Query, "v");
            var vb = QueryValue(ub.Query, "v");
            return va != null && vb != null && string.Equals(va, vb, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static string? QueryValue(string query, string key)
    {
        if (query.Length < 2)
            return null;
        foreach (var pair in query[1..].Split('&'))
        {
            var eq = pair.IndexOf('=');
            if (eq < 0)
                continue;
            if (string.Equals(pair[..eq], key, StringComparison.OrdinalIgnoreCase))
                return pair[(eq + 1)..];
        }
        return null;
    }

    // ===== remote (owner) paths =====

    /// <summary>
    ///     Remote button: apply to our own video at once, then move the room
    ///     clock so every other window follows (they land via broadcast).
    /// </summary>
    private void Owner_Send(string op, double arg)
    {
        _driver.ApplyCommand("{\"k\":\"control\",\"op\":\"" + op + "\",\"arg\":" +
                             arg.ToString("0.##", CultureInfo.InvariantCulture) + "}");

        var tv = TvNet();
        switch (op)
        {
            case "pause":
                PirateTvClientState.Send(new PirateTvCommandEvent
                {
                    Tv = tv,
                    Op = "pause",
                    Arg = _ourPos < 0 ? Snapshot().Pos : _ourPos,
                });
                break;
            case "play":
                PirateTvClientState.Send(new PirateTvCommandEvent { Tv = tv, Op = "play" });
                break;
            case "mute":
                PirateTvClientState.Send(new PirateTvCommandEvent { Tv = tv, Op = "mute", Arg = arg });
                break;
            case "seek":
                {
                    // Seek from the ROOM's position (our page may have drifted).
                    var s = Snapshot();
                    var basePos = _ourPos < 0 ? s.Pos : _ourPos;
                    var target = Math.Max(0, basePos + arg);

                    // A forward jump that crosses the end rolls to the next
                    // playlist entry — same outcome as playing to the end.
                    if (arg > 0 && _ourDur > 30 && target >= _ourDur - 0.5)
                    {
                        PirateTvClientState.Send(new PirateTvCommandEvent { Tv = tv, Op = "manual_next" });
                        return;
                    }

                    PirateTvClientState.Send(new PirateTvCommandEvent { Tv = tv, Op = "seekTo", Arg = target });
                    break;
                }
        }
    }

    public void Dispose()
    {
        _disposed = true;
    }

    // ===== tiny JSON readers (flat objects; strings may be quoted) =====

    private static double? ExtractDouble(string json, string key)
    {
        var idx = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
        if (idx < 0)
            return null;
        var colon = json.IndexOf(':', idx + key.Length + 2);
        if (colon < 0)
            return null;
        var end = json.IndexOfAny([',', '}'], colon + 1);
        if (end < 0)
            return null;
        var piece = json[(colon + 1)..end].Trim();
        return double.TryParse(piece, NumberStyles.Any, CultureInfo.InvariantCulture, out var num)
            ? num
            : null;
    }

    private static bool? ExtractBool(string json, string key)
    {
        var idx = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
        if (idx < 0)
            return null;
        var colon = json.IndexOf(':', idx + key.Length + 2);
        if (colon < 0)
            return null;
        var tail = json.Substring(colon + 1).TrimStart();
        if (tail.StartsWith("true", StringComparison.Ordinal))
            return true;
        if (tail.StartsWith("false", StringComparison.Ordinal))
            return false;
        return null;
    }

    private static string ExtractString(string json, string key)
    {
        var idx = json.IndexOf("\"" + key + "\":", StringComparison.Ordinal);
        if (idx < 0)
            return "";
        // The first quote after the colon starts the value.
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
}
