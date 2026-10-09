// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using Content.Shared._Pirate.CustomMarkings;
using Robust.Client.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
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
        if (png.Length > MaxFileBytes)
            return null;

        try
        {
            // Pirate: reject oversized decoded images before allocating their pixel buffers.
            using var stream = new MemoryStream(png, false);
            var options = new DecoderOptions { MaxFrames = 1 };
            var info = Image.Identify(options, stream);
            if (info == null)
                return null;

            var single = info.Width == CustomMarkingRules.FrameSize && info.Height == CustomMarkingRules.FrameSize;
            var sheet = info.Width == CustomMarkingRules.SheetSize
                        && info.Height > 0
                        && info.Height % CustomMarkingRules.SheetSize == 0
                        && info.Height / CustomMarkingRules.SheetSize <= CustomMarkingRules.MaxFrames;
            if (!single && !sheet)
                return null;

            stream.Position = 0;
            using var image = Image.Load<Rgba32>(options, stream);
            return CustomMarkingArt.FromImage(image.Width, image.Height, image.GetPixelSpan());
        }
        catch (Exception)
        {
            // Not an image ImageSharp can read.
            return null;
        }
    }
}
