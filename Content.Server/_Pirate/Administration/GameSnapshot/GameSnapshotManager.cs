// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared._Pirate.Administration.GameSnapshot;
using Content.Shared.Administration;
using Content.Shared.Database;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.Administration.GameSnapshot;

public sealed class GameSnapshotManager
{
    [Dependency] private readonly IAdminManager _admins = default!;
    [Dependency] private readonly IAdminLogManager _logs = default!;
    [Dependency] private readonly EuiManager _euis = default!;
    [Dependency] private readonly IServerNetManager _net = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Dictionary<ICommonSession, GameSnapshotEui> _views = new();
    private readonly Dictionary<ICommonSession, TimeSpan> _nextRequest = new();
    private readonly Dictionary<INetChannel, PendingCapture> _pending = new();
    private uint _nextId;

    public void Initialize()
    {
        _net.RegisterNetMessage<MsgRequestGameSnapshot>(accept: NetMessageAccept.Client);
        _net.RegisterNetMessage<MsgGameSnapshotReply>(OnReply, NetMessageAccept.Server);
        _admins.OnPermsChanged += OnPermsChanged;
        _players.PlayerStatusChanged += OnStatusChanged;
    }

    public void Open(ICommonSession admin, ICommonSession target)
    {
        if (!_admins.HasAdminFlag(admin, AdminFlags.Admin))
            return;

        if (_views.TryGetValue(admin, out var previous))
            previous.Close();

        var view = new GameSnapshotEui(this, target);
        _views.Add(admin, view);
        _euis.OpenEui(view, admin);
        Request(view);
    }

    internal void Request(GameSnapshotEui view)
    {
        var admin = view.Player;
        var target = view.Target;
        if (!_admins.HasAdminFlag(admin, AdminFlags.Admin))
        {
            view.Close();
            return;
        }

        if (target.Status != SessionStatus.InGame)
        {
            view.Show(GameSnapshotStatus.Offline);
            return;
        }

        if (_pending.TryGetValue(target.Channel, out var existing) && existing.View == view)
            return;

        var now = _timing.RealTime;
        if (existing != null || now < _nextRequest.GetValueOrDefault(admin) || now < _nextRequest.GetValueOrDefault(target))
        {
            view.Show(GameSnapshotStatus.Busy);
            return;
        }

        var id = ++_nextId;
        _nextRequest[admin] = _nextRequest[target] = now + MsgRequestGameSnapshot.Cooldown;
        _pending.Add(target.Channel, new PendingCapture(id, now + MsgRequestGameSnapshot.Timeout, view));
        view.Show(GameSnapshotStatus.Waiting);
        _logs.Add(LogType.AdminCommands, LogImpact.High, $"{admin.Name} requested a game screenshot from {target.Name}.");
        _net.ServerSendMessage(new MsgRequestGameSnapshot { RequestId = id }, target.Channel);
        var channel = target.Channel;
        Timer.Spawn(MsgRequestGameSnapshot.Timeout, () =>
        {
            if (_pending.TryGetValue(channel, out var pending) && pending.Id == id)
            {
                _pending.Remove(channel);
                pending.View.Show(GameSnapshotStatus.TimedOut);
            }
        });
    }

    private void OnReply(MsgGameSnapshotReply reply)
    {
        var channel = reply.MsgChannel;
        if (!_pending.TryGetValue(channel, out var pending) || pending.Id != reply.RequestId
            || pending.WaitingForAdmin || pending.NextOffset != reply.Offset)
            return;

        var view = pending.View;
        if (!_admins.HasAdminFlag(view.Player, AdminFlags.Admin))
        {
            view.Close();
            return;
        }

        var expired = _timing.RealTime >= pending.Deadline;
        if (expired || reply.Pixels.IsEmpty || (reply.Offset != 0
                && (reply.Width != pending.Width || reply.Height != pending.Height || reply.TotalBytes != pending.TotalBytes)))
        {
            _pending.Remove(channel);
            view.Show(expired ? GameSnapshotStatus.TimedOut : GameSnapshotStatus.Unavailable);
            return;
        }

        pending.Width = reply.Width;
        pending.Height = reply.Height;
        pending.TotalBytes = reply.TotalBytes;
        pending.NextOffset += reply.Pixels.Length;
        if (pending.NextOffset == pending.TotalBytes)
            _pending.Remove(channel);
        else
            pending.WaitingForAdmin = true;

        view.Show(GameSnapshotStatus.Received, reply);
    }

    internal void Continue(GameSnapshotEui view, ContinueGameSnapshot message)
    {
        var channel = view.Target.Channel;
        if (!_pending.TryGetValue(channel, out var pending) || pending.View != view || !pending.WaitingForAdmin
            || pending.Id != message.RequestId || pending.NextOffset != message.Offset
            || _timing.RealTime >= pending.Deadline || !_admins.HasAdminFlag(view.Player, AdminFlags.Admin))
            return;

        // Both network legs must deliver this chunk before the player sends another one.
        pending.WaitingForAdmin = false;
        _net.ServerSendMessage(new MsgRequestGameSnapshot { RequestId = pending.Id, Offset = pending.NextOffset }, channel);
    }

    internal void Closed(GameSnapshotEui view)
    {
        _views.Remove(view.Player);
        var channel = view.Target.Channel;
        if (_pending.TryGetValue(channel, out var pending) && pending.View == view)
            _pending.Remove(channel);
    }

    private void OnPermsChanged(AdminPermsChangedEventArgs args)
    {
        if (_views.TryGetValue(args.Player, out var view) && !_admins.HasAdminFlag(args.Player, AdminFlags.Admin))
            view.Close();
    }

    private void OnStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.Disconnected)
            return;

        _nextRequest.Remove(args.Session);
        _pending.Remove(args.Session.Channel);
        foreach (var view in _views.Values)
        {
            if (view.Target == args.Session)
                view.Show(GameSnapshotStatus.Offline);
        }
    }

    private sealed record PendingCapture(uint Id, TimeSpan Deadline, GameSnapshotEui View)
    {
        public bool WaitingForAdmin;
        public ushort Width;
        public ushort Height;
        public int TotalBytes;
        public int NextOffset;
    }
}
