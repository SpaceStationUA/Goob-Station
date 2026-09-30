// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Client._Pirate.ZLevels.Core;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client._Pirate.ZLevels;

[TestFixture]
public sealed class ZVisibilityMaskTest
{
    [Test]
    public void FullyOpenAndMostlyOpenViewsUseTheNormalPath()
    {
        var mask = new ZVisibilityMask();
        var regions = new List<UIBox2>();
        Assert.That(mask.Reset(new Vector2i(256, 256)), Is.True);
        Assert.That(mask.TryGetRegions(regions), Is.False);
        for (var x = 0; x < 4; x++)
            mask.Hide(x, 0);
        Assert.That(mask.TryGetRegions(regions), Is.False);
    }

    [Test]
    public void EnclosedViewHasAnEmptyRegionList()
    {
        var mask = new ZVisibilityMask();
        var regions = new List<UIBox2>();
        mask.Reset(new Vector2i(128, 128));
        for (var y = 0; y < 2; y++)
        for (var x = 0; x < 2; x++)
            mask.Hide(x, y);
        Assert.That(mask.TryGetRegions(regions), Is.True);
        Assert.That(regions, Is.Empty);
    }

    [Test]
    public void AdjacentVisibleCellsMergeWithoutFillingTheGapBetweenHoles()
    {
        var mask = new ZVisibilityMask();
        var regions = new List<UIBox2>();
        mask.Reset(new Vector2i(384, 256));
        for (var y = 0; y < mask.Rows; y++)
        for (var x = 0; x < mask.Columns; x++)
        {
            if (!(y < 2 && (x < 2 || x == 4)))
                mask.Hide(x, y);
        }

        Assert.That(mask.TryGetRegions(regions), Is.True);
        Assert.That(regions, Is.EquivalentTo(new[]
        {
            new UIBox2(0, 0, 128, 128),
            new UIBox2(256, 0, 320, 128),
        }));
    }

    [Test]
    public void DeeperFloorsCanOnlyNarrowTheVisibilityMask()
    {
        var mask = new ZVisibilityMask();
        mask.Reset(new Vector2i(256, 256));
        mask.Hide(0, 0);
        mask.Hide(0, 0); // A floor below is opaque here too.
        mask.Hide(1, 0);
        Assert.That(mask.VisibleCount, Is.EqualTo(14));
        Assert.That(mask.IsVisible(0, 0), Is.False);
        Assert.That(mask.IsVisible(1, 0), Is.False);
        Assert.That(mask.IsVisible(2, 0), Is.True);
        mask.Reset(new Vector2i(256, 256)); // A new frame must expose newly opened tiles immediately.
        Assert.That(mask.IsVisible(0, 0), Is.True);
    }

    [Test]
    public void PartialEdgeCellsStayInsideRenderTargetAndResizeResetsState()
    {
        var mask = new ZVisibilityMask();
        mask.Reset(new Vector2i(130, 100));
        Assert.That(mask.CellBounds(2, 1), Is.EqualTo(new UIBox2(128, 64, 130, 100)));
        mask.Hide(0, 0);
        mask.Reset(new Vector2i(64, 64));
        Assert.That(mask.VisibleCount, Is.EqualTo(1));
        Assert.That(mask.IsVisible(0, 0), Is.True);
    }

    [Test]
    public void FragmentedGeometryFallsBackWithoutReturningAnIncompleteMask()
    {
        var mask = new ZVisibilityMask();
        var regions = new List<UIBox2>();
        mask.Reset(new Vector2i(1024, 1024));
        for (var y = 0; y < mask.Rows; y++)
        for (var x = 0; x < mask.Columns; x++)
        {
            if ((x + y) % 2 == 0)
                mask.Hide(x, y);
        }
        Assert.That(mask.TryGetRegions(regions), Is.False);
        Assert.That(regions, Is.Empty);
    }

    [TestCase(0, 1080)]
    [TestCase(-1, 1080)]
    [TestCase(16385, 1080)]
    [TestCase(8192, 8192)]
    public void InvalidOrExcessiveResolutionFallsBack(int width, int height)
    {
        Assert.That(new ZVisibilityMask().Reset(new Vector2i(width, height)), Is.False);
    }
}
