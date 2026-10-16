// SPDX-License-Identifier: AGPL-3.0-or-later

using static Content.Shared._Pirate.CustomMarkings.CustomMarkingRules;

namespace Content.Shared._Pirate.CustomMarkings;

/// <summary>
/// What each pixel of a marking belongs to on one body. A marking is only drawn on the body, on what else the body
/// shows, such as a tail or ears, and on a small margin around them. A body part that is lost takes its pixels
/// along, so every pixel in reach has one owner: what it lies on, or for the margin the nearest of them.
/// </summary>
public static class CustomMarkingSections
{
    /// <summary>How far past the body's outline a marking may reach, in pixels.</summary>
    public const int Margin = 2;

    /// <summary>A pixel out of reach: no body part there or near enough.</summary>
    public const byte None = 0;

    /// <summary>Size of a map: one byte for each pixel of each facing.</summary>
    public const int Size = Facings * FrameSize * FrameSize;

    /// <summary>Steps to the pixels within <see cref="Margin"/> of a pixel, nearest first.</summary>
    private static readonly (int X, int Y)[] Nearby = BuildNearby();

    /// <summary>Where a pixel of a facing sits in a map.</summary>
    public static int Index(int facing, int x, int y)
    {
        return (facing * FrameSize + y) * FrameSize + x;
    }

    /// <summary>
    /// Builds the map for a body. Each byte is the number of the owning part, or <see cref="None"/>. Parts are
    /// numbered from 1: the limbs in the order given, then the extras.
    /// </summary>
    /// <param name="limbs">How many body parts there are, lowest drawn first.</param>
    /// <param name="underArt">
    /// How many of them, counted from the first, are drawn under the marking. Where a part under it and a part over
    /// it overlap, the pixel is the lower part's: that is what the marking is seen on once the upper part is gone.
    /// </param>
    /// <param name="extras">
    /// How many other things the body shows that a marking may lie on, such as a tail or ears, lowest drawn first.
    /// They only own what no limb does: a pixel on the body belongs to the body, whatever else is drawn there.
    /// </param>
    /// <param name="opaque">Whether a limb or an extra draws something at a pixel of a facing: its number less one, facing, x, y.</param>
    public static byte[] Build(int limbs, int underArt, int extras, Func<int, int, int, int, bool> opaque)
    {
        var body = new byte[Size];
        for (var facing = 0; facing < Facings; facing++)
        {
            for (var y = 0; y < FrameSize; y++)
            {
                for (var x = 0; x < FrameSize; x++)
                {
                    byte under = None;
                    byte over = None;
                    for (var part = 0; part < limbs; part++)
                    {
                        if (!opaque(part, facing, x, y))
                            continue;

                        if (part < underArt)
                            under = (byte) (part + 1);
                        else
                            over = (byte) (part + 1);
                    }

                    var owner = under != None ? under : over;
                    for (var part = limbs; owner == None && part < limbs + extras; part++)
                    {
                        if (opaque(part, facing, x, y))
                            owner = (byte) (part + 1);
                    }

                    body[Index(facing, x, y)] = owner;
                }
            }
        }

        // The margin: each pixel off the body goes to the part of the nearest body pixel in reach.
        var map = body.AsSpan().ToArray();
        for (var facing = 0; facing < Facings; facing++)
        {
            for (var y = 0; y < FrameSize; y++)
            {
                for (var x = 0; x < FrameSize; x++)
                {
                    var at = Index(facing, x, y);
                    if (body[at] != None)
                        continue;

                    foreach (var step in Nearby)
                    {
                        var (nearX, nearY) = (x + step.X, y + step.Y);
                        if (nearX < 0 || nearY < 0 || nearX >= FrameSize || nearY >= FrameSize)
                            continue;

                        var owner = body[Index(facing, nearX, nearY)];
                        if (owner == None)
                            continue;

                        map[at] = owner;
                        break;
                    }
                }
            }
        }

        return map;
    }

    private static (int X, int Y)[] BuildNearby()
    {
        var steps = new List<(int X, int Y)>();
        for (var distance = 1; distance <= Margin * Margin; distance++)
        {
            for (var y = -Margin; y <= Margin; y++)
            {
                for (var x = -Margin; x <= Margin; x++)
                {
                    if (x * x + y * y == distance)
                        steps.Add((x, y));
                }
            }
        }

        return steps.ToArray();
    }
}
