// SPDX-FileCopyrightText: 2026 Corvax Team Contributors
// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using System;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.RoundEnd;

[Serializable, NetSerializable]
public sealed class PhotoAlbumEvent : EntityEventArgs
{
    public List<AlbumData>? Albums { get; }

    public PhotoAlbumEvent(List<AlbumData>? albums)
    {
        Albums = albums;
    }
}

[Serializable, NetSerializable]
public struct AlbumData
{
    public List<AlbumImageData> Images;

    public string? AuthorCkey;
    public string? AuthorName;

    public AlbumData(List<AlbumImageData> images, string? authorCkey, string? authorName)
    {
        this.Images = images;
        this.AuthorCkey = authorCkey;
        this.AuthorName = authorName;
    }
}

[Serializable, NetSerializable]
public struct AlbumImageData
{
    public Guid ImageId;
    public byte[]? PreviewData;
    public string? CustomName;

    public AlbumImageData(Guid imageId, byte[]? previewData, string? customName)
    {
        ImageId = imageId;
        PreviewData = previewData;
        CustomName = customName;
    }
}

[Serializable, NetSerializable]
public sealed class PhotoAlbumImageRequestEvent : EntityEventArgs
{
    public Guid ImageId { get; }

    public PhotoAlbumImageRequestEvent(Guid imageId)
    {
        ImageId = imageId;
    }
}

[Serializable, NetSerializable]
public sealed class PhotoAlbumImageResponseEvent : EntityEventArgs
{
    public Guid ImageId { get; }
    public byte[]? ImageData { get; }

    public PhotoAlbumImageResponseEvent(Guid imageId, byte[]? imageData)
    {
        ImageId = imageId;
        ImageData = imageData;
    }
}


