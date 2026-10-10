// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static Content.Shared._Pirate.CustomMarkings.CustomMarkingRules;

namespace Content.Shared._Pirate.CustomMarkings;

/// <summary>
/// Masks over a body: a bit for each pixel of each facing, <see cref="EraseBytes"/> bytes in all. A marking's
/// <see cref="CustomMarkingArt.Erase"/> is one, holding the pixels of the body it hides.
/// </summary>
public static class CustomMarkingErase
{
    /// <summary>
    /// The least share of a body's pixels, in each facing, that erasing has to leave in sight, in percent. Without
    /// it a marking could make its wearer invisible.
    /// </summary>
    public const int MinKeptPercent = 50;

    private const int FacingBytes = EraseBytes / Facings;

    private static int Bit(int facing, int x, int y)
    {
        return (facing * FrameSize + y) * FrameSize + x;
    }

    public static bool Get(ReadOnlySpan<byte> mask, int facing, int x, int y)
    {
        var bit = Bit(facing, x, y);
        return (mask[bit >> 3] & 1 << (bit & 7)) != 0;
    }

    /// <summary>Sets or clears one pixel. Returns whether it changed.</summary>
    public static bool Set(Span<byte> mask, int facing, int x, int y, bool on)
    {
        var bit = Bit(facing, x, y);
        var flag = 1 << (bit & 7);
        var before = mask[bit >> 3];
        var after = (byte) (on ? before | flag : before & ~flag);
        mask[bit >> 3] = after;
        return before != after;
    }

    /// <summary>Whether a mask holds any pixel.</summary>
    public static bool Any(ReadOnlySpan<byte> mask)
    {
        foreach (var value in mask)
        {
            if (value != 0)
                return true;
        }

        return false;
    }

    /// <summary>Whether a mask holds any pixel of one facing.</summary>
    public static bool Any(ReadOnlySpan<byte> mask, int facing)
    {
        return Any(mask.Slice(facing * FacingBytes, FacingBytes));
    }

    /// <summary>Adds every pixel of one mask to another.</summary>
    public static void Add(Span<byte> to, ReadOnlySpan<byte> mask)
    {
        for (var i = 0; i < to.Length && i < mask.Length; i++)
        {
            to[i] |= mask[i];
        }
    }

    /// <summary>Whether two masks share a pixel.</summary>
    public static bool Overlaps(ReadOnlySpan<byte> mask, ReadOnlySpan<byte> other)
    {
        for (var i = 0; i < mask.Length && i < other.Length; i++)
        {
            if ((mask[i] & other[i]) != 0)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Whether erasing leaves enough of a body in sight: in each facing, <see cref="MinKeptPercent"/> of the body's
    /// pixels are either left alone or drawn over.
    /// </summary>
    /// <param name="body">The body's own pixels.</param>
    /// <param name="erase">The pixels erased.</param>
    /// <param name="covered">The pixels a marking draws over solidly, in every frame.</param>
    public static bool LeavesEnough(ReadOnlySpan<byte> body, ReadOnlySpan<byte> erase, ReadOnlySpan<byte> covered)
    {
        for (var facing = 0; facing < Facings; facing++)
        {
            var total = 0;
            var kept = 0;
            for (var i = facing * FacingBytes; i < (facing + 1) * FacingBytes; i++)
            {
                total += Count(body[i]);
                kept += Count(body[i] & (~erase[i] | covered[i]));
            }

            if (kept * 100 < total * MinKeptPercent)
                return false;
        }

        return true;
    }

    /// <summary>A short name for exactly this mask, for what the client makes from it.</summary>
    public static string Fingerprint(ReadOnlySpan<byte> mask)
    {
        // FNV-1a.
        var hash = 14695981039346656037UL;
        foreach (var value in mask)
        {
            hash = unchecked((hash ^ value) * 1099511628211UL);
        }

        return hash.ToString("x16");
    }

    /// <summary>
    /// A mask as a PNG sheet that an RSI state of four facings can be loaded from, for sprites of a given size:
    /// each facing's mask sits in the middle of its cell, as a body sits in the middle of a larger sprite. Masked
    /// pixels are opaque, the rest clear.
    /// </summary>
    public static byte[] ToPng(ReadOnlySpan<byte> mask, int width, int height)
    {
        using var image = new Image<Rgba32>(width * 2, height * 2);
        var left = (width - FrameSize) / 2;
        var top = (height - FrameSize) / 2;
        for (var facing = 0; facing < Facings; facing++)
        {
            for (var y = 0; y < FrameSize; y++)
            {
                for (var x = 0; x < FrameSize; x++)
                {
                    var (cellX, cellY) = (left + x, top + y);
                    if (cellX < 0 || cellY < 0 || cellX >= width || cellY >= height || !Get(mask, facing, x, y))
                        continue;

                    image[facing % 2 * width + cellX, facing / 2 * height + cellY] = new Rgba32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
                }
            }
        }

        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    private static int Count(int bits)
    {
        var count = 0;
        for (bits &= byte.MaxValue; bits != 0; bits >>= 1)
        {
            count += bits & 1;
        }

        return count;
    }
}
