// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.IO.Compression;
using Content.Shared._Pirate.Administration.GameSnapshot;
using Robust.Client.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Content.Client._Pirate.Administration.GameSnapshot;

internal static class GameSnapshotPixels
{
    public static GameSnapshotFrame Encode(Image<Rgb24> source)
    {
        var scale = Math.Min((double) GameSnapshotFrame.MaxWidth / source.Width,
            (double) GameSnapshotFrame.MaxHeight / source.Height);
        if (scale >= 1)
            return Pack(source);

        var width = Math.Max(1, (int) Math.Round(source.Width * scale));
        var height = Math.Max(1, (int) Math.Round(source.Height * scale));
        using var resized = source.Clone(context => context.Resize(width, height));
        return Pack(resized);
    }

    private static GameSnapshotFrame Pack(Image<Rgb24> image)
    {
        // RGB565 keeps sharp text edges without chroma subsampling. Deflate handles flat UI and sprite colors.
        var source = image.GetPixelSpan();
        var pixels = new byte[source.Length * 2];
        for (var i = 0; i < source.Length; i++)
        {
            var color = source[i];
            var packed = ((color.R & 0xF8) << 8) | ((color.G & 0xFC) << 3) | (color.B >> 3);
            pixels[i * 2] = (byte) (packed >> 8);
            pixels[i * 2 + 1] = (byte) packed;
        }

        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.Fastest, true))
            deflate.Write(pixels, 0, pixels.Length);

        return new GameSnapshotFrame((ushort) image.Width, (ushort) image.Height, output.ToArray());
    }

    public static Image<Rgba32> Decode(GameSnapshotFrame frame)
    {
        if (!GameSnapshotFrame.ValidSize(frame.Width, frame.Height, frame.Pixels.Length))
            throw new InvalidDataException();

        var pixels = new byte[frame.Width * frame.Height * 2];
        using var input = new MemoryStream(frame.Pixels, false);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        deflate.ReadExactly(pixels, 0, pixels.Length);
        if (deflate.ReadByte() != -1)
            throw new InvalidDataException();

        var image = new Image<Rgba32>(frame.Width, frame.Height);
        var target = image.GetPixelSpan();
        for (var i = 0; i < target.Length; i++)
        {
            var packed = (pixels[i * 2] << 8) | pixels[i * 2 + 1];
            var r = packed >> 11;
            var g = (packed >> 5) & 63;
            var b = packed & 31;
            target[i] = new Rgba32((byte) ((r << 3) | (r >> 2)), (byte) ((g << 2) | (g >> 4)),
                (byte) ((b << 3) | (b >> 2)));
        }

        return image;
    }
}
