// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.CustomMarkings;
using NUnit.Framework;

namespace Content.Tests._Pirate.CustomMarkings;

/// <summary>Which body part owns each pixel: the body itself, the margin around it and where parts overlap.</summary>
[TestFixture]
[TestOf(typeof(CustomMarkingSections))]
public sealed class CustomMarkingSectionsTest
{
    private const int South = CustomMarkingArt.South;

    private static byte At(byte[] map, int facing, int x, int y)
    {
        return map[CustomMarkingSections.Index(facing, x, y)];
    }

    /// <summary>One part, a square from 10 to 12 on the south facing.</summary>
    [Test]
    public void MarginTest()
    {
        var map = CustomMarkingSections.Build(1, 1, 0,
            (_, facing, x, y) => facing == South && x is >= 10 and <= 12 && y is >= 10 and <= 12);

        Assert.That(map, Has.Length.EqualTo(CustomMarkingSections.Size));
        Assert.Multiple(() =>
        {
            Assert.That(At(map, South, 11, 11), Is.EqualTo(1), "on the part");
            Assert.That(At(map, South, 9, 10), Is.EqualTo(1), "one pixel off it");
            Assert.That(At(map, South, 8, 10), Is.EqualTo(1), "two pixels off it");
            Assert.That(At(map, South, 7, 10), Is.EqualTo(CustomMarkingSections.None), "three is out of reach");
            Assert.That(At(map, South, 9, 9), Is.EqualTo(1), "a corner, one across and one up");
            Assert.That(At(map, South, 8, 9), Is.EqualTo(CustomMarkingSections.None), "two across and one up is further than two");
            Assert.That(At(map, South, 14, 12), Is.EqualTo(1));
            Assert.That(At(map, South, 12, 14), Is.EqualTo(1));
            Assert.That(At(map, South, 14, 14), Is.EqualTo(CustomMarkingSections.None));
            Assert.That(At(map, South, 0, 0), Is.EqualTo(CustomMarkingSections.None));
            Assert.That(At(map, CustomMarkingArt.North, 11, 11), Is.EqualTo(CustomMarkingSections.None), "a facing with no body has no reach");
        });
    }

    /// <summary>The margin never wraps past an edge of the facing onto the next row or facing.</summary>
    [Test]
    public void EdgeTest()
    {
        var map = CustomMarkingSections.Build(1, 1, 0, (_, facing, x, y) => facing == South && x == 0 && y == 5);

        Assert.Multiple(() =>
        {
            Assert.That(At(map, South, 2, 5), Is.EqualTo(1));
            Assert.That(At(map, South, CustomMarkingRules.FrameSize - 1, 4), Is.EqualTo(CustomMarkingSections.None));
            Assert.That(At(map, South, CustomMarkingRules.FrameSize - 1, 5), Is.EqualTo(CustomMarkingSections.None));
            Assert.That(At(map, CustomMarkingArt.North, 0, 5), Is.EqualTo(CustomMarkingSections.None));
        });
    }

    /// <summary>
    /// Three parts in draw order: a torso and an arm under the art and a hand over it. The arm overlaps the torso and
    /// the hand overlaps the arm.
    /// </summary>
    [Test]
    public void OverlapTest()
    {
        var map = CustomMarkingSections.Build(3, 2, 0, (part, facing, x, y) => facing == South && y == 10 && part switch
        {
            0 => x is >= 10 and <= 20, // torso
            1 => x is >= 19 and <= 24, // arm
            _ => x is >= 24 and <= 26, // hand
        });

        Assert.Multiple(() =>
        {
            Assert.That(At(map, South, 15, 10), Is.EqualTo(1), "torso alone");
            Assert.That(At(map, South, 20, 10), Is.EqualTo(2), "where the arm lies over the torso, the art is seen on the arm");
            Assert.That(At(map, South, 22, 10), Is.EqualTo(2), "arm alone");
            Assert.That(At(map, South, 24, 10), Is.EqualTo(2), "under the hand the art lies on the arm, and stays with it");
            Assert.That(At(map, South, 25, 10), Is.EqualTo(3), "the hand alone: art there would float if the hand went");
            Assert.That(At(map, South, 28, 10), Is.EqualTo(3), "the margin goes to the nearest part");
            Assert.That(At(map, South, 8, 10), Is.EqualTo(1));
            Assert.That(At(map, South, 22, 12), Is.EqualTo(2), "above and below as well");
            Assert.That(At(map, South, 29, 10), Is.EqualTo(CustomMarkingSections.None));
        });

        // The same body with the art under everything, as for a marking behind it: the topmost part has each pixel.
        var behind = CustomMarkingSections.Build(3, 0, 0, (part, facing, x, y) => facing == South && y == 10 && part switch
        {
            0 => x is >= 10 and <= 20,
            1 => x is >= 19 and <= 24,
            _ => x is >= 24 and <= 26,
        });
        Assert.That(At(behind, South, 20, 10), Is.EqualTo(2));
        Assert.That(At(behind, South, 24, 10), Is.EqualTo(3));
    }

    /// <summary>
    /// A torso and, numbered after it, a tail that starts on the torso and runs out past it: the tail has what the
    /// torso doesn't, and its own margin.
    /// </summary>
    [Test]
    public void ExtrasTest()
    {
        var map = CustomMarkingSections.Build(1, 1, 1, (part, facing, x, y) => facing == South && y == 10 && (part == 0
            ? x is >= 10 and <= 20
            : x is >= 18 and <= 28));

        Assert.Multiple(() =>
        {
            Assert.That(At(map, South, 19, 10), Is.EqualTo(1), "on the body, a pixel is the body's whatever else is drawn there");
            Assert.That(At(map, South, 25, 10), Is.EqualTo(2), "past the body, the tail's");
            Assert.That(At(map, South, 30, 10), Is.EqualTo(2), "with a margin of its own");
            Assert.That(At(map, South, 31, 10), Is.EqualTo(CustomMarkingSections.None));
            Assert.That(At(map, South, 25, 12), Is.EqualTo(2));
        });

        // Without the tail, where it was is out of reach.
        var bare = CustomMarkingSections.Build(1, 1, 0, (_, facing, x, y) => facing == South && y == 10 && x is >= 10 and <= 20);
        Assert.That(At(bare, South, 22, 10), Is.EqualTo(1), "the torso's own margin");
        Assert.That(At(bare, South, 25, 10), Is.EqualTo(CustomMarkingSections.None));
    }

    /// <summary>A pixel off the body between two parts goes to the nearer one.</summary>
    [Test]
    public void NearestPartTest()
    {
        var map = CustomMarkingSections.Build(2, 2, 0, (part, facing, x, y) => facing == South && y == 10 && x == (part == 0 ? 10 : 14));

        Assert.Multiple(() =>
        {
            Assert.That(At(map, South, 11, 10), Is.EqualTo(1));
            Assert.That(At(map, South, 13, 10), Is.EqualTo(2));
            Assert.That(At(map, South, 12, 10), Is.Not.EqualTo(CustomMarkingSections.None), "the middle goes to one of them");
        });
    }
}
