// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text;
using Content.Shared._Pirate.CustomMarkings;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Client._Pirate.CustomMarkings;

/// <summary>
/// A content root holding fetched custom marking art as one RSI per hash, so a sprite layer can draw it, facings,
/// frames and all, like any shipped marking. What an RSI can't hold, the art's erase mask, is kept beside it.
/// </summary>
/// <remarks>
/// Roots can only ever be added to <see cref="IResourceManager"/>, never removed, so this mounts once per resource
/// manager and outlives the entity systems, which are rebuilt on reconnect. The manager owns the mounted root; the
/// static registry only holds weak references.
/// </remarks>
public sealed class CustomMarkingResources : IContentRoot, IDisposable
{
    /// <summary>Where the root is mounted. Nothing ships under this path, so it can't shadow a real resource.</summary>
    public static readonly ResPath Prefix = ResPath.Root / "WFCustomMarkings";

    /// <summary>The one state of every RSI here.</summary>
    public const string State = "art";

    private const string TimesFile = "times.bin";
    private const string EraseFile = "erase.bin";

    private static readonly List<WeakReference<CustomMarkingResources>> Mounted = new();

    private readonly WeakReference<IResourceManager> _owner;

    private readonly MemoryContentRoot _root = new();

    private CustomMarkingResources(IResourceManager resources)
    {
        _owner = new WeakReference<IResourceManager>(resources);
        resources.AddRoot(Prefix, this);
    }

    /// <summary>The root for this resource manager, mounting it on first use.</summary>
    public static CustomMarkingResources For(IResourceManager resources)
    {
        lock (Mounted)
        {
            for (var i = Mounted.Count - 1; i >= 0; i--)
            {
                if (!Mounted[i].TryGetTarget(out var existing) || !existing._owner.TryGetTarget(out var owner))
                {
                    Mounted.RemoveAt(i);
                    continue;
                }

                if (ReferenceEquals(owner, resources))
                    return existing;
            }

            var created = new CustomMarkingResources(resources);
            Mounted.Add(new WeakReference<CustomMarkingResources>(created));
            return created;
        }
    }

    void IContentRoot.Mount() => _root.Mount();
    bool IContentRoot.TryGetFile(ResPath path, [NotNullWhen(true)] out Stream? stream) => _root.TryGetFile(path, out stream);
    bool IContentRoot.FileExists(ResPath path) => _root.FileExists(path);
    IEnumerable<ResPath> IContentRoot.FindFiles(ResPath path) => _root.FindFiles(path);
    IEnumerable<string> IContentRoot.GetRelativeFilePaths() => _root.GetRelativeFilePaths();
    public void Dispose() => _root.Dispose();

    /// <summary>Full path of the RSI stored under a name: an art hash, or a name made up for art derived from it.</summary>
    public static ResPath PathFor(string name)
    {
        return Prefix / Relative(name);
    }

    private static ResPath Relative(string name)
    {
        return new ResPath($"{name}.rsi");
    }

    /// <summary>
    /// Puts art under a name: a PNG sheet of its frames, as <see cref="CustomMarkingArt.ToPng"/> lays them out, how
    /// long each frame shows and its erase mask.
    /// </summary>
    /// <param name="size">The size of one facing, for a sheet that isn't a body sprite's.</param>
    public void Store(string name, byte[] png, int[]? frameTimes = null, byte[]? erase = null, Vector2i? size = null)
    {
        var rsi = Relative(name);
        _root.AddOrUpdateFile(rsi / $"{State}.png", png);

        if (frameTimes != null && CustomMarkingRules.PackFrameTimes(frameTimes) is { } times)
            _root.AddOrUpdateFile(rsi / TimesFile, times);
        else
            _root.RemoveFile(rsi / TimesFile);

        if (erase is { Length: CustomMarkingRules.EraseBytes } && CustomMarkingErase.Any(erase))
            _root.AddOrUpdateFile(rsi / EraseFile, erase.AsSpan().ToArray());
        else
            _root.RemoveFile(rsi / EraseFile);

        // The manifest goes in last: it is what makes the RSI loadable.
        var frame = size ?? new Vector2i(CustomMarkingRules.FrameSize, CustomMarkingRules.FrameSize);
        _root.AddOrUpdateFile(rsi / "meta.json", Meta(frame, frameTimes));
    }

    private static byte[] Meta(Vector2i size, int[]? frameTimes)
    {
        var meta = new StringBuilder();
        meta.Append("{\"version\":1,\"license\":\"CC-BY-SA-3.0\",\"copyright\":\"Drawn by a player\",\"size\":{\"x\":")
            .Append(size.X)
            .Append(",\"y\":")
            .Append(size.Y)
            .Append("},\"states\":[{\"name\":\"")
            .Append(State)
            .Append("\",\"directions\":")
            .Append(CustomMarkingRules.Facings);

        // An animated state lists its frames' times, in seconds, once for each facing.
        if (frameTimes is { Length: > 1 })
        {
            meta.Append(",\"delays\":[");
            for (var facing = 0; facing < CustomMarkingRules.Facings; facing++)
            {
                meta.Append(facing == 0 ? "[" : ",[");
                for (var i = 0; i < frameTimes.Length; i++)
                {
                    if (i > 0)
                        meta.Append(',');

                    meta.Append((frameTimes[i] / 1000f).ToString(CultureInfo.InvariantCulture));
                }

                meta.Append(']');
            }

            meta.Append(']');
        }

        meta.Append("}]}");
        return Encoding.UTF8.GetBytes(meta.ToString());
    }

    /// <summary>Forgets what is under a name. The resource cache may still hold its RSI, so check <see cref="Has"/> before loading.</summary>
    public void Remove(string name)
    {
        var rsi = Relative(name);
        _root.RemoveFile(rsi / "meta.json");
        _root.RemoveFile(rsi / $"{State}.png");
        _root.RemoveFile(rsi / TimesFile);
        _root.RemoveFile(rsi / EraseFile);
    }

    public bool Has(string name)
    {
        return _root.FileExists(Relative(name) / "meta.json");
    }

    /// <summary>The stored PNG sheet, for the editor to open or export.</summary>
    public bool TryGetPng(string name, [NotNullWhen(true)] out byte[]? png)
    {
        return TryRead(Relative(name) / $"{State}.png", out png);
    }

    /// <summary>How long each frame of stored art shows, in milliseconds. Null for a still marking.</summary>
    public int[]? GetFrameTimes(string name)
    {
        return TryRead(Relative(name) / TimesFile, out var packed) ? CustomMarkingRules.UnpackFrameTimes(packed) : null;
    }

    /// <summary>The erase mask of stored art. False when it has none.</summary>
    public bool TryGetErase(string name, [NotNullWhen(true)] out byte[]? erase)
    {
        return TryRead(Relative(name) / EraseFile, out erase) && erase.Length == CustomMarkingRules.EraseBytes;
    }

    private bool TryRead(ResPath path, [NotNullWhen(true)] out byte[]? bytes)
    {
        bytes = null;
        if (!_root.TryGetFile(path, out var stream))
            return false;

        using (stream)
        {
            bytes = stream.CopyToArray();
        }

        return true;
    }
}
