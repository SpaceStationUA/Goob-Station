// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later


namespace Content.Pirate.Shared.TV;

/// <summary>
///     Per-television playback state. Server-authoritative: a TV that is not
///     a mirror owns its own channel/queue/clock/lock; a mirror keeps a copy
///     of its master's state plus a <see cref="Source"/> link and forwards
///     every control back to the master (star topology, set up in-game with
///     the multitool/network configurator via DeviceLink).
/// </summary>
[RegisterComponent]
public sealed partial class PirateTvComponent : Component
{
    /// <summary>Playback URL playing now ("" = nothing).</summary>
    [DataField]
    public string Url = "";

    /// <summary>Content kind of <see cref="Url"/> (mirrors WebTvChannel.WebTvKind).</summary>
    [DataField]
    public int Kind;

    /// <summary>Human label ("YouTube", ...).</summary>
    [DataField]
    public string Label = "";

    [DataField]
    public bool Playing;

    [DataField]
    public double Pos;

    /// <summary>Wall-clock anchor of the last state change (ms since epoch).</summary>
    [DataField]
    public long Stamp;

    /// <summary>Room locked: only transport controls pass.</summary>
    [DataField]
    public bool Locked;

    /// <summary>Playlist (entries in order) and the index playing now.</summary>
    [DataField]
    public List<PirateTvQueueItem> Queue = new();

    [DataField]
    public int Now = -1;

    /// <summary>When valid, this TV mirrors the source TV and owns no state itself.</summary>
    [DataField]
    public NetEntity Source = NetEntity.Invalid;

    /// <summary>True when this TV follows another one.</summary>
    public bool IsMirror => Source.IsValid();
}
