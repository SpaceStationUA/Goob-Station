// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Linq;
using Content.Client._Pirate.ZLevels.Core;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client._Pirate.ZLevels;

[TestFixture]
public sealed class ZCropPlannerTest
{
    private static readonly Vector2i FullSize = new(1024, 768);

    [Test]
    public void ScatteredOpeningsCanUseSeveralCropsWhenOneCannotFit()
    {
        UIBox2[] holes = [new(40, 40, 104, 104), new(880, 40, 944, 104),
            new(40, 600, 104, 664), new(880, 600, 944, 664)];
        var planner = new ZCropPlanner();
        Assert.That(planner.TryPlan(holes, FullSize, 32, 1), Is.False);
        Assert.That(planner.Crops, Is.Empty);
        Assert.That(planner.TryPlan(holes, FullSize, 32, 4), Is.True);
        Assert.That(planner.Crops.Count, Is.EqualTo(4));
        AssertCoverage(planner, holes);
        Assert.That(Area(planner), Is.LessThan(0.5));
    }

    [Test]
    public void SixOpeningsMergeIntoAtMostFourCropsWithoutDroppingAny()
    {
        UIBox2[] holes = [new(40, 40, 104, 104), new(168, 40, 232, 104),
            new(880, 40, 944, 104), new(40, 600, 104, 664),
            new(760, 600, 824, 664), new(880, 600, 944, 664)];
        var planner = new ZCropPlanner();
        Assert.That(planner.TryPlan(holes, FullSize, 32, 4), Is.True);
        Assert.That(planner.Crops.Count, Is.InRange(2, 4));
        AssertCoverage(planner, holes);
    }

    [Test]
    public void NearbyAndOverlappingOpeningsPreferOneCrop()
    {
        UIBox2[] holes = [new(400, 300, 464, 364), new(432, 332, 496, 396), new(464, 300, 528, 364)];
        var planner = new ZCropPlanner();
        Assert.That(planner.TryPlan(holes, FullSize, 32, 4), Is.True);
        Assert.That(planner.Crops.Count, Is.EqualTo(1));
        AssertCoverage(planner, holes);
    }

    [Test]
    public void RenderMarginsDoNotExpandMultiCropOutputIntoNeighboringOpenings()
    {
        UIBox2[] holes = [new(0, 0, 64, 64), new(896, 640, 960, 704)];
        var planner = new ZCropPlanner();
        Assert.That(planner.TryPlan(holes, FullSize, 32, 4), Is.True);
        Assert.That(planner.Crops.Count, Is.EqualTo(2));
        Assert.That(planner.Crops.Select(c => c.OutputBounds), Is.EquivalentTo(holes));
        foreach (var crop in planner.Crops)
        {
            Assert.That(crop.OutputBounds.Width, Is.LessThan(crop.RenderBounds.Width));
            Assert.That(crop.OutputBounds.Height, Is.LessThan(crop.RenderBounds.Height));
        }
        AssertCoverage(planner, holes);
    }

    [Test]
    public void ExpensivePlansFallBackInsteadOfSpawningMoreViewports()
    {
        UIBox2[] holes = [new(0, 0, 400, 300), new(624, 468, 1024, 768)];
        var planner = new ZCropPlanner();
        Assert.That(planner.TryPlan(holes, FullSize, 100, 4), Is.False);
        Assert.That(planner.Crops, Is.Empty);
    }

    [Test]
    public void OneCropModeMatchesThePreviousCropBounds()
    {
        UIBox2[] holes = [new(440, 300, 504, 364), new(550, 400, 614, 464)];
        var planner = new ZCropPlanner();
        Assert.That(ZViewportCrop.TryGetCrop(holes, FullSize, 72, out var expected), Is.True);
        Assert.That(planner.TryPlan(holes, FullSize, 72, 1), Is.True);
        Assert.That(planner.Crops.Single().RenderBounds, Is.EqualTo(expected));
    }

    [Test]
    public void FailedOrEmptyPlansCannotReuseThePreviousFramesCrops()
    {
        var planner = new ZCropPlanner();
        Assert.That(planner.TryPlan(new[] { new UIBox2(0, 0, 64, 64) }, FullSize, 32, 4), Is.True);
        Assert.That(planner.TryPlan(Array.Empty<UIBox2>(), FullSize, 32, 4), Is.False);
        Assert.That(planner.Crops, Is.Empty);
        Assert.That(planner.TryPlan(new[] { new UIBox2(0, 0, 1024, 768) }, FullSize, 32, 4), Is.False);
        Assert.That(planner.Crops, Is.Empty);
    }

    [Test]
    public void CachedPlansRespondToOpeningAndSettingChangesImmediately()
    {
        UIBox2[] holes = [new(0, 0, 64, 64), new(896, 640, 960, 704)];
        var planner = new ZCropPlanner();
        Assert.That(planner.TryPlan(holes, FullSize, 32, 4), Is.True);
        Assert.That(planner.TryPlan(holes, FullSize, 32, 4), Is.True);
        Assert.That(planner.TryPlan(holes, FullSize, 32, 1), Is.False);
        Assert.That(planner.Crops, Is.Empty);
        Assert.That(planner.TryPlan(holes, FullSize, 32, 4), Is.True);
        holes[1] = new UIBox2(0, 0, 1024, 768);
        Assert.That(planner.TryPlan(holes, FullSize, 32, 4), Is.False);
        Assert.That(planner.Crops, Is.Empty);
        holes[1] = new UIBox2(64, 0, 128, 64);
        Assert.That(planner.TryPlan(holes, FullSize, 32, 4), Is.True);
        Assert.That(planner.Crops.Count, Is.EqualTo(1));
        Assert.That(planner.TryPlan(holes, FullSize, 800, 4), Is.False);
    }

    [Test]
    public void RandomizedPlansKeepAllOpeningsAndRespectTheWorkBudget()
    {
        var random = new Random(729);
        var planner = new ZCropPlanner();
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var holes = new UIBox2[random.Next(1, 16)];
            for (var i = 0; i < holes.Length; i++)
            {
                var x = random.Next(0, 960);
                var y = random.Next(0, 704);
                holes[i] = new UIBox2(x, y, x + 64, y + 64);
            }
            var cap = random.Next(1, 5);
            if (!planner.TryPlan(holes, FullSize, 32, cap))
            {
                Assert.That(planner.Crops, Is.Empty);
                continue;
            }
            Assert.That(planner.Crops.Count, Is.InRange(1, cap));
            AssertCoverage(planner, holes);
            Assert.That(Area(planner) + 0.05 * planner.Crops.Count, Is.LessThanOrEqualTo(0.75001));
        }
    }

    private static double Area(ZCropPlanner planner) => planner.Crops.Sum(c =>
        (double) c.RenderBounds.Width * c.RenderBounds.Height / (FullSize.X * FullSize.Y));

    private static void AssertCoverage(ZCropPlanner planner, UIBox2[] holes)
    {
        foreach (var hole in holes)
            Assert.That(planner.Crops.Any(c => Contains(c.OutputBounds, hole)), Is.True, $"Uncovered opening: {hole}");
        foreach (var crop in planner.Crops)
        {
            Assert.That(Contains(new UIBox2(0, 0, FullSize.X, FullSize.Y),
                new UIBox2(crop.RenderBounds.Left, crop.RenderBounds.Top, crop.RenderBounds.Right, crop.RenderBounds.Bottom)), Is.True);
            Assert.That(Contains(new UIBox2(crop.RenderBounds.Left, crop.RenderBounds.Top,
                crop.RenderBounds.Right, crop.RenderBounds.Bottom), crop.OutputBounds), Is.True);
        }
    }

    private static bool Contains(UIBox2 outer, UIBox2 inner) => outer.Left <= inner.Left && outer.Top <= inner.Top &&
        outer.Right >= inner.Right && outer.Bottom >= inner.Bottom;
}
