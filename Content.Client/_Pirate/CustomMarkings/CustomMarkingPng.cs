// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using Content.Shared._Pirate.CustomMarkings;
using Robust.Client.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client._Pirate.CustomMarkings;

/// <summary>Reads marking art from PNG: sheets the server sends, and files a player imports.</summary>
public static class CustomMarkingPng
{
    /// <summary>Largest file the importer will try to read.</summary>
    public const int MaxFileBytes = 256 * 1024;

    /// <summary>Art from a PNG of a whole sheet or of one facing; null for anything else.</summary>
    public static CustomMarkingArt? Read(byte[] png)
    {
        if (png.Length > MaxFileBytes || !HasValidHeader(png))
            return null;

        try
        {
            using var stream = new MemoryStream(png, false);
            using var image = Image.Load<Rgba32>(stream);
            return CustomMarkingArt.FromImage(image.Width, image.Height, image.GetPixelSpan());
        }
        catch (Exception)
        {
            // Not an image ImageSharp can read.
            return null;
        }
    }

    // Pirate: the pinned sandbox only permits Load(Stream), so inspect PNG dimensions before decoding.
    private static bool HasValidHeader(byte[] png)
    {
        // Pirate: direct comparisons avoid Span conversions rejected by the pinned IL verifier.
        if (png.Length < 33
            || png[0] != 137 || png[1] != 80 || png[2] != 78 || png[3] != 71
            || png[4] != 13 || png[5] != 10 || png[6] != 26 || png[7] != 10
            || ReadUInt32(png, 8) != 13
            || ReadUInt32(png, 12) != 0x49484452) // IHDR
            return false;

        var width = ReadUInt32(png, 16);
        var height = ReadUInt32(png, 20);
        var single = width == CustomMarkingRules.FrameSize && height == CustomMarkingRules.FrameSize;
        var sheet = width == CustomMarkingRules.SheetSize
                    && height > 0
                    && height % CustomMarkingRules.SheetSize == 0
                    && height / CustomMarkingRules.SheetSize <= CustomMarkingRules.MaxFrames;
        if (!single && !sheet)
            return false;

        for (var offset = 8; offset <= png.Length - 12;)
        {
            var length = ReadUInt32(png, offset);
            if (length > png.Length - offset - 12)
                return false;

            var type = ReadUInt32(png, offset + 4);
            // Animation is represented by sheet rows. Reject APNG to bound decoded frame allocations too.
            if (type == 0x6163544C) // acTL
                return false;

            if (type == 0x49454E44) // IEND
                return length == 0;

            offset += (int) length + 12;
        }

        return false;
    }

    private static uint ReadUInt32(byte[] png, int offset)
    {
        return (uint) png[offset] << 24 | (uint) png[offset + 1] << 16
               | (uint) png[offset + 2] << 8 | png[offset + 3];
    }
}
