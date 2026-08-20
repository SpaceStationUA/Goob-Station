using System.IO;
using System.Linq;
using System.Text.Json;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Pirate.Server.CustomGhostSpriteSystem;

public sealed class GhostSpriteMeasurer
{
    private static readonly ResPath TextureRoot = new("/Textures");

    private readonly IResourceManager _resourceManager;

    private readonly Dictionary<(string Rsi, string? State), int> _contentEdges = new();
    private readonly Dictionary<string, RsiMeta?> _metas = new();

    public GhostSpriteMeasurer(IResourceManager resourceManager)
    {
        _resourceManager = resourceManager;
    }

    public float GetScale(ResPath rsi, string? state, float maxSize, int maxSquare)
    {
        if (maxSize <= 0f || maxSquare <= 0)
            return 1f;

        var limit = maxSquare * maxSize;
        float actual;

        if (TryGetContentEdge(rsi, state, out var contentEdge))
        {
            actual = contentEdge;
        }
        else if (TryGetMeta(rsi) is { } meta)
        {
            // Якщо пікселі недоступні, використовуємо розмір кадру RSI.
            actual = Math.Max(meta.Width, meta.Height);
        }
        else
        {
            return 1f;
        }

        if (actual <= 0f || actual <= limit)
            return 1f;

        return limit / actual;
    }

    private bool TryGetContentEdge(ResPath rsi, string? state, out int edge)
    {
        var key = (rsi.ToString(), state);
        if (_contentEdges.TryGetValue(key, out edge))
            return edge > 0;

        edge = 0;

        if (TryGetMeta(rsi) is not { } meta || meta.Width <= 0 || meta.Height <= 0)
        {
            _contentEdges[key] = 0;
            return false;
        }

        var frameWidth = meta.Width;
        var frameHeight = meta.Height;
        var states = state != null ? new[] { state } : meta.States;

        var maxWidth = 0;
        var maxHeight = 0;

        foreach (var stateName in states)
        {
            MeasureFile(TextureRoot / rsi / $"{stateName}.png", frameWidth, frameHeight, ref maxWidth, ref maxHeight);
        }

        // У зібраних збірках RSI може бути атласом .rsic.
        if (maxWidth == 0 && maxHeight == 0)
            MeasureFile((TextureRoot / rsi).WithExtension("rsic"), frameWidth, frameHeight, ref maxWidth, ref maxHeight);

        edge = Math.Max(maxWidth, maxHeight);
        _contentEdges[key] = edge;
        return edge > 0;
    }

    private void MeasureFile(ResPath path, int frameWidth, int frameHeight, ref int maxWidth, ref int maxHeight)
    {
        if (!_resourceManager.ContentFileExists(path))
            return;

        try
        {
            using var stream = _resourceManager.ContentFileRead(path);
            using var image = Image.Load<Rgba32>(stream);
            MeasureSheet(image, frameWidth, frameHeight, ref maxWidth, ref maxHeight);
        }
        catch (Exception)
        {
        }
    }

    private static void MeasureSheet(Image<Rgba32> image, int frameWidth, int frameHeight, ref int maxWidth, ref int maxHeight)
    {
        var columns = image.Width / frameWidth;
        var rows = image.Height / frameHeight;

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var left = int.MaxValue;
                var right = -1;
                var top = int.MaxValue;
                var bottom = -1;

                for (var y = 0; y < frameHeight; y++)
                {
                    for (var x = 0; x < frameWidth; x++)
                    {
                        if (image[column * frameWidth + x, row * frameHeight + y].A == 0)
                            continue;

                        if (x < left)
                            left = x;
                        if (x > right)
                            right = x;
                        if (y < top)
                            top = y;
                        if (y > bottom)
                            bottom = y;
                    }
                }

                if (right < 0)
                    continue;

                maxWidth = Math.Max(maxWidth, right - left + 1);
                maxHeight = Math.Max(maxHeight, bottom - top + 1);
            }
        }
    }

    private RsiMeta? TryGetMeta(ResPath rsi)
    {
        var key = rsi.ToString();
        if (_metas.TryGetValue(key, out var cached))
            return cached;

        RsiMeta? meta = null;

        try
        {
            var path = TextureRoot / rsi / "meta.json";
            if (_resourceManager.ContentFileExists(path))
            {
                using var stream = _resourceManager.ContentFileRead(path);
                using var document = JsonDocument.Parse(stream);
                var root = document.RootElement;

                if (root.TryGetProperty("size", out var size)
                    && size.TryGetProperty("x", out var x)
                    && size.TryGetProperty("y", out var y))
                {
                    var states = root.TryGetProperty("states", out var statesElement)
                                 && statesElement.ValueKind == JsonValueKind.Array
                        ? statesElement.EnumerateArray()
                            .Where(e => e.ValueKind == JsonValueKind.Object && e.TryGetProperty("name", out _))
                            .Select(e => e.GetProperty("name").GetString())
                            .Where(s => !string.IsNullOrEmpty(s))
                            .Select(s => s!)
                            .ToArray()
                        : Array.Empty<string>();

                    meta = new RsiMeta(x.GetInt32(), y.GetInt32(), states);
                }
            }
        }
        catch (Exception)
        {
        }

        _metas[key] = meta;
        return meta;
    }

    private sealed record RsiMeta(int Width, int Height, string[] States);
}
