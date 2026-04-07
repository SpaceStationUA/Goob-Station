// SPDX-FileCopyrightText: 2026 Corvax Team Contributors
// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using Content.Shared._Pirate.Photo;

namespace Content.Server._Pirate.Photo;

[RegisterComponent]
public sealed partial class PhotoCardComponent : Component
{
    [DataField]
    public byte[]? ImageData;

    [DataField]
    public byte[]? PreviewData;

    /// <summary>
    /// Optional custom title entered when the photo is printed.
    /// Displayed in the photo window header.
    /// </summary>
    [DataField]
    public string? CustomName;

    /// <summary>
    /// Optional custom description entered when the photo is printed.
    /// Used to extend the examine description text.
    /// </summary>
    [DataField]
    public string? CustomDescription;

    /// <summary>
    /// Optional caption entered when the photo is printed.
    /// Displayed in the caption area below the photo.
    /// </summary>
    [DataField]
    public string? Caption;

    /// <summary>
    /// Auto-generated capture description used as the base examine text.
    /// </summary>
    [DataField]
    public string? BaseDescription;

    /// <summary>
    /// Structured capture metadata for later persistence and feature use.
    /// </summary>
    public PhotoCaptureData? CaptureData;

    /// <summary>
    /// UTC timestamp when the photo was taken.
    /// </summary>
    public DateTime? CreatedAt;

    /// <summary>
    /// UTC timestamp when user-editable metadata was last changed.
    /// </summary>
    public DateTime? UpdatedAt;

    /// <summary>
    /// Entities captured in this photo at capture time.
    /// Runtime metadata only; not serialized for persistence.
    /// </summary>
    public List<EntityUid> MobsSeen = new();

    /// <summary>
    /// Subset of <see cref="MobsSeen"/> that were dead when captured.
    /// Runtime metadata only; not serialized for persistence.
    /// </summary>
    public List<EntityUid> DeadSeen = new();

    /// <summary>
    /// Identity names visible in the photo at capture time.
    /// Runtime metadata only; not serialized for persistence.
    /// </summary>
    public List<string> NamesSeen = new();

    /// <summary>
    /// True when this photo card was recreated from a persistent album snapshot.
    /// Runtime metadata only; not serialized for persistence.
    /// </summary>
    public bool IsArchivedAlbumPhoto;
}


