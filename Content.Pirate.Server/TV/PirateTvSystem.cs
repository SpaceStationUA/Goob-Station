// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.TV;
using Content.Server.Administration.Managers;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Pirate.Server.TV;

/// <summary>
///     TV-2: the room clock and playlist live here, on the server.
///     Clients send picks, queue adds/navs, remote controls and the
///     video-"ended" ping; this system owns the state, mutates it,
///     broadcasts <see cref="PirateTvStateEvent"/> and pops a world
///     "who did what" feed at the actor's feet.
///     When the room is locked, only transport controls (play/pause/
///     seek/next-on-end) pass; every picker/jumper/lock-toggle is
///     rejected.
/// </summary>
public sealed class PirateTvSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IAdminManager _admin = default!;

    private string _url = "";
    private int _kind;
    private string _label = "";
    private bool _playing;
    private double _pos;
    private long _stamp;
    private bool _locked;

    private readonly List<PirateTvQueueItem> _queue = new();
    private int _now = -1;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PirateTvPickEvent>(OnPick);
        SubscribeNetworkEvent<PirateTvCommandEvent>(OnCommand);
        SubscribeNetworkEvent<PirateTvQueueAddEvent>(OnQueueAdd);
        SubscribeNetworkEvent<PirateTvQueueNavEvent>(OnQueueNav);
        SubscribeNetworkEvent<PirateTvQueueRemoveEvent>(OnQueueRemove);
        SubscribeNetworkEvent<PirateTvQueueMoveEvent>(OnQueueMove);
        SubscribeNetworkEvent<PirateTvLockEvent>(OnLockToggle);
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    // ===== client → server =====

    private void OnPick(PirateTvPickEvent msg, EntitySessionEventArgs args)
    {
        if (_locked && !IsAdmin(args))
        {
            Feed(args, "ТБ заблоковано — канал не міняється");
            return;
        }

        foreach (var item in _queue)
            if (item.Url == msg.Url)
            {
                NavTo(_queue.IndexOf(item));
                return;
            }

        var it = new PirateTvQueueItem { Url = msg.Url, Kind = msg.Kind, Label = msg.Label, Title = msg.Title };
        _queue.Add(it);
        NavTo(_queue.Count - 1);
        Feed(args, "поставив на ТБ: " + msg.Label);
    }

    private void OnQueueAdd(PirateTvQueueAddEvent msg, EntitySessionEventArgs args)
    {
        if (_locked && !IsAdmin(args))
        {
            Feed(args, "ТБ заблоковано — чергу не змінити");
            return;
        }

        foreach (var item in _queue)
            if (item.Url == msg.Url)
                return;

        var it = new PirateTvQueueItem { Url = msg.Url, Kind = msg.Kind, Label = msg.Label, Title = msg.Title };
        _queue.Add(it);
        if (_url.Length == 0)
        {
            // Empty room: the add becomes "play now".
            NavTo(_queue.Count - 1);
            return;
        }

        BroadcastState();
        Feed(args, "додали в чергу: " + msg.Label);
    }

    private void OnQueueNav(PirateTvQueueNavEvent msg, EntitySessionEventArgs args)
    {
        if (_locked && !IsAdmin(args))
        {
            Feed(args, "ТБ заблоковано — черга зафіксована");
            return;
        }
        if (msg.Index < 0 || msg.Index >= _queue.Count)
            return;

        NavTo(msg.Index);
        Feed(args, "переключив на елемент " + (msg.Index + 1));
    }

    private void OnQueueRemove(PirateTvQueueRemoveEvent msg, EntitySessionEventArgs args)
    {
        if (_locked && !IsAdmin(args))
        {
            Feed(args, "ТБ заблоковано — чергу не змінити");
            return;
        }
        if (msg.Index < 0 || msg.Index >= _queue.Count)
            return;

        var it = _queue[msg.Index];
        var wasNow = msg.Index == _now;
        _queue.RemoveAt(msg.Index);

        if (_queue.Count == 0)
        {
            _now = -1;
            _url = "";
            _label = "";
            _playing = false;
            BroadcastState();
            Feed(args, "очистив чергу");
            return;
        }

        if (wasNow)
        {
            NavTo(Math.Min(msg.Index, _queue.Count - 1), silent: true);
            Feed(args, "прибрав " + it.Label + " — грає наступний");
            return;
        }

        if (_now > msg.Index)
            _now--;
        BroadcastState();
        Feed(args, "прибрав " + it.Label);
    }

    private void OnQueueMove(PirateTvQueueMoveEvent msg, EntitySessionEventArgs args)
    {
        if (_locked && !IsAdmin(args))
        {
            Feed(args, "ТБ заблоковано — чергу не змінити");
            return;
        }
        if (msg.Index < 0 || msg.Index >= _queue.Count)
            return;

        var j = msg.Index + Math.Sign(msg.Delta); // Delta -1 (▲) = move up
        if (j < 0 || j >= _queue.Count)
            return;

        var it = _queue[msg.Index];
        _queue[msg.Index] = _queue[j];
        _queue[j] = it;

        if (_now == msg.Index)
            _now = j;
        else if (_now == j)
            _now = msg.Index;

        BroadcastState();
    }

    private void OnLockToggle(PirateTvLockEvent msg, EntitySessionEventArgs args)
    {
        _locked = msg.Locked;
        BroadcastState();
        Feed(args, _locked ? "заблокував ТБ" : "розблокував ТБ");
    }

    private void OnCommand(PirateTvCommandEvent msg, EntitySessionEventArgs args)
    {
        switch (msg.Op)
        {
            case "pause":
                // The presser's own video position becomes the room anchor.
                _playing = false;
                _pos = Math.Max(0, msg.Arg);
                break;
            case "play":
                _playing = true;
                _stamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                break;
            case "seekTo":
                _pos = Math.Max(0, msg.Arg);
                _stamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                break;
            case "ended":
                // Auto-advance: the playlist's next entry starts playing.
                NavTo(_now + 1 < _queue.Count ? _now + 1 : 0, silent: true);
                return;
            case "manual_next":
                if (_locked && !IsAdmin(args))
                {
                    Feed(args, "ТБ заблоковано — наступний відео недоступний");
                    return;
                }
                NavTo(_now + 1 < _queue.Count ? _now + 1 : 0, silent: true);
                Feed(args, "наступний відео");
                return;
            case "set_title":
                // The page reported its own video title; stamp the current
                // playlist entry so every queue row shows the real name.
                if (msg.Title.Length == 0 || _now < 0 || _now >= _queue.Count)
                    return;
                var t = msg.Title[..Math.Min(msg.Title.Length, 100)];
                var cur = _queue[_now];
                if (cur.Title == t)
                    return;
                cur.Title = t;
                BroadcastState();
                return;
            case "mute":
                return; // local-only control; no room impact
            default:
                return;
        }

        BroadcastState();
        Feed(args, msg.Op switch
        {
            "pause" => "ставить на паузу",
            "play" => "вмикає",
            "seekTo" => "перематує",
            _ => msg.Op,
        });
    }

    // ===== core =====

    private bool IsAdmin(EntitySessionEventArgs args)
    {
        var ent = args.SenderSession.AttachedEntity;
        return ent != null && _admin.IsAdmin(ent.Value);
    }

    /// <summary>Starts playing the queue[i] entry (wraps to 0 past the end).</summary>
    private void NavTo(int index, bool silent = false)
    {
        if (_queue.Count == 0)
        {
            _now = -1;
            _url = "";
            BroadcastState();
            return;
        }

        _now = index < 0 ? _queue.Count - 1 : index % _queue.Count;
        var cur = _queue[_now];
        _url = cur.Url;
        _kind = cur.Kind;
        _label = cur.Label;
        _pos = 0;
        _playing = true;
        _stamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        BroadcastState();
        if (!silent)
            return; // feed already popped by callers
    }

    private void BroadcastState()
    {
        RaiseNetworkEvent(new PirateTvStateEvent
        {
            Url = _url,
            Kind = _kind,
            Label = _label,
            Playing = _playing,
            Pos = _pos,
            Stamp = _stamp,
            Locked = _locked,
            Now = _now,
            Items = _queue,
        });
    }

    /// <summary>World popup feed: "who did what" at the actor's feet.</summary>
    private void Feed(EntitySessionEventArgs args, string action)
    {
        var ent = args.SenderSession.AttachedEntity;
        if (ent == null)
            return;
        _popup.PopupEntity(action, ent.Value, Filter.Pvs(ent.Value), false, PopupType.Medium);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus != SessionStatus.InGame)
            return;
        RaiseNetworkEvent(new PirateTvStateEvent
        {
            Url = _url,
            Kind = _kind,
            Label = _label,
            Playing = _playing,
            Pos = _pos,
            Stamp = _stamp,
            Locked = _locked,
            Now = _now,
            Items = _queue,
        }, e.Session.Channel);
    }
}
