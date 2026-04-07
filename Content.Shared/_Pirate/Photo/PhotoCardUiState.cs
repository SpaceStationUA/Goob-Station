// SPDX-FileCopyrightText: 2026 Corvax Team Contributors
// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Photo;

[Serializable, NetSerializable]
public sealed class PhotoCardUiState(byte[]? imageData, string? customName, string? caption) : BoundUserInterfaceState
{
    /// <summary>
    /// Binary image data for the photo preview/full image.
    /// </summary>
    public byte[]? ImageData { get; } = imageData;

    /// <summary>
    /// Custom user-defined name for the image.
    /// </summary>
    public string? CustomName { get; } = customName;

    /// <summary>
    /// Caption text shown with the image.
    /// </summary>
    public string? Caption { get; } = caption;
}

[Serializable, NetSerializable]
/// <summary>
/// Key representing which <see cref="PlayerBoundUserInterface"/> is open for this entity.
/// A single-member enum is the standard BUI key convention and is future-proof if more UIs are added later.
/// </summary>
public enum PhotoCardUiKey : byte
{
    Key
}


