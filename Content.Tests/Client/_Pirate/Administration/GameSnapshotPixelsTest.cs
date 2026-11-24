// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.IO.Compression;
using Content.Client._Pirate.Administration.GameSnapshot;
using Content.Shared._Pirate.Administration.GameSnapshot;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Tests.Client._Pirate.Administration;

[TestFixture]
public sealed class GameSnapshotPixelsTest
{
    [TestCase(1920, 1080, 1280, 720)]
    [TestCase(3440, 1440, 1280, 536)]
    [TestCase(900, 1600, 405, 720)]
    [TestCase(601, 319, 601, 319)]
    public void FrameFitsPreviewAndPreservesAllFourCorners(int width, int height, int expectedWidth, int expectedHeight)
    {
        using var source = new Image<Rgb24>(width, height);
        for (var y = 0; y < 12; y++)
        for (var x = 0; x < 12; x++)
        {
            source[x, y] = new Rgb24(255, 255, 0);
            source[width - 1 - x, y] = new Rgb24(0, 255, 255);
            source[x, height - 1 - y] = new Rgb24(255, 0, 255);
            source[width - 1 - x, height - 1 - y] = new Rgb24(0, 0, 255);
        }

        var frame = GameSnapshotPixels.Encode(source);
        using var result = GameSnapshotPixels.Decode(frame);

        Assert.That((result.Width, result.Height), Is.EqualTo((expectedWidth, expectedHeight)));
        Assert.That(result[0, 0], Is.EqualTo(new Rgba32(255, 255, 0)));
        Assert.That(result[result.Width - 1, 0], Is.EqualTo(new Rgba32(0, 255, 255)));
        Assert.That(result[0, result.Height - 1], Is.EqualTo(new Rgba32(255, 0, 255)));
        Assert.That(result[result.Width - 1, result.Height - 1], Is.EqualTo(new Rgba32(0, 0, 255)));
    }

    [TestCase(1)]
    [TestCase(3)]
    public void FrameMustInflateToExactlyTheDeclaredPixelCount(int byteCount)
    {
        using var packed = new MemoryStream();
        using (var deflate = new DeflateStream(packed, CompressionLevel.Fastest, true))
            deflate.Write(new byte[byteCount], 0, byteCount);

        var frame = new GameSnapshotFrame(1, 1, packed.ToArray());
        Assert.That(() => GameSnapshotPixels.Decode(frame), Throws.Exception);
    }

    [Test]
    public void OnlyCompleteBoundedChunksCanStartOrContinueAFrame()
    {
        const int size = GameSnapshotFrame.ChunkBytes;
        Assert.That(GameSnapshotFrame.ValidChunk(1280, 720, size + 1, 0, size), Is.True);
        Assert.That(GameSnapshotFrame.ValidChunk(1280, 720, size + 1, size, 1), Is.True);
        Assert.That(GameSnapshotFrame.ValidChunk(1280, 720, size + 1, 1, size), Is.False);
        Assert.That(GameSnapshotFrame.ValidChunk(1280, 720, size, 0, size - 1), Is.False);
        Assert.That(GameSnapshotFrame.ValidChunk(1, 1, size, 0, size), Is.False);
        Assert.That(GameSnapshotFrame.ValidChunk(1280, 721, size, 0, size), Is.False);
    }
}
