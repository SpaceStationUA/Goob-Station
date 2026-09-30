// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Linq;
using Content.Client._Pirate.ZLevels.Core;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client._Pirate.ZLevels;

[TestFixture]
public sealed class ZCropRenderOrderTest
{
    private static ZCropPlanner.Crop Crop(int width, int height, int x = 0) =>
        new(new UIBox2i(x, 0, x + width, height), new UIBox2(x, 0, x + width, height));

    [Test]
    public void GroupsEqualSizesWithoutChangingTheCompositePlan()
    {
        ZCropPlanner.Crop[] crops = [Crop(256, 192), Crop(512, 384), Crop(256, 192, 600), Crop(512, 384, 400)];
        var original = crops.ToArray();
        Span<int> order = stackalloc int[4];
        ZCropRenderOrder.Fill(crops, order, true);
        Assert.That(order.ToArray(), Is.EqualTo(new[] { 0, 2, 1, 3 }));
        Assert.That(crops, Is.EqualTo(original), "Output rectangles and compositing order must stay intact.");
        ZCropRenderOrder.Fill(crops, order, false);
        Assert.That(order.ToArray(), Is.EqualTo(new[] { 0, 1, 2, 3 }));
    }

    [Test]
    public void EqualAreasWithDifferentDimensionsAreDifferentGroups()
    {
        ZCropPlanner.Crop[] crops = [Crop(256, 128), Crop(128, 256), Crop(256, 128), Crop(128, 256)];
        Span<int> order = stackalloc int[4];
        ZCropRenderOrder.Fill(crops, order, true);
        Assert.That(order.ToArray(), Is.EqualTo(new[] { 0, 2, 1, 3 }));
    }

    [Test]
    public void EveryFourCropSizeCombinationVisitsEachCropOnceWithMinimalTransitions()
    {
        for (var pattern = 0; pattern < 81; pattern++)
        {
            var value = pattern;
            var crops = new ZCropPlanner.Crop[4];
            for (var i = 0; i < 4; i++, value /= 3)
                crops[i] = Crop(128 * (1 + value % 3), 96 * (1 + value % 3));
            var order = new int[4];
            ZCropRenderOrder.Fill(crops, order, true);
            Assert.That(order, Is.EquivalentTo(new[] { 0, 1, 2, 3 }));
            var transitions = Enumerable.Range(1, 3).Count(i =>
                crops[order[i]].RenderBounds.Size != crops[order[i - 1]].RenderBounds.Size);
            Assert.That(transitions, Is.EqualTo(crops.Select(c => c.RenderBounds.Size).Distinct().Count() - 1));
        }
    }

    [Test]
    public void EmptyAndSingleCropPlansDoNotReadUnusedOrderSlots()
    {
        var order = new[] { -1, -1, -1, -1 };
        ZCropRenderOrder.Fill(Array.Empty<ZCropPlanner.Crop>(), order, true);
        Assert.That(order, Is.EqualTo(new[] { -1, -1, -1, -1 }));
        ZCropRenderOrder.Fill(new[] { Crop(256, 192) }, order, true);
        Assert.That(order, Is.EqualTo(new[] { 0, -1, -1, -1 }));
    }
}
