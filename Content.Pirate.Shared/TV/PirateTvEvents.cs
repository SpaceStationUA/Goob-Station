// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.TV;

/// <summary>One entry of the TV's playlist.</summary>
[Serializable, NetSerializable]
public sealed class PirateTvQueueItem
{
    public string Url = "";
    public int Kind;
    public string Label = "";
    public string Title = "";
}

/// <summary>
///     Server → clients: the room's shared playback clock + playlist.
///     Kind mirrors Content.Pirate.Client WebTvChannel.WebTvKind as an int
///     (0 None, 1 YouTube, 2 Twitch). Stamp anchors the wall-clock
///     (time of last change); Now is the playlist entry currently played.
/// </summary>
[Serializable, NetSerializable]
public sealed class PirateTvStateEvent : EntityEventArgs
{
    public string Url = "";
    public int Kind;
    public string Label = "";
    public bool Playing;
    public double Pos;
    public long Stamp;
    public bool Locked;
    public int Now;
    public List<PirateTvQueueItem> Items = new();
}

/// <summary>Client → server: remote control (play/pause/seekTo/ended/set_title/manual_next).
/// For pause, Arg carries the presser's video position (room anchor).
/// "ended" advances the playlist automatically even while locked;
/// "manual_next" is the button path (rejected while locked).
/// set_title stamps the playing entry with the page's own video title.</summary>
[Serializable, NetSerializable]
public sealed class PirateTvCommandEvent : EntityEventArgs
{
    public string Op = "";
    public double Arg;
    public string Title = "";
}

/// <summary>Client → server: someone picked what the room watches. Admin
/// flag bypasses the room lock (server verifies the sender is admin).</summary>
[Serializable, NetSerializable]
public sealed class PirateTvPickEvent : EntityEventArgs
{
    public string Url = "";
    public int Kind;
    public string Label = "";
    public string Title = "";
    public bool Admin;
}

/// <summary>Client → server: add an entry to the playlist.</summary>
[Serializable, NetSerializable]
public sealed class PirateTvQueueAddEvent : EntityEventArgs
{
    public string Url = "";
    public int Kind;
    public string Label = "";
    public string Title = "";
    public bool Admin;
}

/// <summary>Client → server: jump to a playlist position.</summary>
[Serializable, NetSerializable]
public sealed class PirateTvQueueNavEvent : EntityEventArgs
{
    public int Index;
    public bool Admin;
}

/// <summary>Client → server: remove a playlist entry (index).</summary>
[Serializable, NetSerializable]
public sealed class PirateTvQueueRemoveEvent : EntityEventArgs
{
    public int Index;
}

/// <summary>Client → server: move a playlist entry by Delta (-1 up, +1 down).</summary>
[Serializable, NetSerializable]
public sealed class PirateTvQueueMoveEvent : EntityEventArgs
{
    public int Index;
    public int Delta;
}

/// <summary>Client → server: lock/unlock the room's control.</summary>
[Serializable, NetSerializable]
public sealed class PirateTvLockEvent : EntityEventArgs
{
    public bool Locked;
}
