// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.CustomMarkings;

/// <summary>Client to server: send my library.</summary>
[Serializable, NetSerializable]
public sealed class CustomMarkingLibraryRequestEvent : EntityEventArgs;

/// <summary>Server to client: the player's whole library, after a request or any change to it.</summary>
[Serializable, NetSerializable]
public sealed class CustomMarkingLibraryEvent(List<CustomMarkingEntry> entries) : EntityEventArgs
{
    public readonly List<CustomMarkingEntry> Entries = entries;
}

/// <summary>
/// Client to server: add a marking to my library, or change the one with this id. <see cref="Pixels"/> is a raw
/// RGBA sheet for each frame, one after another; null keeps the entry's art.
/// </summary>
[Serializable, NetSerializable]
public sealed class CustomMarkingSaveEvent(
    int request,
    int id,
    string name,
    CustomMarkingPlacement placement,
    byte[]? pixels,
    int[]? frameTimes = null,
    byte[]? erase = null)
    : EntityEventArgs
{
    /// <summary>Echoed in the result, so the editor can tell which save it answers.</summary>
    public readonly int Request = request;

    /// <summary>The entry to change, or 0 for a new one.</summary>
    public readonly int Id = id;

    public readonly string Name = name;
    public readonly CustomMarkingPlacement Placement = placement;
    public readonly byte[]? Pixels = pixels;

    /// <summary>How long each frame shows, in milliseconds. Null for a still marking.</summary>
    public readonly int[]? FrameTimes = frameTimes;

    /// <summary>The body pixels the marking erases, as a <see cref="CustomMarkingErase"/> mask. Null for none.</summary>
    public readonly byte[]? Erase = erase;
}

/// <summary>Server to client: the saved entry, or a loc id saying why it wasn't saved.</summary>
[Serializable, NetSerializable]
public sealed class CustomMarkingSaveResultEvent(int request, CustomMarkingEntry? entry, string? previousHash, string? error)
    : EntityEventArgs
{
    public readonly int Request = request;
    public readonly CustomMarkingEntry? Entry = entry;

    /// <summary>The art the entry had before this save, when the save replaced it.</summary>
    public readonly string? PreviousHash = previousHash;

    public readonly string? Error = error;
}

/// <summary>Client to server: remove an entry from my library. Characters wearing its art keep it.</summary>
[Serializable, NetSerializable]
public sealed class CustomMarkingDeleteEvent(int id) : EntityEventArgs
{
    public readonly int Id = id;
}

/// <summary>Client to server: send the art for these hashes.</summary>
[Serializable, NetSerializable]
public sealed class CustomMarkingArtRequestEvent(List<string> hashes) : EntityEventArgs
{
    public readonly List<string> Hashes = hashes;
}

/// <summary>
/// Server to client: art as a PNG sheet with what goes with it, or a null sheet when the server has none it will
/// show for that hash.
/// </summary>
[Serializable, NetSerializable]
public sealed class CustomMarkingArtEvent(string hash, byte[]? png, int[]? frameTimes = null, byte[]? erase = null) : EntityEventArgs
{
    public readonly string Hash = hash;
    public readonly byte[]? Png = png;

    /// <summary>How long each frame shows, in milliseconds. Null for a still marking.</summary>
    public readonly int[]? FrameTimes = frameTimes;

    /// <summary>The body pixels the marking erases, as a <see cref="CustomMarkingErase"/> mask. Null for none.</summary>
    public readonly byte[]? Erase = erase;
}
