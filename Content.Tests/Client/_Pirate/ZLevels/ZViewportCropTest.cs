// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Numerics;
using Content.Client._Pirate.ZLevels.Core;
using Content.Client.Viewport;
using NUnit.Framework;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Tests.Client._Pirate.ZLevels;

[TestFixture]
public sealed class ZViewportCropTest
{
    [Test]
    public void DistantHolesUseTheFullViewInsteadOfClippingOneAway()
    {
        UIBox2[] regions = [new(0, 0, 64, 64), new(960, 704, 1024, 768)];
        Assert.That(ZViewportCrop.TryGetCrop(regions, new Vector2i(1024, 768), 32, out _), Is.False);
    }

    [Test]
    public void CropContainsPaddedFractionalRegionsAtEveryScreenEdge()
    {
        var full = new Vector2i(1024, 768);
        foreach (var x in new[] { 0f, 50.3f, 400.7f, 1000f })
        foreach (var y in new[] { 0f, 75.2f, 380.6f, 730f })
        {
            var region = new UIBox2(x, y, Math.Min(x + 20, full.X), Math.Min(y + 20, full.Y));
            Assert.That(ZViewportCrop.TryGetCrop(new[] { region }, full, 31.7f, out var crop), Is.True);
            Assert.That(crop.Left, Is.LessThanOrEqualTo(Math.Max(0, region.Left - 31.7f)));
            Assert.That(crop.Top, Is.LessThanOrEqualTo(Math.Max(0, region.Top - 31.7f)));
            Assert.That(crop.Right, Is.GreaterThanOrEqualTo(Math.Min(full.X, region.Right + 31.7f)));
            Assert.That(crop.Bottom, Is.GreaterThanOrEqualTo(Math.Min(full.Y, region.Bottom + 31.7f)));
            Assert.That(crop.Left, Is.GreaterThanOrEqualTo(0));
            Assert.That(crop.Top, Is.GreaterThanOrEqualTo(0));
            Assert.That(crop.Right, Is.LessThanOrEqualTo(full.X));
            Assert.That(crop.Bottom, Is.LessThanOrEqualTo(full.Y));
            Assert.That(crop.Width / (float) crop.Height, Is.EqualTo(full.X / (float) full.Y).Within(0.01f));
        }
    }

    [Test]
    public void GrowingTheCachedTargetKeepsTheRequestedCropCovered()
    {
        var full = new Vector2i(1024, 768);
        foreach (var region in new[] { new UIBox2(0, 0, 64, 64), new UIBox2(960, 704, 1024, 768) })
        {
            Assert.That(ZViewportCrop.TryGetCrop(new[] { region }, full, 32, out var crop), Is.True);
            var grown = ZViewportCrop.CenterWithin(new Vector2(crop.Left + crop.Width / 2f,
                crop.Top + crop.Height / 2f), new Vector2i(768, 576), full);
            Assert.That(grown.Left, Is.LessThanOrEqualTo(crop.Left));
            Assert.That(grown.Top, Is.LessThanOrEqualTo(crop.Top));
            Assert.That(grown.Right, Is.GreaterThanOrEqualTo(crop.Right));
            Assert.That(grown.Bottom, Is.GreaterThanOrEqualTo(crop.Bottom));
        }
    }

    [TestCase(0f)]
    [TestCase(0.8f)]
    [TestCase(-1.5f)]
    public void CropPixelsProjectToTheSameWorldPointsAsTheFullView(float rotation)
    {
        var original = new ScalingViewport.ZEye(-3, -2, 0)
        {
            Position = new MapCoordinates(new Vector2(100, -57), new MapId(1)),
            Offset = new Vector2(0.3f, -0.9f),
            Rotation = new Angle(rotation),
            Scale = new Vector2(1.2f, 0.8f),
            DrawFov = false,
            DrawLight = true,
        };
        var full = new Vector2i(1310, 777);
        var scale = new Vector2(0.75f, 0.9f);
        var crop = new UIBox2i(73, 91, 730, 480);
        var cropped = ScalingViewport.CreateZCropEye(original, full, scale, crop);
        var fromFull = ScalingViewport.GetZScreenToWorld(original, full, scale);
        var fromCrop = ScalingViewport.GetZScreenToWorld(cropped, crop.Size, scale);
        foreach (var pixel in new[] { Vector2.Zero, new Vector2(crop.Width, crop.Height), new Vector2(100, 200) })
        {
            var expected = Vector2.Transform(pixel + new Vector2(crop.Left, crop.Top), fromFull);
            Assert.That(Vector2.Distance(Vector2.Transform(pixel, fromCrop), expected), Is.LessThan(0.0001f));
        }
        Assert.That(cropped.Position, Is.EqualTo(original.Position));
        Assert.That(cropped.Depth, Is.EqualTo(original.Depth));
        Assert.That(cropped.LowestDepth, Is.EqualTo(original.LowestDepth));
        Assert.That(cropped.HighestDepth, Is.EqualTo(original.HighestDepth));
        Assert.That(cropped.DrawFov, Is.EqualTo(original.DrawFov));
        Assert.That(cropped.DrawLight, Is.EqualTo(original.DrawLight));
    }

    [Test]
    public void InvalidInputsCannotProduceACrop()
    {
        Assert.That(ZViewportCrop.TryGetCrop(Array.Empty<UIBox2>(), new Vector2i(1024, 768), 0, out _), Is.False);
        UIBox2[] regions = [new(0, 0, 64, 64)];
        Assert.That(ZViewportCrop.TryGetCrop(regions, Vector2i.Zero, 0, out _), Is.False);
        Assert.That(ZViewportCrop.TryGetCrop(regions, new Vector2i(1024, 768), float.NaN, out _), Is.False);
        Assert.That(ZViewportCrop.TryGetCrop(regions, new Vector2i(1024, 768), -1, out _), Is.False);
    }
}
