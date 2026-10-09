// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using Content.Client._Pirate.CustomMarkings;
using Content.Shared._Pirate.CustomMarkings;
using NUnit.Framework;
using Robust.Shared.Maths;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Tests._Pirate.CustomMarkings;

/// <summary>The pixel sheet: its layout, the editor's operations and the PNG it travels as.</summary>
[TestFixture]
[TestOf(typeof(CustomMarkingArt))]
[TestOf(typeof(CustomMarkingPng))]
public sealed class CustomMarkingArtTest
{
    private const int Frame = CustomMarkingRules.FrameSize;
    private const int Sheet = CustomMarkingRules.SheetSize;

    private static readonly Rgba32 Red = new(255, 0, 0, 255);
    private static readonly Rgba32 Blue = new(0, 0, 255, 128);

    /// <summary>Facings sit on the sheet the way an RSI state lays out its four directions.</summary>
    [TestCase(CustomMarkingArt.South, 0, 0)]
    [TestCase(CustomMarkingArt.North, Frame, 0)]
    [TestCase(CustomMarkingArt.East, 0, Frame)]
    [TestCase(CustomMarkingArt.West, Frame, Frame)]
    public void SheetLayoutTest(int facing, int sheetX, int sheetY)
    {
        var art = new CustomMarkingArt();
        Assert.That(art.SetPixel(0, facing, 3, 5, Red), Is.True);
        Assert.That(art.SetPixel(0, facing, 3, 5, Red), Is.False, "setting the same value changes nothing");

        var at = ((sheetY + 5) * Sheet + sheetX + 3) * 4;
        Assert.Multiple(() =>
        {
            Assert.That(art.Pixels[at], Is.EqualTo(255));
            Assert.That(art.Pixels[at + 3], Is.EqualTo(255));
            Assert.That(art.GetPixel(0, facing, 3, 5), Is.EqualTo(Red));
            Assert.That(art.IsBlank(), Is.False);
        });

        art.Clear(0, facing);
        Assert.That(art.IsBlank(), Is.True);
    }

    [Test]
    public void FromPixelsTest()
    {
        Assert.That(CustomMarkingArt.FromPixels(null), Is.Null);
        Assert.That(CustomMarkingArt.FromPixels(new byte[CustomMarkingRules.PixelBytes - 4]), Is.Null);

        // A transparent pixel can't carry colour: the same drawing always has the same bytes, and so one hash.
        var raw = new byte[CustomMarkingRules.PixelBytes];
        raw[0] = 200;
        raw[4] = 10;
        raw[7] = 255;
        var art = CustomMarkingArt.FromPixels(raw)!;
        Assert.Multiple(() =>
        {
            Assert.That(art.Pixels[0], Is.Zero);
            Assert.That(art.GetPixel(0, CustomMarkingArt.South, 1, 0), Is.EqualTo(new Rgba32(10, 0, 0, 255)));
            Assert.That(art.Pixels, Is.Not.SameAs(raw));
        });

        var edited = new CustomMarkingArt();
        edited.SetPixel(0, CustomMarkingArt.South, 0, 0, new Rgba32(200, 0, 0, 0));
        Assert.That(edited.IsBlank(), Is.True);
        Assert.That(edited.Pixels[0], Is.Zero);
    }

    [Test]
    public void ColourConversionTest()
    {
        for (var value = 0; value <= byte.MaxValue; value++)
        {
            var pixel = new Rgba32((byte) value, (byte) (255 - value), (byte) value, (byte) value);
            Assert.That(CustomMarkingArt.ToPixel(CustomMarkingArt.ToColor(pixel)), Is.EqualTo(pixel), $"channel value {value}");
        }

        Assert.That(CustomMarkingArt.ToPixel(new Color(2f, -1f, 0.5f, 1f)), Is.EqualTo(new Rgba32(255, 0, 128, 255)));
    }

    [Test]
    public void FillTest()
    {
        var art = new CustomMarkingArt();
        // A closed box from (2,2) to (6,6).
        for (var i = 2; i <= 6; i++)
        {
            art.SetPixel(0, CustomMarkingArt.East, i, 2, Red);
            art.SetPixel(0, CustomMarkingArt.East, i, 6, Red);
            art.SetPixel(0, CustomMarkingArt.East, 2, i, Red);
            art.SetPixel(0, CustomMarkingArt.East, 6, i, Red);
        }

        art.Fill(0, CustomMarkingArt.East, 4, 4, Blue);
        Assert.Multiple(() =>
        {
            Assert.That(art.GetPixel(0, CustomMarkingArt.East, 3, 3), Is.EqualTo(Blue));
            Assert.That(art.GetPixel(0, CustomMarkingArt.East, 5, 5), Is.EqualTo(Blue));
            Assert.That(art.GetPixel(0, CustomMarkingArt.East, 2, 2), Is.EqualTo(Red), "the outline stays");
            Assert.That(art.GetPixel(0, CustomMarkingArt.East, 7, 7).A, Is.Zero, "the fill stays inside");
            Assert.That(art.GetPixel(0, CustomMarkingArt.West, 4, 4).A, Is.Zero, "and on its facing");
        });

        // Filling the outside reaches every edge of the facing and nothing past it.
        art.Fill(0, CustomMarkingArt.East, 0, 0, Red);
        Assert.Multiple(() =>
        {
            Assert.That(art.GetPixel(0, CustomMarkingArt.East, Frame - 1, Frame - 1), Is.EqualTo(Red));
            Assert.That(art.GetPixel(0, CustomMarkingArt.East, 4, 4), Is.EqualTo(Blue));
            Assert.That(art.GetPixel(0, CustomMarkingArt.South, 0, Frame - 1).A, Is.Zero);
            Assert.That(art.GetPixel(0, CustomMarkingArt.West, 0, 0).A, Is.Zero);
        });

        Assert.DoesNotThrow(() => art.Fill(0, CustomMarkingArt.East, -1, 40, Red));
    }

    [Test]
    public void MoveAndMirrorTest()
    {
        var art = new CustomMarkingArt();
        art.SetPixel(0, CustomMarkingArt.East, 1, 2, Red);
        art.SetPixel(0, CustomMarkingArt.East, Frame - 1, 0, Blue);

        art.CopyFacing(0, CustomMarkingArt.East, CustomMarkingArt.West, true);
        Assert.Multiple(() =>
        {
            Assert.That(art.GetPixel(0, CustomMarkingArt.West, Frame - 2, 2), Is.EqualTo(Red));
            Assert.That(art.GetPixel(0, CustomMarkingArt.West, 0, 0), Is.EqualTo(Blue));
            Assert.That(art.GetPixel(0, CustomMarkingArt.East, 1, 2), Is.EqualTo(Red), "the source is left alone");
        });

        // Flipping a facing in place.
        art.CopyFacing(0, CustomMarkingArt.East, CustomMarkingArt.East, true);
        Assert.That(art.GetPixel(0, CustomMarkingArt.East, Frame - 2, 2), Is.EqualTo(Red));
        Assert.That(art.GetPixel(0, CustomMarkingArt.East, 1, 2).A, Is.Zero);

        art.Shift(0, CustomMarkingArt.East, 1, -1);
        Assert.Multiple(() =>
        {
            Assert.That(art.GetPixel(0, CustomMarkingArt.East, Frame - 1, 1), Is.EqualTo(Red));
            Assert.That(art.GetPixel(0, CustomMarkingArt.East, 1, 0).A, Is.Zero, "pixels pushed off the top are gone");
            Assert.That(art.GetPixel(0, CustomMarkingArt.West, Frame - 2, 2), Is.EqualTo(Red), "other facings don't move");
        });

        var before = art.Clone();
        art.Clear(0, CustomMarkingArt.West);
        Assert.That(art.SamePixels(before), Is.False);
        art.Restore(before);
        Assert.That(art.SamePixels(before), Is.True);
    }

    [Test]
    public void FlipTest()
    {
        var art = new CustomMarkingArt();
        art.SetPixel(0, CustomMarkingArt.South, 10, 3, Red);
        art.SetPixel(0, CustomMarkingArt.South, 15, 4, Blue);
        art.SetPixel(0, CustomMarkingArt.South, 31, 5, Red);
        art.SetPixel(0, CustomMarkingArt.North, 10, 3, Red);

        // About column 15, a body's middle: its edges swap, and the middle stays.
        art.Flip(0, CustomMarkingArt.South, 30);
        Assert.Multiple(() =>
        {
            Assert.That(art.GetPixel(0, CustomMarkingArt.South, 20, 3), Is.EqualTo(Red));
            Assert.That(art.GetPixel(0, CustomMarkingArt.South, 10, 3).A, Is.Zero);
            Assert.That(art.GetPixel(0, CustomMarkingArt.South, 15, 4), Is.EqualTo(Blue));
            Assert.That(art.GetPixel(0, CustomMarkingArt.South, 31, 5).A, Is.Zero, "a pixel flipped off the facing is lost");
            Assert.That(art.GetPixel(0, CustomMarkingArt.North, 10, 3), Is.EqualTo(Red), "other facings stay");
        });

        // Twice is back where it started, less what fell off.
        art.Flip(0, CustomMarkingArt.South, 30);
        Assert.That(art.GetPixel(0, CustomMarkingArt.South, 10, 3), Is.EqualTo(Red));
        Assert.That(art.GetPixel(0, CustomMarkingArt.South, 31, 5).A, Is.Zero);

        // About the middle of the frame, the first and last columns swap.
        art.Flip(0, CustomMarkingArt.South, CustomMarkingRules.FrameSize - 1);
        Assert.That(art.GetPixel(0, CustomMarkingArt.South, 21, 3), Is.EqualTo(Red));
        Assert.That(art.GetPixel(0, CustomMarkingArt.South, 16, 4), Is.EqualTo(Blue));
    }

    [Test]
    public void WithoutTest()
    {
        var art = new CustomMarkingArt();
        art.SetPixel(0, CustomMarkingArt.South, 1, 1, Red);
        art.SetPixel(0, CustomMarkingArt.South, 2, 2, Blue);
        art.SetPixel(0, CustomMarkingArt.West, 1, 1, Red);

        var asked = 0;
        var cut = art.Without((facing, x, y) =>
        {
            asked++;
            return facing == CustomMarkingArt.South && x == 1;
        });

        Assert.Multiple(() =>
        {
            Assert.That(asked, Is.EqualTo(3), "only drawn pixels are asked about");
            Assert.That(cut.GetPixel(0, CustomMarkingArt.South, 1, 1).A, Is.Zero);
            Assert.That(cut.GetPixel(0, CustomMarkingArt.South, 2, 2), Is.EqualTo(Blue));
            Assert.That(cut.GetPixel(0, CustomMarkingArt.West, 1, 1), Is.EqualTo(Red));
            Assert.That(art.GetPixel(0, CustomMarkingArt.South, 1, 1), Is.EqualTo(Red), "the original is left alone");
            Assert.That(art.Without((_, _, _) => false).SamePixels(art), Is.True);
            Assert.That(art.Without((_, _, _) => true).IsBlank(), Is.True);
        });
    }

    [Test]
    public void FingerprintTest()
    {
        var art = new CustomMarkingArt();
        art.SetPixel(0, CustomMarkingArt.North, 5, 6, Red);
        var same = art.Clone();
        var other = art.Clone();
        other.SetPixel(0, CustomMarkingArt.North, 5, 7, Red);

        Assert.Multiple(() =>
        {
            Assert.That(art.Fingerprint(), Has.Length.EqualTo(16));
            Assert.That(art.Fingerprint(), Does.Match("^[0-9a-f]{16}$"));
            Assert.That(same.Fingerprint(), Is.EqualTo(art.Fingerprint()));
            Assert.That(other.Fingerprint(), Is.Not.EqualTo(art.Fingerprint()));
            Assert.That(new CustomMarkingArt().Fingerprint(), Is.Not.EqualTo(art.Fingerprint()));
        });
    }

    [Test]
    public void PngRoundTripTest()
    {
        var art = new CustomMarkingArt();
        art.SetPixel(0, CustomMarkingArt.South, 0, 0, Red);
        art.SetPixel(0, CustomMarkingArt.North, 31, 31, Blue);
        art.SetPixel(0, CustomMarkingArt.East, 7, 9, new Rgba32(1, 2, 3, 4));
        art.SetPixel(0, CustomMarkingArt.West, 16, 16, new Rgba32(250, 251, 252, 253));

        var read = CustomMarkingPng.Read(art.ToPng());
        Assert.That(read, Is.Not.Null);
        Assert.That(read!.SamePixels(art), Is.True);
    }

    [Test]
    public void ImportTest()
    {
        // One facing alone becomes the south facing.
        using var single = new Image<Rgba32>(Frame, Frame);
        single[4, 5] = Red;
        var art = CustomMarkingPng.Read(Png(single));
        Assert.That(art, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(art!.GetPixel(0, CustomMarkingArt.South, 4, 5), Is.EqualTo(Red));
            art.Clear(0, CustomMarkingArt.South);
            Assert.That(art.IsBlank(), Is.True);
        });

        using var wide = new Image<Rgba32>(Sheet, Frame);
        using var large = new Image<Rgba32>(Sheet * 2, Sheet * 2);
        using var animated = new Image<Rgba32>(Frame, Frame);
        animated.Frames.AddFrame(single.Frames.RootFrame);
        Assert.Multiple(() =>
        {
            Assert.That(CustomMarkingPng.Read(Png(wide)), Is.Null, "not square");
            Assert.That(CustomMarkingPng.Read(Png(large)), Is.Null, "too big");
            Assert.That(CustomMarkingPng.Read(new byte[] { 1, 2, 3 }), Is.Null, "not an image");
            Assert.That(CustomMarkingPng.Read(new byte[CustomMarkingPng.MaxFileBytes + 1]), Is.Null, "too many bytes");
            Assert.That(CustomMarkingPng.Read(Png(animated)), Is.Null, "animation belongs in bounded sheet rows, not APNG frames");
        });
    }

    /// <summary>An animated marking: frames are added as copies, drawn on apart, timed and taken out.</summary>
    [Test]
    public void FramesTest()
    {
        var art = new CustomMarkingArt();
        art.SetPixel(0, CustomMarkingArt.South, 1, 1, Red);
        Assert.That(art.Frames, Is.EqualTo(1));

        Assert.That(art.AddFrame(0), Is.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(art.Frames, Is.EqualTo(2));
            Assert.That(art.Pixels, Has.Length.EqualTo(2 * CustomMarkingRules.PixelBytes));
            Assert.That(art.GetPixel(1, CustomMarkingArt.South, 1, 1), Is.EqualTo(Red), "a new frame starts as a copy of the one before");
            Assert.That(art.GetFrameTime(1), Is.EqualTo(art.GetFrameTime(0)));
        });

        art.SetPixel(1, CustomMarkingArt.South, 2, 2, Blue);
        Assert.That(art.GetPixel(0, CustomMarkingArt.South, 2, 2).A, Is.Zero, "frames are drawn on apart");

        // A frame goes in after the one named, and the rest move up.
        Assert.That(art.AddFrame(0), Is.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(art.Frames, Is.EqualTo(3));
            Assert.That(art.GetPixel(1, CustomMarkingArt.South, 2, 2).A, Is.Zero, "the copy of the first frame");
            Assert.That(art.GetPixel(2, CustomMarkingArt.South, 2, 2), Is.EqualTo(Blue), "what was the second frame");
        });

        Assert.That(art.SetFrameTime(2, 500), Is.True);
        Assert.That(art.SetFrameTime(2, 500), Is.False);
        Assert.That(art.SetFrameTime(0, 1), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(art.GetFrameTime(0), Is.EqualTo(CustomMarkingRules.MinFrameTime), "a frame can't flash by");
            Assert.That(art.GetFrameTimes(), Is.EqualTo(new[] { CustomMarkingRules.MinFrameTime, CustomMarkingRules.DefaultFrameTime, 500 }));
        });

        Assert.That(art.RemoveFrame(1), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(art.Frames, Is.EqualTo(2));
            Assert.That(art.GetPixel(1, CustomMarkingArt.South, 2, 2), Is.EqualTo(Blue));
            Assert.That(art.GetFrameTimes(), Is.EqualTo(new[] { CustomMarkingRules.MinFrameTime, 500 }));
        });

        Assert.That(art.RemoveFrame(0), Is.True);
        Assert.That(art.RemoveFrame(0), Is.False, "the last frame stays");
        Assert.That(art.GetPixel(0, CustomMarkingArt.South, 2, 2), Is.EqualTo(Blue));

        // No more frames than the limit given, or than the rules allow at all.
        Assert.That(art.AddFrame(0, 1), Is.EqualTo(-1));
        for (var i = 1; i < CustomMarkingRules.MaxFrames; i++)
        {
            Assert.That(art.AddFrame(art.Frames - 1, int.MaxValue), Is.EqualTo(i));
        }

        Assert.That(art.AddFrame(0, int.MaxValue), Is.EqualTo(-1));
    }

    /// <summary>
    /// An animated marking's sheet is what an RSI state loads: two cells to a row, every frame of the south facing
    /// first, then the north's, the east's and the west's.
    /// </summary>
    [Test]
    public void AnimatedSheetTest()
    {
        var art = new CustomMarkingArt();
        art.AddFrame(0);
        art.AddFrame(1);
        for (var frame = 0; frame < 3; frame++)
        {
            for (var facing = 0; facing < CustomMarkingRules.Facings; facing++)
            {
                art.SetPixel(frame, facing, frame, facing, new Rgba32((byte) (10 + frame), (byte) (20 + facing), 0, 255));
            }
        }

        var png = art.ToPng();
        using var image = Image.Load<Rgba32>(png);
        Assert.Multiple(() =>
        {
            Assert.That((image.Width, image.Height), Is.EqualTo((Sheet, Sheet * 3)));
            // Cell 4, the north facing's second frame: third row, first column.
            Assert.That(image[1, 2 * Frame + 1], Is.EqualTo(new Rgba32(11, 21, 0, 255)));
            // Cell 11, the west facing's last frame: sixth row, second column.
            Assert.That(image[Frame + 2, 5 * Frame + 3], Is.EqualTo(new Rgba32(12, 23, 0, 255)));
        });

        var read = CustomMarkingPng.Read(png);
        Assert.That(read, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(read!.Frames, Is.EqualTo(3));
            Assert.That(read.SamePixels(art), Is.True);
        });

        // A still marking's sheet is as it always was: the four facings, two to a row.
        using var still = Image.Load<Rgba32>(new CustomMarkingArt().ToPng());
        Assert.That((still.Width, still.Height), Is.EqualTo((Sheet, Sheet)));

        using var tooMany = new Image<Rgba32>(Sheet, Sheet * (CustomMarkingRules.MaxFrames + 1));
        Assert.That(CustomMarkingPng.Read(Png(tooMany)), Is.Null, "more frames than a marking can have");
    }

    /// <summary>What a client uploads: frames, a time for each and a mask, all checked.</summary>
    [Test]
    public void UploadTest()
    {
        var two = new byte[2 * CustomMarkingRules.PixelBytes];
        two[CustomMarkingRules.PixelBytes + 3] = 255;
        var erase = new byte[CustomMarkingRules.EraseBytes];
        erase[0] = 1;

        var art = CustomMarkingArt.FromPixels(two, new[] { 1, 99999 }, erase)!;
        Assert.Multiple(() =>
        {
            Assert.That(art.Frames, Is.EqualTo(2));
            Assert.That(art.GetPixel(1, CustomMarkingArt.South, 0, 0).A, Is.EqualTo(255));
            Assert.That(art.GetFrameTimes(), Is.EqualTo(new[] { CustomMarkingRules.MinFrameTime, CustomMarkingRules.MaxFrameTime }),
                "times are kept in range");
            Assert.That(art.IsErased(CustomMarkingArt.South, 0, 0), Is.True);
            Assert.That(art.Erase, Is.Not.SameAs(erase));
        });

        Assert.Multiple(() =>
        {
            Assert.That(CustomMarkingArt.FromPixels(two)!.GetFrameTimes(),
                Is.EqualTo(new[] { CustomMarkingRules.DefaultFrameTime, CustomMarkingRules.DefaultFrameTime }));
            Assert.That(CustomMarkingArt.FromPixels(two, maxFrames: 1), Is.Null, "more frames than allowed");
            Assert.That(CustomMarkingArt.FromPixels(two, new[] { 200 }), Is.Null, "a time for each frame");
            Assert.That(CustomMarkingArt.FromPixels(two, null, new byte[3]), Is.Null, "a whole mask");
            Assert.That(CustomMarkingArt.FromPixels(new byte[0]), Is.Null);
            Assert.That(CustomMarkingArt.FromPixels(new byte[(CustomMarkingRules.MaxFrames + 1) * CustomMarkingRules.PixelBytes]), Is.Null);
        });
    }

    /// <summary>The body pixels a marking erases are kept apart from what it draws.</summary>
    [Test]
    public void EraseTest()
    {
        var art = new CustomMarkingArt();
        Assert.That(art.HasErase(), Is.False);
        Assert.That(art.SetErased(CustomMarkingArt.East, 1, 2, true), Is.True);
        Assert.That(art.SetErased(CustomMarkingArt.East, 1, 2, true), Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(art.IsErased(CustomMarkingArt.East, 1, 2), Is.True);
            Assert.That(art.IsErased(CustomMarkingArt.West, 1, 2), Is.False);
            Assert.That(art.HasErase(), Is.True);
            Assert.That(art.IsBlank(), Is.True, "erasing draws nothing");
            Assert.That(art.Same(new CustomMarkingArt()), Is.False);
            Assert.That(art.SamePixels(new CustomMarkingArt()), Is.True);
            Assert.That(art.Fingerprint(), Is.Not.EqualTo(new CustomMarkingArt().Fingerprint()));
            Assert.That(art.Clone().IsErased(CustomMarkingArt.East, 1, 2), Is.True);
        });

        // The mask is moved, flipped and copied like a facing of the art, and on its own.
        art.SetPixel(0, CustomMarkingArt.East, 1, 2, Red);
        art.ShiftErase(CustomMarkingArt.East, 2, 1);
        Assert.Multiple(() =>
        {
            Assert.That(art.IsErased(CustomMarkingArt.East, 3, 3), Is.True);
            Assert.That(art.IsErased(CustomMarkingArt.East, 1, 2), Is.False);
            Assert.That(art.GetPixel(0, CustomMarkingArt.East, 1, 2), Is.EqualTo(Red), "the art stays where it was");
        });

        art.FlipErase(CustomMarkingArt.East, 30);
        Assert.That(art.IsErased(CustomMarkingArt.East, 27, 3), Is.True);

        art.CopyErase(CustomMarkingArt.East, CustomMarkingArt.West, true);
        Assert.Multiple(() =>
        {
            Assert.That(art.IsErased(CustomMarkingArt.West, Frame - 1 - 27, 3), Is.True);
            Assert.That(art.IsErased(CustomMarkingArt.East, 27, 3), Is.True, "the source is left alone");
        });

        art.ClearErase(CustomMarkingArt.East);
        Assert.Multiple(() =>
        {
            Assert.That(art.IsErased(CustomMarkingArt.East, 27, 3), Is.False);
            Assert.That(art.IsErased(CustomMarkingArt.West, Frame - 1 - 27, 3), Is.True, "other facings keep theirs");
        });

        // Art that is restored takes the other's frames, their times and its mask.
        var other = new CustomMarkingArt();
        other.AddFrame(0);
        other.SetFrameTime(1, 700);
        art.Restore(other);
        Assert.That(art.Same(other), Is.True);
        Assert.That(art.HasErase(), Is.False);
    }

    /// <summary>What art hides of the body under it: the pixels it draws solidly in every frame.</summary>
    [Test]
    public void SolidTest()
    {
        var art = new CustomMarkingArt();
        art.SetPixel(0, CustomMarkingArt.South, 1, 1, Red);
        art.SetPixel(0, CustomMarkingArt.South, 2, 2, new Rgba32(0, 0, 255, 127));
        art.SetPixel(0, CustomMarkingArt.South, 3, 3, new Rgba32(0, 0, 255, 128));

        var solid = art.Solid();
        Assert.Multiple(() =>
        {
            Assert.That(CustomMarkingErase.Get(solid, CustomMarkingArt.South, 1, 1), Is.True);
            Assert.That(CustomMarkingErase.Get(solid, CustomMarkingArt.South, 2, 2), Is.False, "less than half opaque shows what is under it");
            Assert.That(CustomMarkingErase.Get(solid, CustomMarkingArt.South, 3, 3), Is.True);
            Assert.That(CustomMarkingErase.Get(solid, CustomMarkingArt.South, 0, 0), Is.False);
            Assert.That(CustomMarkingErase.Get(solid, CustomMarkingArt.North, 1, 1), Is.False);
        });

        // Solid in one frame and clear in the next leaves the body bare half the time.
        art.AddFrame(0);
        art.SetPixel(1, CustomMarkingArt.South, 1, 1, default);
        solid = art.Solid();
        Assert.That(CustomMarkingErase.Get(solid, CustomMarkingArt.South, 1, 1), Is.False);
        Assert.That(CustomMarkingErase.Get(solid, CustomMarkingArt.South, 3, 3), Is.True);
    }

    /// <summary>Cutting art to a body takes a pixel out of every frame, and only asks about those some frame draws.</summary>
    [Test]
    public void WithoutFramesTest()
    {
        var art = new CustomMarkingArt();
        art.AddFrame(0);
        art.SetPixel(0, CustomMarkingArt.South, 1, 1, Red);
        art.SetPixel(1, CustomMarkingArt.South, 1, 1, Blue);
        art.SetPixel(1, CustomMarkingArt.South, 4, 4, Blue);
        art.SetFrameTime(1, 800);
        art.SetErased(CustomMarkingArt.South, 9, 9, true);

        var asked = 0;
        var cut = art.Without((_, x, _) =>
        {
            asked++;
            return x == 1;
        });

        Assert.Multiple(() =>
        {
            Assert.That(asked, Is.EqualTo(2), "once for each place drawn on, in whichever frame");
            Assert.That(cut.Frames, Is.EqualTo(2));
            Assert.That(cut.GetPixel(0, CustomMarkingArt.South, 1, 1).A, Is.Zero);
            Assert.That(cut.GetPixel(1, CustomMarkingArt.South, 1, 1).A, Is.Zero);
            Assert.That(cut.GetPixel(1, CustomMarkingArt.South, 4, 4), Is.EqualTo(Blue));
            Assert.That(cut.GetFrameTime(1), Is.EqualTo(800), "the frames keep their times");
            Assert.That(cut.IsErased(CustomMarkingArt.South, 9, 9), Is.True);
        });
    }

    private static byte[] Png(Image image)
    {
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }
}
