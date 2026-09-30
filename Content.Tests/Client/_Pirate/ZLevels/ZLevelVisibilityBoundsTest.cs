// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Numerics;
using Content.Client.Viewport;
using NUnit.Framework;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Tests.Client._Pirate.ZLevels;

[TestFixture]
public sealed class ZLevelVisibilityBoundsTest
{
    [TestCase(0f)]
    [TestCase(0.7f)]
    [TestCase(-1.5f)]
    public void RegionalProjectionMatchesTheRenderTarget(float rotation)
    {
        var eye = new Eye
        {
            Position = new MapCoordinates(new Vector2(120, -73), new MapId(1)),
            Offset = new Vector2(0.3f, -0.9f),
            Rotation = new Angle(rotation),
            Scale = new Vector2(1.2f, 0.8f),
        };
        var size = new Vector2i(1310, 777);
        var scale = new Vector2(0.75f, 0.9f);
        var combined = ScalingViewport.GetZScreenToWorld(eye, size, scale);
        eye.GetViewMatrixInv(out var inverse, scale);
        foreach (var pixel in new[] { Vector2.Zero, new Vector2(1309, 776), new Vector2(364, 213) })
        {
            var expected = Vector2.Transform((pixel - size / 2f) * new Vector2(1, -1) / EyeManager.PixelsPerMeter, inverse);
            Assert.That(Vector2.Distance(Vector2.Transform(pixel, combined), expected), Is.LessThan(0.0001f));
        }
    }

    [Test]
    public void RotatedViewportIncludesTheOtherTwoCorners()
    {
        // Transforming only the diagonal (0,0)-(10,10) loses almost the entire X extent.
        Vector2[] corners = [new(0, 0), new(10, 0), new(0, 10), new(10, 10)];
        Assert.That(ScalingViewport.TryGetZVisibilityTileBounds(corners,
            Matrix3x2.CreateRotation(MathF.PI / 4), 1, out var start, out var end), Is.True);
        Assert.That(start.X, Is.LessThanOrEqualTo(-8));
        Assert.That(end.X, Is.GreaterThanOrEqualTo(8));
        Assert.That(start.Y, Is.LessThanOrEqualTo(-1));
        Assert.That(end.Y, Is.GreaterThanOrEqualTo(15));
    }

    [Test]
    public void GridTranslationAndTileSizeAreAppliedBeforePadding()
    {
        Vector2[] corners = [new(12, -6), new(20, -6), new(12, 2), new(20, 2)];
        Assert.That(ScalingViewport.TryGetZVisibilityTileBounds(corners,
            Matrix3x2.CreateTranslation(-10, 8), 2, out var start, out var end), Is.True);
        Assert.That(start, Is.EqualTo(new Vector2i(0, 0)));
        Assert.That(end, Is.EqualTo(new Vector2i(6, 6)));
    }

    [Test]
    public void ExtremeZoomAndInvalidGeometryKeepLowerDecks()
    {
        Vector2[] corners = [new(0, 0), new(1000, 0), new(0, 1000), new(1000, 1000)];
        Assert.That(ScalingViewport.TryGetZVisibilityTileBounds(corners,
            Matrix3x2.Identity, 1, out _, out _), Is.False);
        corners[0] = new Vector2(float.NaN, 0);
        Assert.That(ScalingViewport.TryGetZVisibilityTileBounds(corners,
            Matrix3x2.Identity, 1, out _, out _), Is.False);
        Assert.That(ScalingViewport.TryGetZVisibilityTileBounds(corners,
            Matrix3x2.Identity, 0, out _, out _), Is.False);
    }
}
