// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client._Pirate.Singularity;
using NUnit.Framework;

namespace Content.Tests.Client._Pirate.Singularity;

[TestFixture]
public sealed class SingularityDistortionTest
{
    [TestCase(0f)]
    [TestCase(float.Epsilon)]
    [TestCase(0.000001f)]
    public void CursorAtOrNearToyCenterStaysFinite(float distance)
    {
        // The round 10754 toy had intensity 2000 and falloff 2.6 before container scaling.
        var delta = new Vector2(distance, 0f);
        var offset = SingularityDistortionMath.GetOffset(delta, Vector2.One, 2000f, 2.6f, 640f);
        Assert.That(SingularityDistortionMath.IsFinite(delta - offset), Is.True);
        if (distance == 0f)
            Assert.That(offset, Is.EqualTo(Vector2.Zero));
    }

    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(float.NegativeInfinity)]
    public void InvalidSourceCannotEnterShaderOrAffectCursor(float invalid)
    {
        Assert.That(SingularityDistortionMath.IsValidSource(new Vector2(invalid, 0f), 25f, 2.6f), Is.False);
        Assert.That(SingularityDistortionMath.IsValidSource(Vector2.Zero, invalid, 2.6f), Is.False);
        Assert.That(SingularityDistortionMath.IsValidSource(Vector2.Zero, 25f, invalid), Is.False);
        Assert.That(SingularityDistortionMath.GetOffset(new Vector2(invalid, 0f), Vector2.One, 25f, 2.6f, 640f),
            Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void DisabledSingularityAtCenterDoesNotDivideZeroByZero()
    {
        Assert.That(SingularityDistortionMath.GetOffset(Vector2.Zero, Vector2.One, 0f, 9999f, 640f),
            Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void DistortionStillPullsTowardCenterAndStopsAtRange()
    {
        var delta = new Vector2(32f, 0f);
        var offset = SingularityDistortionMath.GetOffset(delta, Vector2.One, 25f, 2.6f, 640f);
        Assert.That(offset.X, Is.GreaterThan(0f).And.LessThan(delta.X));
        Assert.That(offset.Y, Is.Zero);
        Assert.That(SingularityDistortionMath.GetOffset(new Vector2(640f, 0f), Vector2.One, 25f, 2.6f, 640f),
            Is.EqualTo(Vector2.Zero));
    }
}
