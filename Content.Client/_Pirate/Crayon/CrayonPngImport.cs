// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Linq;
using Content.Shared._Pirate.Crayon;
using Robust.Client.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client._Pirate.Crayon;

public static class CrayonPngImport
{
    public const int MaxBytes = 2 * 1024 * 1024;
    public const int MaxSide = 2048;

    public static CrayonSymbol? Read(Stream file, int size, IReadOnlyList<CrayonColor> crayons)
    {
        if (size is not (CrayonSymbol.Small or CrayonSymbol.Large) || crayons.Count == 0
            || !file.CanSeek || file.Length is < 33 or > MaxBytes)
            return null;

        var bytes = new byte[(int) file.Length];
        var read = 0;
        while (read < bytes.Length)
        {
            var count = file.Read(bytes, read, bytes.Length - read);
            if (count == 0)
                return null;
            read += count;
        }

        using var png = new MemoryStream();
        if (!CopyImageChunks(bytes, png))
            return null;

        png.Position = 0;
        using var image = Image.Load<Rgba32>(png);
        var scale = size / (double) Math.Max(image.Width, image.Height);
        var width = Math.Max(1, (int) Math.Round(image.Width * scale));
        var height = Math.Max(1, (int) Math.Round(image.Height * scale));
        using var resized = image.Clone(context => context.Resize(width, height));

        var shades = crayons.SelectMany(c => CrayonPaletteSystem.Shades(c.Color)).Select(s => s.Color).Distinct().ToList();
        var counts = new int[shades.Count];
        var pixels = resized.GetPixelSpan();
        for (var i = 0; i < pixels.Length; i++)
        {
            var pixel = pixels[i];
            if (pixel.A >= 128)
                counts[Nearest(pixel, shades)]++;
        }

        var symbol = new CrayonSymbol { Size = size, Pixels = new byte[size * size] };
        symbol.Palette = Enumerable.Range(0, shades.Count).Where(i => counts[i] > 0)
            .OrderByDescending(i => counts[i]).Take(CrayonSymbol.MaxColors).Select(i => shades[i]).ToList();
        if (symbol.Palette.Count == 0)
            return null;

        var left = (size - width) / 2;
        var top = (size - height) / 2;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = pixels[y * width + x];
                if (pixel.A >= 128)
                    symbol.Pixels[(y + top) * size + x + left] = (byte) (1 + Nearest(pixel, symbol.Palette));
            }
        }

        return symbol;
    }

    private static int Nearest(Rgba32 pixel, List<Color> colors)
    {
        var closest = 0;
        var best = int.MaxValue;
        for (var i = 0; i < colors.Count; i++)
        {
            var c = colors[i];
            var r = pixel.R - c.RByte;
            var g = pixel.G - c.GByte;
            var b = pixel.B - c.BByte;
            var distance = r * r + g * g + b * b;
            if (distance >= best)
                continue;

            closest = i;
            best = distance;
        }

        return closest;
    }

    // The client sandbox exposes only Load(Stream). Bound the decoded dimensions and pass the
    // decoder just the chunks needed for still pixels, including indexed colors and transparency.
    private static bool CopyImageChunks(byte[] bytes, Stream output)
    {
        if (UInt32(bytes, 0) != 0x89504E47 || UInt32(bytes, 4) != 0x0D0A1A0A
            || UInt32(bytes, 8) != 13 || UInt32(bytes, 12) != 0x49484452
            || UInt32(bytes, 16) is 0 or > MaxSide || UInt32(bytes, 20) is 0 or > MaxSide)
            return false;

        output.Write(bytes, 0, 33);
        var hasPixels = false;
        for (var offset = 33; offset <= bytes.Length - 12;)
        {
            var length = UInt32(bytes, offset);
            if (length > bytes.Length - offset - 12)
                return false;

            var count = (int) length + 12;
            switch (UInt32(bytes, offset + 4))
            {
                case 0x49444154: // IDAT
                    hasPixels = true;
                    output.Write(bytes, offset, count);
                    break;
                case 0x504C5445: // PLTE
                case 0x74524E53: // tRNS
                    if (hasPixels || length > 768)
                        return false;
                    output.Write(bytes, offset, count);
                    break;
                case 0x49454E44: // IEND
                    if (length != 0 || !hasPixels)
                        return false;
                    output.Write(bytes, offset, count);
                    return true;
                case 0x6163544C: // acTL
                case 0x6663544C: // fcTL
                case 0x66644154: // fdAT
                    return false;
                default:
                    if ((bytes[offset + 4] & 0x20) == 0) // Unknown critical chunk or a second IHDR.
                        return false;
                    break;
            }

            offset += count;
        }

        return false;
    }

    private static uint UInt32(byte[] bytes, int i) =>
        (uint) bytes[i] << 24 | (uint) bytes[i + 1] << 16 | (uint) bytes[i + 2] << 8 | bytes[i + 3];
}
