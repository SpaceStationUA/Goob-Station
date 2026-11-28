// Pirate: diary drawings - compact text encoding of a painting so it can be stored
// next to the persistent text of a diary between rounds.
// NOTE: content assemblies are sandboxed, so this deliberately avoids System.IO types
// that are not whitelisted for the client (BinaryWriter, etc.).
using System;
using System.Collections.Generic;

namespace Content.Goobstation.Shared.Paint;

public static class PaintImageCodec
{
    private const int FormatVersion = 1;
    private const int MaxDimension = 512;

    public static string Encode(PaintImage image)
    {
        var palette = image.Palette;
        var buffer = new byte[16 + palette.Count * 4 + 4 + image.Pixels.Length];
        var offset = 0;

        WriteInt(buffer, ref offset, FormatVersion);
        WriteInt(buffer, ref offset, image.Width);
        WriteInt(buffer, ref offset, image.Height);
        WriteInt(buffer, ref offset, palette.Count);

        foreach (var color in palette)
        {
            buffer[offset++] = color.RByte;
            buffer[offset++] = color.GByte;
            buffer[offset++] = color.BByte;
            buffer[offset++] = color.AByte;
        }

        WriteInt(buffer, ref offset, image.Pixels.Length);
        Array.Copy(image.Pixels, 0, buffer, offset, image.Pixels.Length);

        return Convert.ToBase64String(buffer);
    }

    /// <summary>
    /// Decodes a painting previously produced by <see cref="Encode"/>. Returns null for
    /// corrupt data or a drawing that no longer matches the surface it belongs to.
    /// </summary>
    public static PaintImage? Decode(string text, Vector2i size)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            var buffer = Convert.FromBase64String(text);
            var offset = 0;

            if (!TryReadInt(buffer, ref offset, out var version) || version != FormatVersion)
                return null;

            if (!TryReadInt(buffer, ref offset, out var width)
                || !TryReadInt(buffer, ref offset, out var height)
                || width <= 0 || height <= 0 || width > MaxDimension || height > MaxDimension)
                return null;

            if (!TryReadInt(buffer, ref offset, out var paletteCount)
                || paletteCount <= 0 || paletteCount > PaintImage.MaxPaletteSize
                || offset + paletteCount * 4 > buffer.Length)
                return null;

            var palette = new List<Color>(paletteCount);
            for (var i = 0; i < paletteCount; i++)
            {
                palette.Add(new Color(
                    buffer[offset++],
                    buffer[offset++],
                    buffer[offset++],
                    buffer[offset++]));
            }

            if (!TryReadInt(buffer, ref offset, out var length)
                || length != width * height
                || offset + length > buffer.Length)
                return null;

            var pixels = new byte[length];
            Array.Copy(buffer, offset, pixels, 0, length);

            var image = new PaintImage(width, height, palette, pixels);
            return image.Size == size ? image : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void WriteInt(byte[] buffer, ref int offset, int value)
    {
        BitConverter.GetBytes(value).CopyTo(buffer, offset);
        offset += sizeof(int);
    }

    private static bool TryReadInt(byte[] buffer, ref int offset, out int value)
    {
        value = 0;
        if (offset < 0 || offset + sizeof(int) > buffer.Length)
            return false;

        value = BitConverter.ToInt32(buffer, offset);
        offset += sizeof(int);
        return true;
    }
}
