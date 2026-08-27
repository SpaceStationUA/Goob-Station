using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using NUnit.Framework;

namespace Content.Tests.Shared._Pirate.ZLevels;

[TestFixture]
[TestOf(typeof(CESharedZLevelsSystem))]
public sealed class CEZPhysicsCadenceTest
{
    [TestCase(30f, 30f, 120u, 120)]
    [TestCase(15f, 30f, 120u, 60)]
    [TestCase(45f, 30f, 120u, 180)]
    [TestCase(30f, 60f, 120u, 60)]
    [TestCase(240f, 30f, 120u, 960)]
    public void StepCountMatchesRequestedCadence(
        float zPhysicsRate,
        float engineRate,
        uint ticks,
        int expectedSteps)
    {
        var steps = 0;
        for (uint tick = 0; tick < ticks; tick++)
        {
            steps += CESharedZLevelsSystem.GetZPhysicsStepsForTick(
                tick,
                zPhysicsRate,
                engineRate);
        }

        Assert.That(steps, Is.EqualTo(expectedSteps));
    }

    [Test]
    public void ReplayingTickProducesIdenticalStepCount()
    {
        const uint tick = 12345;

        var first = CESharedZLevelsSystem.GetZPhysicsStepsForTick(tick, 45f, 30f);
        var replay = CESharedZLevelsSystem.GetZPhysicsStepsForTick(tick, 45f, 30f);

        Assert.That(replay, Is.EqualTo(first));
    }

    [Test]
    public void PerTickWorkIsCapped()
    {
        var steps = CESharedZLevelsSystem.GetZPhysicsStepsForTick(10, 240f, 20f);

        Assert.That(steps, Is.EqualTo(CESharedZLevelsSystem.MaxStepsPerFrame));
    }
}
