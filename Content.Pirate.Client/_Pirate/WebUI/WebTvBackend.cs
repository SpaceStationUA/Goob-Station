// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;

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
///     Shared state hub for the Pirate TV's room — the client mirror of
///     the server (TV-2). Every TV/picker window subscribes; the server's
///     authoritative broadcasts land through <see cref="Apply"/>,
///     local remote actions over the network (best-effort + echo).
/// </summary>
public sealed class WebTvBackend
{
    /// <summary>The picked channel plus shared playback state.</summary>
    public sealed class ChannelState
    {
        /// <summary>Playback URL playing now ("crossed-out" empty = nothing playing).</summary>
        public string Url = "";
        /// <summary>Content kind of <see cref="Url"/>.</summary>
        public WebTvChannel.WebTvKind Kind = WebTvChannel.WebTvKind.None;
        /// <summary>Human label ("YouTube", "Twitch VOD", ...).</summary>
        public string Label = "";
        public bool Playing;
        public double Pos;
        public long Stamp;
        /// <summary>Room locked: only transport controls pass.</summary>
        public bool Locked;
        /// <summary>Playlist (entries in order) and the index playing now.</summary>
        public IReadOnlyList<WebTvQueueEntry> Queue = Array.Empty<WebTvQueueEntry>();
        public int QueueNow = -1;
    }

    private ChannelState _state = new();

    private readonly List<EventReceiver> _receivers = new();

    /// <summary>Instant view of the current state (used as initial render).</summary>
    public ChannelState Snapshot() => _state;

    /// <summary>Registers an event receiver; returns an unsubscriber.</summary>
    public Action Subscribe(Action<ChannelState> receiver)
    {
        var r = new EventReceiver(receiver);
        _receivers.Add(r);
        return () => _receivers.Remove(r);
    }

    /// <summary>
    ///     Mutates the shared state and fans the copy out to every
    ///     subscriber (pickers, TVs, the server relay in TV-2).
    /// </summary>
    public void Apply(Action<ChannelState> mutate)
    {
        lock (this)
        {
            mutate(_state);
            var copy = _state;
            foreach (var r in _receivers.ToArray())
                r.Invoke(copy);
        }
    }

    private sealed class EventReceiver
    {
        private readonly Action<ChannelState> _receiver;
        public EventReceiver(Action<ChannelState> receiver) => _receiver = receiver;
        public void Invoke(ChannelState s) => _receiver(s);
    }
}
