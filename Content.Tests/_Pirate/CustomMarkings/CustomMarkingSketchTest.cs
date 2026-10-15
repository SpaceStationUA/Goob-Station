// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Pirate.CustomMarkings;
using Content.Shared._Pirate.CustomMarkings;
using NUnit.Framework;
using Robust.Shared.Maths;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Tests._Pirate.CustomMarkings;

/// <summary>The editor's strokes and its undo and redo steps.</summary>
[TestFixture]
[TestOf(typeof(CustomMarkingSketch))]
public sealed class CustomMarkingSketchTest
{
    private const int South = CustomMarkingArt.South;

    private static readonly Rgba32 Red = new(255, 0, 0, 255);
    private static readonly Rgba32 Blue = new(0, 0, 255, 255);

    [Test]
    public void StrokeIsOneStepTest()
    {
        var opened = new CustomMarkingArt();
        var sketch = new CustomMarkingSketch(opened);
        Assert.That(sketch.CanUndo, Is.False);
        Assert.That(sketch.CanRedo, Is.False);

        // One drag: three moves, one step.
        sketch.Begin();
        sketch.Line(0, South, new Vector2i(0, 0), new Vector2i(0, 0), Red);
        sketch.Line(0, South, new Vector2i(0, 0), new Vector2i(5, 0), Red);
        sketch.Line(0, South, new Vector2i(5, 0), new Vector2i(5, 5), Red);
        sketch.End();

        Assert.Multiple(() =>
        {
            Assert.That(sketch.Art.GetPixel(0, South, 3, 0), Is.EqualTo(Red), "a fast drag leaves no gaps");
            Assert.That(sketch.Art.GetPixel(0, South, 5, 3), Is.EqualTo(Red));
            Assert.That(opened.IsBlank(), Is.True, "the art it was opened with is left alone");
            Assert.That(sketch.CanUndo, Is.True);
        });

        sketch.Undo();
        Assert.That(sketch.Art.IsBlank(), Is.True);
        Assert.That(sketch.CanUndo, Is.False);
        Assert.That(sketch.CanRedo, Is.True);

        sketch.Redo();
        Assert.That(sketch.Art.GetPixel(0, South, 5, 5), Is.EqualTo(Red));
        Assert.That(sketch.CanRedo, Is.False);
    }

    [Test]
    public void DiagonalAndOffCanvasTest()
    {
        var sketch = new CustomMarkingSketch(new CustomMarkingArt());
        sketch.Begin();
        sketch.Line(0, South, new Vector2i(-4, -4), new Vector2i(3, 3), Red);
        sketch.Line(0, South, new Vector2i(30, 10), new Vector2i(900, 10), Red);
        sketch.End();

        Assert.Multiple(() =>
        {
            Assert.That(sketch.Art.GetPixel(0, South, 0, 0), Is.EqualTo(Red));
            Assert.That(sketch.Art.GetPixel(0, South, 2, 2), Is.EqualTo(Red));
            Assert.That(sketch.Art.GetPixel(0, South, 1, 0).A, Is.Zero);
            Assert.That(sketch.Art.GetPixel(0, South, 31, 10), Is.EqualTo(Red), "the part inside the facing is drawn");
            Assert.That(sketch.Art.GetPixel(0, CustomMarkingArt.North, 0, 10).A, Is.Zero, "and nothing spills onto the next facing");
        });
    }

    [Test]
    public void StepsTest()
    {
        var sketch = new CustomMarkingSketch(new CustomMarkingArt());

        // A step that changes nothing isn't kept.
        sketch.Begin();
        sketch.End();
        sketch.Change(art => art.Clear(0, South));
        sketch.End();
        Assert.That(sketch.CanUndo, Is.False);

        sketch.Change(art => art.SetPixel(0, South, 1, 1, Red));
        sketch.Change(art => art.SetPixel(0, South, 2, 2, Blue));
        sketch.Undo();
        Assert.That(sketch.Art.GetPixel(0, South, 2, 2).A, Is.Zero);
        Assert.That(sketch.Art.GetPixel(0, South, 1, 1), Is.EqualTo(Red));

        // Drawing after an undo forgets what could have been redone.
        sketch.Change(art => art.SetPixel(0, South, 3, 3, Blue));
        Assert.That(sketch.CanRedo, Is.False);
        sketch.Redo();
        Assert.That(sketch.Art.GetPixel(0, South, 2, 2).A, Is.Zero);

        sketch.Undo();
        sketch.Undo();
        Assert.That(sketch.Art.IsBlank(), Is.True);
        sketch.Undo();
        Assert.That(sketch.Art.IsBlank(), Is.True, "undoing past the start does nothing");

        // An undo in the middle of a stroke closes it first, so it can be redone.
        sketch.Begin();
        sketch.Line(0, South, new Vector2i(9, 9), new Vector2i(9, 9), Red);
        sketch.Undo();
        Assert.That(sketch.Art.GetPixel(0, South, 9, 9).A, Is.Zero);
        sketch.Redo();
        Assert.That(sketch.Art.GetPixel(0, South, 9, 9), Is.EqualTo(Red));
    }

    [Test]
    public void MirrorTest()
    {
        // The mirror stands between the two middle columns here, so column x mirrors to last - x.
        const int last = CustomMarkingRules.FrameSize - 1;
        var sketch = new CustomMarkingSketch(new CustomMarkingArt()) { Mirror = true, MirrorAxis = last };

        sketch.Begin();
        sketch.Line(0, South, new Vector2i(2, 5), new Vector2i(6, 9), Red);
        sketch.End();

        Assert.Multiple(() =>
        {
            Assert.That(sketch.Art.GetPixel(0, South, 2, 5), Is.EqualTo(Red));
            Assert.That(sketch.Art.GetPixel(0, South, 4, 7), Is.EqualTo(Red));
            Assert.That(sketch.Art.GetPixel(0, South, last - 2, 5), Is.EqualTo(Red), "the stroke is repeated across the middle");
            Assert.That(sketch.Art.GetPixel(0, South, last - 4, 7), Is.EqualTo(Red));
            Assert.That(sketch.Art.GetPixel(0, South, last - 6, 9), Is.EqualTo(Red));
            Assert.That(sketch.Art.GetPixel(0, CustomMarkingArt.North, last - 2, 5).A, Is.Zero, "only on the facing drawn on");
            Assert.That(sketch.MirrorX(0), Is.EqualTo(last));
            Assert.That(sketch.MirrorX(15), Is.EqualTo(16));
        });

        // Both halves are one step.
        sketch.Undo();
        Assert.That(sketch.Art.IsBlank(), Is.True);
        sketch.Redo();
        Assert.That(sketch.Art.GetPixel(0, South, last - 2, 5), Is.EqualTo(Red));

        // Erasing mirrors as drawing does.
        sketch.Begin();
        sketch.Line(0, South, new Vector2i(2, 5), new Vector2i(2, 5), default);
        sketch.End();
        Assert.That(sketch.Art.GetPixel(0, South, 2, 5).A, Is.Zero);
        Assert.That(sketch.Art.GetPixel(0, South, last - 2, 5).A, Is.Zero);

        // A box drawn on one side is drawn on both, and filling one fills the other.
        var boxes = new CustomMarkingSketch(new CustomMarkingArt()) { Mirror = true, MirrorAxis = last };
        boxes.Begin();
        boxes.Line(0, South, new Vector2i(3, 3), new Vector2i(7, 3), Red);
        boxes.Line(0, South, new Vector2i(7, 3), new Vector2i(7, 7), Red);
        boxes.Line(0, South, new Vector2i(7, 7), new Vector2i(3, 7), Red);
        boxes.Line(0, South, new Vector2i(3, 7), new Vector2i(3, 3), Red);
        boxes.Fill(0, South, 5, 5, Blue);
        boxes.End();

        Assert.Multiple(() =>
        {
            Assert.That(boxes.Art.GetPixel(0, South, 5, 5), Is.EqualTo(Blue));
            Assert.That(boxes.Art.GetPixel(0, South, last - 5, 5), Is.EqualTo(Blue));
            Assert.That(boxes.Art.GetPixel(0, South, last - 3, 3), Is.EqualTo(Red), "the outline across the middle stays");
            Assert.That(boxes.Art.GetPixel(0, South, 16, 5).A, Is.Zero, "nothing between the boxes is filled");
        });

        // Switched off, a stroke stays where it is drawn.
        var plain = new CustomMarkingSketch(new CustomMarkingArt());
        plain.Begin();
        plain.Line(0, South, new Vector2i(2, 5), new Vector2i(2, 5), Red);
        plain.Fill(0, South, 20, 20, Blue);
        plain.End();
        Assert.That(plain.Art.GetPixel(0, South, last - 2, 5), Is.EqualTo(Blue), "the fill reached it, the stroke did not");
        Assert.That(plain.Art.GetPixel(0, South, 2, 5), Is.EqualTo(Red));
    }

    /// <summary>A body's front is an odd number of pixels wide, so by default the mirror stands on its middle column.</summary>
    [Test]
    public void MirrorOnMiddleColumnTest()
    {
        var sketch = new CustomMarkingSketch(new CustomMarkingArt()) { Mirror = true };
        Assert.That(sketch.MirrorAxis, Is.EqualTo(CustomMarkingSketch.DefaultMirrorAxis));

        sketch.Begin();
        sketch.Line(0, South, new Vector2i(10, 4), new Vector2i(10, 4), Red);
        sketch.Line(0, South, new Vector2i(15, 6), new Vector2i(15, 6), Red);
        sketch.Line(0, South, new Vector2i(31, 8), new Vector2i(31, 8), Red);
        sketch.End();

        Assert.Multiple(() =>
        {
            Assert.That(sketch.Art.GetPixel(0, South, 10, 4), Is.EqualTo(Red));
            Assert.That(sketch.Art.GetPixel(0, South, 20, 4), Is.EqualTo(Red), "the torso's left edge mirrors to its right edge");
            Assert.That(sketch.Art.GetPixel(0, South, 21, 4).A, Is.Zero);
            Assert.That(sketch.Art.GetPixel(0, South, 15, 6), Is.EqualTo(Red), "the middle column mirrors to itself");
            Assert.That(sketch.Art.GetPixel(0, South, 14, 6).A, Is.Zero);
            Assert.That(sketch.Art.GetPixel(0, South, 16, 6).A, Is.Zero);
            Assert.That(sketch.Art.GetPixel(0, South, 31, 8), Is.EqualTo(Red), "a pixel whose mirror image is off the facing is still drawn");
            Assert.That(sketch.MirrorX(31), Is.EqualTo(-1));
        });

        // A fill whose mirror image is off the facing fills only where it was asked to.
        Assert.DoesNotThrow(() => sketch.Change(_ => sketch.Fill(0, South, 31, 31, Blue)));
        Assert.That(sketch.Art.GetPixel(0, South, 0, 31), Is.EqualTo(Blue));
    }

    /// <summary>Drawing only adds where it is allowed, here the left half of the facing; erasing reaches everywhere.</summary>
    [Test]
    public void ReachTest()
    {
        var sketch = new CustomMarkingSketch(new CustomMarkingArt()) { Allowed = (_, x, _) => x < 16 };

        sketch.Begin();
        sketch.Line(0, South, new Vector2i(10, 5), new Vector2i(20, 5), Red);
        sketch.End();
        Assert.That(sketch.Art.GetPixel(0, South, 15, 5), Is.EqualTo(Red));
        Assert.That(sketch.Art.GetPixel(0, South, 16, 5).A, Is.Zero, "the stroke stops where reach does");

        // A fill stays within reach, and one started out of reach does nothing.
        sketch.Change(_ => sketch.Fill(0, South, 0, 0, Blue));
        Assert.Multiple(() =>
        {
            Assert.That(sketch.Art.GetPixel(0, South, 15, 20), Is.EqualTo(Blue));
            Assert.That(sketch.Art.GetPixel(0, South, 16, 20).A, Is.Zero);
            Assert.That(sketch.Art.GetPixel(0, South, 15, 5), Is.EqualTo(Red));
        });

        sketch.Change(_ => sketch.Fill(0, South, 20, 20, Red));
        Assert.That(sketch.Art.GetPixel(0, South, 20, 20).A, Is.Zero);

        // The mirror image of a stroke is held to the same reach.
        var mirrored = new CustomMarkingSketch(new CustomMarkingArt()) { Mirror = true, Allowed = (_, x, _) => x < 16 };
        mirrored.Begin();
        mirrored.Line(0, South, new Vector2i(10, 5), new Vector2i(10, 5), Red);
        mirrored.End();
        Assert.That(mirrored.Art.GetPixel(0, South, 10, 5), Is.EqualTo(Red));
        Assert.That(mirrored.Art.GetPixel(0, South, 20, 5).A, Is.Zero);

        // Art drawn for another body can lie out of reach; it can still be rubbed out.
        var other = new CustomMarkingArt();
        other.SetPixel(0, South, 25, 5, Red);
        other.SetPixel(0, South, 26, 5, Red);
        var loaded = new CustomMarkingSketch(other) { Allowed = (_, x, _) => x < 16 };
        loaded.Begin();
        loaded.Line(0, South, new Vector2i(25, 5), new Vector2i(25, 5), default);
        loaded.End();
        Assert.That(loaded.Art.GetPixel(0, South, 25, 5).A, Is.Zero);
        loaded.Change(_ => loaded.Fill(0, South, 26, 5, default));
        Assert.That(loaded.Art.GetPixel(0, South, 26, 5).A, Is.Zero);
    }

    /// <summary>The body eraser draws on the erase mask the way the pencil draws on the art.</summary>
    [Test]
    public void EraseLineTest()
    {
        var sketch = new CustomMarkingSketch(new CustomMarkingArt()) { Mirror = true, Allowed = (_, x, _) => x < 20 };
        sketch.Begin();
        sketch.EraseLine(South, new Vector2i(10, 4), new Vector2i(12, 4), true);
        sketch.End();

        Assert.Multiple(() =>
        {
            Assert.That(sketch.Art.IsErased(South, 10, 4), Is.True);
            Assert.That(sketch.Art.IsErased(South, 12, 4), Is.True);
            Assert.That(sketch.Art.IsErased(South, 18, 4), Is.True, "mirrored about column 15");
            Assert.That(sketch.Art.IsErased(South, 19, 4), Is.True);
            Assert.That(sketch.Art.IsErased(South, 20, 4), Is.False, "the body is only erased where drawing reaches");
            Assert.That(sketch.Art.IsBlank(), Is.True, "erasing the body draws nothing");
            Assert.That(sketch.CanUndo, Is.True);
        });

        // Bringing the body back reaches everywhere: a mask made on another body can lie out of this one's reach.
        sketch.Art.SetErased(South, 25, 9, true);
        sketch.Mirror = false;
        sketch.Change(_ => sketch.EraseLine(South, new Vector2i(25, 9), new Vector2i(25, 9), false));
        Assert.That(sketch.Art.IsErased(South, 25, 9), Is.False);

        sketch.Undo();
        Assert.That(sketch.Art.IsErased(South, 25, 9), Is.True);
        sketch.Undo();
        Assert.That(sketch.Art.IsErased(South, 10, 4), Is.False, "a stroke of the body eraser is undone like any other");
    }

    /// <summary>Steps cover the whole marking: its frames, how long they show and what it erases.</summary>
    [Test]
    public void FrameStepsTest()
    {
        var sketch = new CustomMarkingSketch(new CustomMarkingArt());
        sketch.Change(art => art.SetPixel(0, South, 1, 1, Red));
        sketch.Change(art => art.AddFrame(0));
        sketch.Begin();
        sketch.Line(1, South, new Vector2i(5, 5), new Vector2i(5, 5), Blue);
        sketch.End();
        sketch.Change(art => art.SetFrameTime(1, 900));

        Assert.Multiple(() =>
        {
            Assert.That(sketch.Art.Frames, Is.EqualTo(2));
            Assert.That(sketch.Art.GetPixel(1, South, 5, 5), Is.EqualTo(Blue));
            Assert.That(sketch.Art.GetPixel(0, South, 5, 5).A, Is.Zero, "a stroke goes on one frame");
            Assert.That(sketch.Art.GetFrameTime(1), Is.EqualTo(900));
        });

        sketch.Undo();
        Assert.That(sketch.Art.GetFrameTime(1), Is.EqualTo(CustomMarkingRules.DefaultFrameTime));
        sketch.Undo();
        Assert.That(sketch.Art.GetPixel(1, South, 5, 5).A, Is.Zero);
        sketch.Undo();
        Assert.That(sketch.Art.Frames, Is.EqualTo(1), "adding a frame is a step too");

        sketch.Redo();
        sketch.Redo();
        Assert.That(sketch.Art.Frames, Is.EqualTo(2));
        Assert.That(sketch.Art.GetPixel(1, South, 5, 5), Is.EqualTo(Blue));

        // Setting a frame's time to what it already is changes nothing, so it is no step.
        sketch.Change(art => art.SetFrameTime(0, art.GetFrameTime(0)));
        Assert.That(sketch.CanRedo, Is.True, "and what could be redone still can");

        var steps = 0;
        while (sketch.CanUndo)
        {
            sketch.Undo();
            steps++;
        }

        Assert.That(steps, Is.EqualTo(3));
    }

    [Test]
    public void StepLimitTest()
    {
        var sketch = new CustomMarkingSketch(new CustomMarkingArt());
        for (var i = 0; i < CustomMarkingSketch.MaxSteps + 10; i++)
        {
            var x = i % CustomMarkingRules.FrameSize;
            var y = i / CustomMarkingRules.FrameSize;
            sketch.Change(art => art.SetPixel(0, South, x, y, Red));
        }

        var undone = 0;
        while (sketch.CanUndo)
        {
            sketch.Undo();
            undone++;
        }

        Assert.That(undone, Is.EqualTo(CustomMarkingSketch.MaxSteps));
        Assert.That(sketch.Art.GetPixel(0, South, 9, 0), Is.EqualTo(Red), "the oldest steps can no longer be undone");
        Assert.That(sketch.Art.GetPixel(0, South, 10, 0).A, Is.Zero);
    }
}
