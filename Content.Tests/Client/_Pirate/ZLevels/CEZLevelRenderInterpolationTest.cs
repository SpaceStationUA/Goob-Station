using Content.Client._Pirate.ZLevels.Core;
using NUnit.Framework;

namespace Content.Tests.Client._Pirate.ZLevels;

[TestFixture]
[TestOf(typeof(CEClientZLevelsSystem))]
public sealed class CEZLevelRenderInterpolationTest
{
    [Test]
    public void InterpolationMovesMonotonicallyTowardTarget()
    {
        var current = 0f;

        for (var frame = 0; frame < 20; frame++)
        {
            var next = CEClientZLevelsSystem.InterpolateRenderHeight(current, 1f, 1f / 60f, 0.05f, 0.12f);

            Assert.That(next, Is.GreaterThanOrEqualTo(current));
            Assert.That(next, Is.LessThanOrEqualTo(1f));
            current = next;
        }
    }

    [Test]
    public void InterpolationNeverTrailsBeyondLagCap()
    {
        const float target = 0.3f;
        const float maxLag = 0.12f;

        var height = CEClientZLevelsSystem.InterpolateRenderHeight(0f, target, 1f / 240f, 0.5f, maxLag);

        Assert.That(height, Is.GreaterThanOrEqualTo(target - maxLag));
    }

    [Test]
    public void InterpolationSettlesExactlyOnTarget()
    {
        var height = CEClientZLevelsSystem.InterpolateRenderHeight(0.9999f, 1f, 1f / 60f, 0.05f, 0.12f);

        Assert.That(height, Is.EqualTo(1f));
    }
}
