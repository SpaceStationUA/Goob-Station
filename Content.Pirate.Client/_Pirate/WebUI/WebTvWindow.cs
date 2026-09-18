// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Globalization;
using Content.Pirate.Shared.TV;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.WebView;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     Pirate TV viewer: shows whatever channel the room picked
///     (YouTube embed or Twitch page) inside its own browser area with a
///     native remote (pause/play/±10s/mute). Each viewer keeps their own
///     session of the site; the room's shared clock lives in
///     <see cref="WebTvBackend"/> and every window follows it.
/// </summary>
public sealed class WebTvWindow : DefaultWindow, IDisposable
{
    public static readonly WebTvBackend Backend = new();

    private readonly WebViewControl _web;
    private readonly WebUiTuiIpc _ipc;
    private readonly WebUiTvDriver _driver;

    private readonly Label _status = new()
    {
        Text = "канал: ...",
        FontColorOverride = Color.LightGray,
    };

    private readonly Button _pause = new() { Text = "⏸ Пауза" };
    private readonly Button _play = new() { Text = "▶ Грати" };
    private readonly Button _back10 = new() { Text = "-10с" };
    private readonly Button _back60 = new() { Text = "-60с" };
    private readonly Button _fwd10 = new() { Text = "+10с" };
    private readonly Button _fwd60 = new() { Text = "+60с" };
    private readonly Button _mute = new() { Text = "🔇 Звук" };
    private readonly Button _next = new() { Text = "⏭ Далі" };

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

    // Room truth we already acted on.
    private string _shownUrl = "";
    private long _followedStamp = -1;

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
            MinWidth = 265,
            MaxWidth = 275,
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
        _mute.OnPressed += _ => Owner_Send("mute", _ourMuted ? 0 : 1);
        _next.OnPressed += OnNextPressed;
        _panelToggle.OnPressed += OnPanelToggle;
        _fwd60.OnPressed += _ => Owner_Send("seek", 60);

        // Same drain as the picker: AlwaysActive browsers survive window
        // closes, so drop the browser here to stop background audio.
        OnClose += () => { try { _web.AlwaysActive = false; } catch { } };

        Backend.Subscribe(OnBackendBroadcast);
    }

    /// <summary>Opens centered and shows whatever the room currently watches.</summary>
    public void OpenCenteredTv()
    {
        OpenCentered();
        var s = Backend.Snapshot();
        if (s.Kind != WebTvChannel.WebTvKind.None && s.Url.Length > 0)
            NavigateTo(s);
        else
            _status.Text = "канал: (нічого не грає — відкрий Браузер)";
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _ipc.Pump();
        _driver.Tick(args.DeltaSeconds);
        FollowRoomClock();
        RangeCheck();
    }

    /// <summary>Walk away? The window closes (TV view stays at the TV).</summary>
    private void RangeCheck()
    {
        if (TvUid == null || !TvUid.Value.IsValid())
            return;
        try
        {
            var ent = Robust.Shared.IoC.IoCManager.Resolve<Robust.Shared.GameObjects.IEntityManager>();
            var player = Robust.Shared.IoC.IoCManager
                .Resolve<Robust.Client.Player.IPlayerManager>().LocalEntity;
            if (player == null)
                return;
            if (!ent.TryGetComponent(player.Value, out Robust.Shared.GameObjects.TransformComponent? pt) ||
                !ent.TryGetComponent(TvUid.Value, out Robust.Shared.GameObjects.TransformComponent? tt))
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
        if (t != null)
            _ourPos = Math.Max(0, t.Value);
        if (dur != null)
            _ourDur = Math.Max(0, dur.Value);
        if (playing != null)
            _ourPlaying = playing.Value;
        if (muted != null)
            _ourMuted = muted.Value;

        // Finished = the element says so, or we're within half a second of
        // the end (they behave identically: the room rolls to the next).
        var finished = ended == true ||
                       (_ourDur > 30 && _ourPos >= _ourDur - 0.5 && _ourPos > 5);
        if (finished && !_endedPublished)
        {
            // Playlist auto-advance: the video finished, tell the room.
            _endedPublished = true;
            var net = Robust.Shared.IoC.IoCManager
                .Resolve<Robust.Shared.GameObjects.IEntityNetworkManager>();
            net.SendSystemNetworkMessage(new PirateTvCommandEvent { Op = "ended", Arg = 0 });
        }
        else if (!finished && _endedPublished)
        {
            _endedPublished = false;
        }

        // The page knows its own title; stamp the room entry with it once.
        var title = ExtractString(json, "title");
        if (title != null && title.Length > 0 && title != _titleSent)
        {
            var s = Backend.Snapshot();
            if (s.Kind != WebTvChannel.WebTvKind.None && s.Url.Length > 0)
            {
                _titleSent = title;
                var net = Robust.Shared.IoC.IoCManager
                    .Resolve<Robust.Shared.GameObjects.IEntityNetworkManager>();
                net.SendSystemNetworkMessage(new PirateTvCommandEvent
                {
                    Op = "set_title",
                    Title = title.Length > 100 ? title[..100] : title,
                });
            }
        }

        var mm = (int)(_ourPos / 60);
        var ss = (int)(_ourPos % 60);
        _status.Text = $"{StateLabel()}  {mm:00}:{ss:00}  " + (_ourPlaying ? "▶" : "⏸") + (_ourMuted ? " 🔇" : "");
    }

    private string _titleSent = "";

    private bool _endedPublished;

    private string StateLabel()
    {
        var s = Backend.Snapshot();
        return s.Kind != WebTvChannel.WebTvKind.None && s.Label.Length > 0 ? s.Label : "канал";
    }

    // ===== room-clock follow (shared target = Pos + wall time while playing) =====

    private void FollowRoomClock()
    {
        var s = Backend.Snapshot();
        if (s.Kind == WebTvChannel.WebTvKind.None || s.Url.Length == 0)
            return;

        var target = VideoPos(s.Stamp);
        if (Math.Abs(_ourPos - target) > 5.0)
            _driver.ApplyCommand("{\"k\":\"control\",\"op\":\"seekTo\",\"arg\":" +
                                 target.ToString("0.##", CultureInfo.InvariantCulture) + "}");
        if (_ourPlaying != s.Playing)
            _driver.ApplyCommand("{\"k\":\"control\",\"op\":\"" + (s.Playing ? "play" : "pause") + "\"}");
    }

    /// <summary>The room's expected video position at wall-clock `stampMs`.</summary>
    public static double VideoPos(long stampMs)
    {
        var s = Backend.Snapshot();
        var elapsed = s.Playing ? Math.Max(0, (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - stampMs) / 1000.0) : 0;
        return Math.Max(0, s.Pos + elapsed);
    }

    // ===== broadcasts: URL switch + control state =====

    private void OnBackendBroadcast(WebTvBackend.ChannelState s)
    {
        if (_disposed)
            return;

        RefreshQueue(s);

        if (s.Kind == WebTvChannel.WebTvKind.None || s.Url.Length == 0)
        {
            _followedStamp = -1;
            return;
        }

        if (s.Url != _shownUrl)
        {
            NavigateTo(s);
            return;
        }

        if (s.Stamp == _followedStamp)
            return;
        _followedStamp = s.Stamp;

        // Someone moved the room clock (pause/play/seek): apply immediately.
        var target = VideoPos(s.Stamp);
        if (Math.Abs(_ourPos - target) > 2.0)
            _driver.ApplyCommand("{\"k\":\"control\",\"op\":\"seekTo\",\"arg\":" +
                                 target.ToString("0.##", CultureInfo.InvariantCulture) + "}");
        if (_ourPlaying != s.Playing)
            _driver.ApplyCommand("{\"k\":\"control\",\"op\":\"" + (s.Playing ? "play" : "pause") + "\"}");
    }

    /// <summary>Queue rows: click jumps the room to that position.</summary>
    private void RefreshQueue(WebTvBackend.ChannelState s)
    {
        _queueBox.RemoveAllChildren();
        var idx = 0;
        foreach (var item in s.Queue)
        {
            var i = idx++;
            _queueBox.AddChild(BuildQueueRow(i, item.Title.Length > 0 ? item.Title : item.Label,
                i == s.QueueNow, s.Locked));
        }
    }

    /// <summary>
    ///     One playlist entry: the (widely readable, clipped) row button on
    ///     top and the small ▲▼✖ row beneath — the panel is only ~200px
    ///     wide, so both would fight for space on one line. Locked rooms
    ///     get the whole row disabled; the playing entry turns green.
    /// </summary>
    public static Control BuildQueueRow(int i, string title, bool nowPlaying, bool locked)
    {
        Control BuildRow()
        {
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

            // Wrap the action row in an outer click proxy → toggle nav box.
            nav.OnPressed += _ => QueueSend(new PirateTvQueueNavEvent { Index = i });
            return nav;
        }

        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 1 };
        box.AddChild(BuildRow());

        var tools = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 2,
            Margin = new Thickness(0, 0, 0, 2),
        };
        tools.AddChild(MiniBtn("▲", () => QueueSend(new PirateTvQueueMoveEvent { Index = i, Delta = -1 }), locked));
        tools.AddChild(MiniBtn("▼", () => QueueSend(new PirateTvQueueMoveEvent { Index = i, Delta = 1 }), locked));
        tools.AddChild(MiniBtn("✖", () => QueueSend(new PirateTvQueueRemoveEvent { Index = i }), locked));
        box.AddChild(tools);
        return box;
    }

    public static Button MiniBtn(string label, Action act, bool disabled = false)
    {
        var b = new Button { Text = label, MinWidth = 30, Disabled = disabled };
        b.OnPressed += _ => act();
        return b;
    }

    public static void QueueSend(object ev)
    {
        var net = Robust.Shared.IoC.IoCManager.Resolve<Robust.Shared.GameObjects.IEntityNetworkManager>();
        switch (ev)
        {
            case PirateTvQueueMoveEvent m:
                net.SendSystemNetworkMessage(m);
                break;
            case PirateTvQueueRemoveEvent r:
                net.SendSystemNetworkMessage(r);
                break;
            case PirateTvQueueNavEvent n:
                net.SendSystemNetworkMessage(n);
                break;
        }
    }

    private void OnNextPressed(BaseButton.ButtonEventArgs _)
    {
        var net = Robust.Shared.IoC.IoCManager.Resolve<Robust.Shared.GameObjects.IEntityNetworkManager>();
        net.SendSystemNetworkMessage(new PirateTvCommandEvent { Op = "manual_next", Arg = 0 });
    }

    private void OnPanelToggle(BaseButton.ButtonEventArgs _)
    {
        if (_panelBox == null)
            return;
        _panelBox.Visible = !_panelBox.Visible;
        _panelToggle.Text = _panelBox.Visible ? "»" : "«";
    }

    private void NavigateTo(WebTvBackend.ChannelState s)
    {
        _shownUrl = s.Url;
        _followedStamp = -1;
        _ourPos = -1000;
        _ourDur = 0;
        _endedPublished = false;
        try { _web.Url = s.Url; } catch { /* headless dev */ }
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

        // TV-2: the room clock is server-side; publish the intent, the
        // authoritative broadcast lands (and corrects us) in its echo.
        var net = Robust.Shared.IoC.IoCManager
            .Resolve<Robust.Shared.GameObjects.IEntityNetworkManager>();
        switch (op)
        {
            case "pause":
                net.SendSystemNetworkMessage(new PirateTvCommandEvent
                {
                    Op = "pause",
                    Arg = _ourPos < 0 ? 0 : _ourPos,
                });
                break;
            case "play":
                net.SendSystemNetworkMessage(new PirateTvCommandEvent { Op = "play", Arg = 0 });
                break;
            case "seek":
                {
                    var basePos = _ourPos < 0 ? 0 : _ourPos;
                    var target = Math.Max(0, basePos + arg);

                    // A forward jump that crosses the end rolls the room to
                    // the next playlist entry — same outcome as the video
                    // playing to its natural end.
                    if (arg > 0 && _ourDur > 30 && target >= _ourDur - 0.5)
                    {
                        net.SendSystemNetworkMessage(new PirateTvCommandEvent
                        {
                            Op = "manual_next",
                            Arg = 0,
                        });
                        return;
                    }

                    net.SendSystemNetworkMessage(new PirateTvCommandEvent
                    {
                        Op = "seekTo",
                        Arg = target,
                    });
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
