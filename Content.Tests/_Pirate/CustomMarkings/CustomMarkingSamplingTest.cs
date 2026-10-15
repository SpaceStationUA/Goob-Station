// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Content.Client._Pirate.CustomMarkings.UI;
using NUnit.Framework;
using Robust.Shared.Maths;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Tests._Pirate.CustomMarkings;

/// <summary>How the colour picker reads body pixels copied back from a render target.</summary>
[TestFixture]
[TestOf(typeof(CustomMarkingSampling))]
public sealed class CustomMarkingSamplingTest
{
    [Test]
    public void OpaquePixelIsItsOwnColourTest()
    {
        Assert.That(CustomMarkingSampling.TryStraightColor(new Rgba32(200, 120, 40, 255), out var color), Is.True);
        Assert.That(((int) color.RByte, (int) color.GByte, (int) color.BByte), Is.EqualTo((200, 120, 40)), "an opaque pixel is taken as it is");
        Assert.That(color.A, Is.EqualTo(1f));
    }

    [Test]
    public void NothingDrawnPicksNothingTest()
    {
        Assert.That(CustomMarkingSampling.TryStraightColor(new Rgba32(0, 0, 0, 0), out _), Is.False);
        Assert.That(CustomMarkingSampling.TryStraightColor(new Rgba32(90, 90, 90, 0), out _), Is.False, "colour without alpha is nothing");
    }

    [TestCase(0.9f)]
    [TestCase(0.5f)]
    [TestCase(0.25f)]
    public void PartlyCoveredPixelGivesBackItsColourTest(float alpha)
    {
        var drawn = new Color(180, 90, 30);
        var sample = CustomMarkingSampling.Premultiply(drawn, alpha);
        Assert.That(sample.R, Is.LessThan(drawn.RByte), "the stored pixel is darkened by its alpha");
        Assert.That(sample.A, Is.EqualTo((byte) MathF.Round(alpha * 255)));

        Assert.That(CustomMarkingSampling.TryStraightColor(sample, out var color), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(color.RByte, Is.EqualTo(drawn.RByte).Within(3));
            Assert.That(color.GByte, Is.EqualTo(drawn.GByte).Within(3));
            Assert.That(color.BByte, Is.EqualTo(drawn.BByte).Within(3));
            Assert.That(color.A, Is.EqualTo(1f), "a body colour is picked at full opacity");
        });
    }

    [Test]
    public void BrighterThanItsAlphaStaysInRangeTest()
    {
        Assert.That(CustomMarkingSampling.TryStraightColor(new Rgba32(255, 255, 255, 10), out var color), Is.True);
        Assert.That(((int) color.RByte, (int) color.GByte, (int) color.BByte), Is.EqualTo((255, 255, 255)));
    }
}
