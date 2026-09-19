// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using Content.Pirate.Shared.TV;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Network;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>One playlist entry as seen by the client.</summary>
public sealed class WebTvQueueEntry
{
    public string Url = "";
    public WebTvChannel.WebTvKind Kind = WebTvChannel.WebTvKind.None;
    public string Label = "";
    public string Title = "";
}

/// <summary>
///     Window-facing façade for TV state. Every television has its own
///     entry (keyed by NetEntity); a mirror's entry carries the master's
///     playback state plus a <see cref="Source"/>. Windows read here and
///     send controls through <see cref="Send"/>; the server owns the truth
///     and answers with <see cref="PirateTvStateEvent"/> pushes.
/// </summary>
public static class PirateTvClientState
{
    public sealed class Entry
    {
        public string Url = "";
        public WebTvChannel.WebTvKind Kind = WebTvChannel.WebTvKind.None;
        public string Label = "";
        public bool Playing;
        public double Pos;
        public long Stamp;
        public bool Locked;
        public IReadOnlyList<WebTvQueueEntry> Queue = Array.Empty<WebTvQueueEntry>();
        public int QueueNow = -1;
        public NetEntity Source = NetEntity.Invalid;

        public bool IsMirror => Source.IsValid();
    }

    private static readonly Dictionary<NetEntity, Entry> _entries = new();
    private static readonly Dictionary<NetEntity, long> _requested = new();

    /// <summary>Current state of a TV (empty entry until the server replies).</summary>
    public static Entry Get(NetEntity tv)
        => _entries.TryGetValue(tv, out var e) ? e : new Entry();

    /// <summary>Removes a TV's state (e.g. it was deleted).</summary>
    public static void Forget(NetEntity tv)
    {
        _entries.Remove(tv);
        _requested.Remove(tv);
    }

    /// <summary>
    ///     Asks the server for a TV's state when it has no fresh entry yet
    ///     (window opens / reconnects). Throttled to twice a second per TV.
    /// </summary>
    public static void Request(NetEntity tv)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (_requested.TryGetValue(tv, out var last) && now - last < 500)
            return;
        _requested[tv] = now;

        if (IoCManager.Resolve<INetManager>() is not { IsConnected: true })
            return;
        IoCManager.Resolve<IEntityNetworkManager>()
            .SendSystemNetworkMessage(new PirateTvRequestEvent { Tv = tv });
    }

    public static void OnState(PirateTvStateEvent msg)
    {
        var list = new List<WebTvQueueEntry>();
        foreach (var it in msg.Items)
            list.Add(new WebTvQueueEntry
            {
                Url = it.Url,
                Kind = (WebTvChannel.WebTvKind)it.Kind,
                Label = it.Label,
                Title = it.Title,
            });

        _entries[msg.Tv] = new Entry
        {
            Url = msg.Url,
            Kind = (WebTvChannel.WebTvKind)msg.Kind,
            Label = msg.Label,
            Playing = msg.Playing,
            Pos = msg.Pos,
            Stamp = msg.Stamp,
            Locked = msg.Locked,
            Queue = list,
            QueueNow = msg.Now,
            Source = msg.Source,
        };
    }

    /// <summary>Sends a TV control event (resolving the local entity).</summary>
    public static void Send(EntityEventArgs ev)
    {
        if (IoCManager.Resolve<INetManager>() is not { IsConnected: true })
            return;
        IoCManager.Resolve<IEntityNetworkManager>().SendSystemNetworkMessage(ev);
    }

    /// <summary>NetEntity handle for a local TV entity (Invalid if unknown).</summary>
    public static NetEntity Net(EntityUid uid)
    {
        try
        {
            return IoCManager.Resolve<IEntityManager>().GetNetEntity(uid);
        }
        catch
        {
            return NetEntity.Invalid;
        }
    }

    /// <summary>The room clock position at wall time `stampMs` for an entry.</summary>
    public static double VideoPos(Entry e, long stampMs)
    {
        var elapsed = e.Playing
            ? Math.Max(0, (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - stampMs) / 1000.0)
            : 0;
        return Math.Max(0, e.Pos + elapsed);
    }

    /// <summary>True when the client is connected to a server (calls are safe).</summary>
    public static bool Connected
        => IoCManager.Resolve<INetManager>() is { IsConnected: true };

    /// <summary>True when the local player is attached (calls are safe).</summary>
    public static bool LocalPlayerReady
    {
        get
        {
            try
            {
                return IoCManager.Resolve<IPlayerManager>().LocalEntity != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
