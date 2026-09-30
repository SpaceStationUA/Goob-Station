// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later


namespace Content.Pirate.Shared.TV;

/// <summary>
///     Per-television playback state. Server-authoritative: a TV that is not
///     a mirror owns its own channel/queue/clock/lock; a mirror keeps a copy
///     of its master's state plus a <see cref="Source"/> link and forwards
///     every control back to the master (star topology, set up in-game with
///     the multitool/network configurator via DeviceLink).
///
///     Nothing here is <c>[DataField]</c> and that is deliberate. This is live
///     runtime state: no prototype sets any of it from YAML, and clients are
///     synced by <see cref="PirateTvStateEvent"/> rather than component state.
///     Marking it as DataField made the component claim to be YAML-round-
///     trippable when it is not — <see cref="Source"/> is a NetEntity and
///     <see cref="Queue"/> holds PirateTvQueueItem, neither of which has a
///     DataDefinition, so prototype save/load threw "No data definition found"
///     (the Queue only slipped through because it is always empty).
/// </summary>
[RegisterComponent]
public sealed partial class PirateTvComponent : Component
{
    /// <summary>Playback URL playing now ("" = nothing).</summary>
    public string Url = "";

    /// <summary>Content kind of <see cref="Url"/> (mirrors WebTvChannel.WebTvKind).</summary>
    public int Kind;

    /// <summary>Human label ("YouTube", ...).</summary>
    public string Label = "";

    public bool Playing;

    /// <summary>Room mute state (synced; the remote toggles it for everyone).</summary>
    public bool Muted;

    public double Pos;

    /// <summary>Wall-clock anchor of the last state change (ms since epoch).</summary>
    public long Stamp;

    /// <summary>Room locked: only transport controls pass.</summary>
    public bool Locked;

    /// <summary>Playlist (entries in order) and the index playing now.</summary>
    public List<PirateTvQueueItem> Queue = new();

    public int Now = -1;

    /// <summary>When valid, this TV mirrors the source TV and owns no state itself.</summary>
    public NetEntity Source = NetEntity.Invalid;

    /// <summary>True when this TV follows another one.</summary>
    public bool IsMirror => Source.IsValid();
}
