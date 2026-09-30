// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.ZLevels.Core;
using Content.Shared.Maps;
using Moq;
using NUnit.Framework;
using Robust.Shared.Map;

namespace Content.Tests.Shared._Pirate.ZLevels;

/// <summary>Render culling includes ZTransparent floors; the sight predicate does not.</summary>
[TestFixture]
public sealed class CMUZLevelOpeningPredicateTest
{
    private const int Floor = 1;
    private const int Glass = 2;
    private const int SightPermeable = 3;

    private static ITileDefinitionManager Defs()
    {
        var defs = new Mock<ITileDefinitionManager>();
        defs.Setup(d => d[Floor]).Returns(new ContentTileDefinition());
        defs.Setup(d => d[Glass]).Returns(new ContentTileDefinition { ZTransparent = true });
        defs.Setup(d => d[SightPermeable]).Returns(new ContentTileDefinition { ZSightPermeable = true });
        return defs.Object;
    }

    [Test]
    public void TransparentFloorIsAVisualOpeningButNotASightOpening()
    {
        var defs = Defs();
        Assert.That(CMUZLevelOpeningCache.IsVisualOpeningTile(new Tile(Glass), defs), Is.True);
        Assert.That(CMUZLevelOpeningCache.IsOpeningTile(new Tile(Glass), defs), Is.False);
    }

    [Test]
    public void EmptyAndSightPermeableTilesAreOpeningsForBoth()
    {
        var defs = Defs();
        foreach (var tile in new[] { Tile.Empty, new Tile(SightPermeable) })
        {
            Assert.That(CMUZLevelOpeningCache.IsVisualOpeningTile(tile, defs), Is.True);
            Assert.That(CMUZLevelOpeningCache.IsOpeningTile(tile, defs), Is.True);
        }
    }

    [Test]
    public void SolidFloorIsNeverAnOpening()
    {
        var defs = Defs();
        Assert.That(CMUZLevelOpeningCache.IsVisualOpeningTile(new Tile(Floor), defs), Is.False);
        Assert.That(CMUZLevelOpeningCache.IsOpeningTile(new Tile(Floor), defs), Is.False);
    }
}
