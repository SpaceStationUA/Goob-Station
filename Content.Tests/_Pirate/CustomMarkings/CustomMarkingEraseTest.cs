// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.CustomMarkings;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Tests._Pirate.CustomMarkings;

/// <summary>Masks over a body: their bits, the sprite sheet one is drawn through, and how much erasing may take.</summary>
[TestFixture]
[TestOf(typeof(CustomMarkingErase))]
public sealed class CustomMarkingEraseTest
{
    private const int South = CustomMarkingArt.South;
    private const int East = CustomMarkingArt.East;
    private const int West = CustomMarkingArt.West;
    private const int Frame = CustomMarkingRules.FrameSize;

    private static byte[] Mask(params (int Facing, int X, int Y)[] pixels)
    {
        var mask = new byte[CustomMarkingRules.EraseBytes];
        foreach (var (facing, x, y) in pixels)
        {
            CustomMarkingErase.Set(mask, facing, x, y, true);
        }

        return mask;
    }

    [Test]
    public void BitsTest()
    {
        var mask = new byte[CustomMarkingRules.EraseBytes];
        Assert.That(mask, Has.Length.EqualTo(512), "a bit for each pixel of each facing");
        Assert.That(CustomMarkingErase.Any(mask), Is.False);

        Assert.That(CustomMarkingErase.Set(mask, East, 31, 31, true), Is.True);
        Assert.That(CustomMarkingErase.Set(mask, East, 31, 31, true), Is.False, "setting what is set changes nothing");
        Assert.Multiple(() =>
        {
            Assert.That(CustomMarkingErase.Get(mask, East, 31, 31), Is.True);
            Assert.That(CustomMarkingErase.Get(mask, East, 30, 31), Is.False);
            Assert.That(CustomMarkingErase.Get(mask, West, 0, 0), Is.False, "the next facing starts on a byte of its own");
            Assert.That(CustomMarkingErase.Any(mask), Is.True);
            Assert.That(CustomMarkingErase.Any(mask, East), Is.True);
            Assert.That(CustomMarkingErase.Any(mask, South), Is.False);
            Assert.That(CustomMarkingErase.Any(mask, West), Is.False);
        });

        var other = Mask((South, 0, 0), (East, 31, 31));
        Assert.Multiple(() =>
        {
            Assert.That(CustomMarkingErase.Overlaps(mask, other), Is.True);
            Assert.That(CustomMarkingErase.Overlaps(mask, Mask((South, 0, 0))), Is.False);
            Assert.That(CustomMarkingErase.Fingerprint(mask), Does.Match("^[0-9a-f]{16}$"));
            Assert.That(CustomMarkingErase.Fingerprint(mask), Is.Not.EqualTo(CustomMarkingErase.Fingerprint(other)));
            Assert.That(CustomMarkingErase.Fingerprint(mask), Is.EqualTo(CustomMarkingErase.Fingerprint(Mask((East, 31, 31)))));
        });

        CustomMarkingErase.Add(mask, Mask((South, 0, 0), (West, 5, 6)));
        Assert.That(CustomMarkingErase.Get(mask, South, 0, 0), Is.True);
        Assert.That(CustomMarkingErase.Get(mask, West, 5, 6), Is.True);
        Assert.That(CustomMarkingErase.Get(mask, East, 31, 31), Is.True, "what was there stays");

        Assert.That(CustomMarkingErase.Set(mask, East, 31, 31, false), Is.True);
        Assert.That(CustomMarkingErase.Any(mask, East), Is.False);
    }

    /// <summary>A body of ten pixels in the south facing: half of it has to stay in sight.</summary>
    [Test]
    public void LeavesEnoughTest()
    {
        var body = new byte[CustomMarkingRules.EraseBytes];
        for (var x = 0; x < 10; x++)
        {
            CustomMarkingErase.Set(body, South, x, 0, true);
        }

        var nothing = new byte[CustomMarkingRules.EraseBytes];
        var half = Mask((South, 0, 0), (South, 1, 0), (South, 2, 0), (South, 3, 0), (South, 4, 0));
        var more = Mask((South, 0, 0), (South, 1, 0), (South, 2, 0), (South, 3, 0), (South, 4, 0), (South, 5, 0));

        Assert.Multiple(() =>
        {
            Assert.That(CustomMarkingErase.MinKeptPercent, Is.EqualTo(50));
            Assert.That(CustomMarkingErase.LeavesEnough(body, nothing, nothing), Is.True);
            Assert.That(CustomMarkingErase.LeavesEnough(body, half, nothing), Is.True, "half may go");
            Assert.That(CustomMarkingErase.LeavesEnough(body, more, nothing), Is.False, "no more than half");
            Assert.That(CustomMarkingErase.LeavesEnough(body, more, Mask((South, 5, 0))), Is.True, "what is drawn over counts as kept");
            Assert.That(CustomMarkingErase.LeavesEnough(body, more, Mask((South, 20, 20))), Is.False, "drawing somewhere else doesn't");
            Assert.That(CustomMarkingErase.LeavesEnough(body, body, body), Is.True, "a body wholly redrawn is all there");
            Assert.That(CustomMarkingErase.LeavesEnough(body, Mask((South, 20, 20), (East, 1, 1)), nothing), Is.True,
                "erasing where the body isn't takes nothing");
            Assert.That(CustomMarkingErase.LeavesEnough(nothing, more, nothing), Is.True, "no body, nothing to lose");
        });

        // Each facing is counted on its own: plenty left in one doesn't make up for another.
        CustomMarkingErase.Set(body, East, 3, 3, true);
        CustomMarkingErase.Set(body, East, 4, 3, true);
        CustomMarkingErase.Set(body, East, 5, 3, true);
        Assert.That(CustomMarkingErase.LeavesEnough(body, Mask((East, 3, 3)), nothing), Is.True);
        Assert.That(CustomMarkingErase.LeavesEnough(body, Mask((East, 3, 3), (East, 4, 3)), nothing), Is.False);
    }

    /// <summary>The sheet a mask is drawn through is laid out like a sprite's four facings, the mask in the middle of each.</summary>
    [Test]
    public void SheetTest()
    {
        var mask = Mask((South, 0, 0), (CustomMarkingArt.North, 31, 0), (East, 2, 3), (West, 31, 31));

        using var plain = Image.Load<Rgba32>(CustomMarkingErase.ToPng(mask, Frame, Frame));
        Assert.Multiple(() =>
        {
            Assert.That((plain.Width, plain.Height), Is.EqualTo((Frame * 2, Frame * 2)));
            Assert.That(plain[0, 0].A, Is.EqualTo(255));
            Assert.That(plain[Frame + 31, 0].A, Is.EqualTo(255), "north is the second cell of the top row");
            Assert.That(plain[2, Frame + 3].A, Is.EqualTo(255), "east the first of the bottom row");
            Assert.That(plain[Frame + 31, Frame + 31].A, Is.EqualTo(255));
            Assert.That(plain[1, 0].A, Is.Zero);
            Assert.That(Opaque(plain), Is.EqualTo(4));
        });

        // For a sprite twice the size, drawn centred on the body, the mask is in the middle of each cell.
        using var large = Image.Load<Rgba32>(CustomMarkingErase.ToPng(mask, Frame * 2, Frame * 2));
        Assert.Multiple(() =>
        {
            Assert.That((large.Width, large.Height), Is.EqualTo((Frame * 4, Frame * 4)));
            Assert.That(large[16, 16].A, Is.EqualTo(255));
            Assert.That(large[Frame * 2 + 16 + 31, 16].A, Is.EqualTo(255));
            Assert.That(large[16 + 2, Frame * 2 + 16 + 3].A, Is.EqualTo(255));
            Assert.That(large[0, 0].A, Is.Zero);
            Assert.That(Opaque(large), Is.EqualTo(4));
        });

        // For a smaller one, what falls outside it is left off, not wrapped into the next cell.
        using var small = Image.Load<Rgba32>(CustomMarkingErase.ToPng(mask, 16, 16));
        Assert.That((small.Width, small.Height), Is.EqualTo((32, 32)));
        Assert.That(Opaque(small), Is.Zero, "every masked pixel lay in the rim that a small sprite doesn't have");

        using var middle = Image.Load<Rgba32>(CustomMarkingErase.ToPng(Mask((East, 8, 9)), 16, 16));
        Assert.That(middle[0, 16 + 1].A, Is.EqualTo(255));
        Assert.That(Opaque(middle), Is.EqualTo(1));
    }

    private static int Opaque(Image<Rgba32> image)
    {
        var count = 0;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (image[x, y].A != 0)
                    count++;
            }
        }

        return count;
    }
}
