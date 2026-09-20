// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Radio;

/// <summary>
///     A single station in the catalog sent to the client. Kept deliberately
///     flat (plain fields, Ogg/Opus only) so it marshals cheaply.
/// </summary>
[Serializable, NetSerializable]
public sealed class PirateRadioStationEntry
{
    public string Id = "";
    public string Label = "";
    public string Genre = "";
    public string Url = "";
    public bool Featured;
}

/// <summary>
///     Client → server: "send me the station catalog" (on program open).
///     Marker is the installed radio program entity (the cartridge mirror).
/// </summary>
[Serializable, NetSerializable]
public sealed class PirateRadioCatalogRequestEvent : EntityEventArgs
{
    public NetEntity Marker;
}

/// <summary>
///     Server → client: the (cached) station catalog. Pinned curated
///     stations always come first; remote-discovered ones follow.
/// </summary>
[Serializable, NetSerializable]
public sealed class PirateRadioCatalogEvent : EntityEventArgs
{
    public NetEntity Marker;
    public List<PirateRadioStationEntry> Stations = new();
}

/// <summary>
///     Client → server: start/stop/reselect the stream on this program.
///     State is purely personal (PDA), so there is no world sync.
/// </summary>
[Serializable, NetSerializable]
public sealed class PirateRadioCommandEvent : EntityEventArgs
{
    public NetEntity Marker;
    public string Op = "";
    public string StationId = "";
}

/// <summary>
///     Server → client: the program's currently selected station. The page
///     resolves the station in its catalog; live radio needs no clock.
/// </summary>
[Serializable, NetSerializable]
public sealed class PirateRadioStateEvent : EntityEventArgs
{
    public NetEntity Marker;
    public string StationId = "";
    public bool Playing;
}
