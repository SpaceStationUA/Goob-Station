// SPDX-License-Identifier: AGPL-3.0-or-later

using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client._Pirate.CustomMarkings.UI;

/// <summary>Colour maths for body pixels read back from a render target.</summary>
public static class CustomMarkingSampling
{
    /// <summary>
    /// The colour a sampled pixel was drawn in. A pixel read back from a render target is premultiplied, its colour
    /// scaled by its alpha, and stored as sRGB, so the division happens in linear light as the blending did. False
    /// where nothing was drawn.
    /// </summary>
    public static bool TryStraightColor(Rgba32 sample, out Color color)
    {
        color = default;
        if (sample.A == 0)
            return false;

        if (sample.A == byte.MaxValue)
        {
            color = new Color(sample.R, sample.G, sample.B);
            return true;
        }

        var alpha = sample.A / (float) byte.MaxValue;
        var linear = Color.FromSrgb(new Color(sample.R, sample.G, sample.B));
        var straight = Color.ToSrgb(new Color(
            Math.Min(1f, linear.R / alpha),
            Math.Min(1f, linear.G / alpha),
            Math.Min(1f, linear.B / alpha)));
        color = new Color(Channel(straight.R), Channel(straight.G), Channel(straight.B));
        return true;
    }

    /// <summary>What a render target holds after a colour is drawn over nothing at an opacity: the inverse of <see cref="TryStraightColor"/>.</summary>
    public static Rgba32 Premultiply(Color color, float alpha)
    {
        var linear = Color.FromSrgb(color.WithAlpha(1f));
        var stored = Color.ToSrgb(new Color(linear.R * alpha, linear.G * alpha, linear.B * alpha));
        return new Rgba32(Channel(stored.R), Channel(stored.G), Channel(stored.B), Channel(alpha));
    }

    /// <summary>The nearest byte to a channel value; the colour's own byte properties cut off instead.</summary>
    private static byte Channel(float value)
    {
        return (byte) Math.Clamp(MathF.Round(value * byte.MaxValue), byte.MinValue, byte.MaxValue);
    }
}
