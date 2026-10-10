// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;

namespace Content.Shared._Pirate.CustomMarkings;

/// <summary>Sizes, limits and checks for custom markings, shared by the editor and the server.</summary>
public static class CustomMarkingRules
{
    /// <summary>Width and height of one facing, the size of a body sprite.</summary>
    public const int FrameSize = 32;

    /// <summary>South, north, east and west, in the order an RSI stores them.</summary>
    public const int Facings = 4;

    /// <summary>Width and height of the sheet holding the four facings, two to a row.</summary>
    public const int SheetSize = FrameSize * 2;

    /// <summary>Size of one frame's sheet as raw RGBA bytes, the form art is uploaded in.</summary>
    public const int PixelBytes = SheetSize * SheetSize * 4;

    /// <summary>Most frames a marking has, whatever the server setting says. One is a still marking.</summary>
    public const int MaxFrames = 8;

    /// <summary>
    /// The shortest a frame of an animated marking shows, in milliseconds, which keeps a marking from flashing
    /// faster than ten times a second.
    /// </summary>
    public const int MinFrameTime = 100;

    /// <summary>The longest a frame shows, in milliseconds.</summary>
    public const int MaxFrameTime = 10000;

    public const int DefaultFrameTime = 200;

    /// <summary>Size of an erase mask: a bit for each pixel of each facing.</summary>
    public const int EraseBytes = Facings * FrameSize * FrameSize / 8;

    public const int HashLength = 64;

    public const int MaxNameLength = 32;

    /// <summary>Most custom markings a body can wear, whatever the server setting says.</summary>
    public const int MaxWornCap = 16;

    /// <summary>Most hashes one art request may name.</summary>
    public const int MaxRequestedArt = 32;

    private const char WornSeparator = ',';
    private const char PlacementSeparator = ':';

    /// <summary>Whether <paramref name="hash"/> has the form of an art hash: lowercase hex of the right length.</summary>
    public static bool IsValidHash(string? hash)
    {
        if (hash is not { Length: HashLength })
            return false;

        foreach (var c in hash)
        {
            if (c is not (>= '0' and <= '9' or >= 'a' and <= 'f'))
                return false;
        }

        return true;
    }

    /// <summary>Trims a library name, drops control characters and cuts it to length. May return an empty string.</summary>
    public static string CleanName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var builder = new StringBuilder(MaxNameLength);
        foreach (var c in name.Trim())
        {
            if (char.IsControl(c))
                continue;

            builder.Append(c);
            if (builder.Length >= MaxNameLength)
                break;
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// The usable part of a worn list: well-formed hashes and known placements, each pair once, up to
    /// <paramref name="max"/>. Order is kept, as later markings draw over earlier ones.
    /// </summary>
    public static List<CustomMarking> Clean(IEnumerable<CustomMarking>? worn, int max)
    {
        var clean = new List<CustomMarking>();
        if (worn == null)
            return clean;

        max = Math.Min(max, MaxWornCap);

        foreach (var marking in worn)
        {
            if (clean.Count >= max)
                break;

            if (IsValidHash(marking.Hash) && Enum.IsDefined(marking.Placement) && !clean.Contains(marking))
                clean.Add(marking);
        }

        return clean;
    }

    /// <summary>
    /// Takes a marking off a worn list, or puts it on if there is room. Returns whether the list changed.
    /// </summary>
    public static bool Toggle(List<CustomMarking> worn, CustomMarking marking, int max)
    {
        if (worn.Remove(marking))
            return true;

        if (worn.Count >= max)
            return false;

        worn.Add(marking);
        return true;
    }

    /// <summary>
    /// Brings a worn list up to date with a library entry that was just saved: a changed entry the character
    /// wears is swapped for its new form in place, and a new entry is put on if there is room. Returns whether
    /// the list changed.
    /// </summary>
    /// <param name="before">The entry as it was, or null when it is new.</param>
    public static bool ApplySaved(List<CustomMarking> worn, CustomMarkingEntry? before, CustomMarkingEntry saved, int max)
    {
        var marking = new CustomMarking(saved.Hash, saved.Placement);
        if (before is not { } old)
        {
            if (worn.Count >= max || worn.Contains(marking))
                return false;

            worn.Add(marking);
            return true;
        }

        var at = worn.IndexOf(new CustomMarking(old.Hash, old.Placement));
        if (at < 0 || worn[at] == marking)
            return false;

        worn.RemoveAt(at);
        if (!worn.Contains(marking))
            worn.Insert(at, marking);

        return true;
    }

    /// <summary>
    /// Frame times as stored beside art: two bytes for each frame, low byte first. Null for a still marking, which
    /// has none worth keeping.
    /// </summary>
    public static byte[]? PackFrameTimes(int[] times)
    {
        if (times.Length <= 1)
            return null;

        var packed = new byte[times.Length * 2];
        for (var i = 0; i < times.Length; i++)
        {
            packed[i * 2] = (byte) times[i];
            packed[i * 2 + 1] = (byte) (times[i] >> 8);
        }

        return packed;
    }

    /// <summary>Reads <see cref="PackFrameTimes"/> bytes. Null when there are none, as for a still marking.</summary>
    public static int[]? UnpackFrameTimes(byte[]? packed)
    {
        if (packed == null || packed.Length < 4 || packed.Length % 2 != 0)
            return null;

        var times = new int[packed.Length / 2];
        for (var i = 0; i < times.Length; i++)
        {
            times[i] = packed[i * 2] | packed[i * 2 + 1] << 8;
        }

        return times;
    }

    /// <summary>A worn list as the text saved with a character: <c>hash:placement</c> pairs, comma separated.</summary>
    public static string ToStored(IReadOnlyList<CustomMarking> worn)
    {
        var builder = new StringBuilder(worn.Count * (HashLength + 3));
        foreach (var marking in worn)
        {
            if (builder.Length > 0)
                builder.Append(WornSeparator);

            builder.Append(marking.Hash).Append(PlacementSeparator).Append((int) marking.Placement);
        }

        return builder.ToString();
    }

    /// <summary>Reads <see cref="ToStored"/> text. Pairs it can't read are dropped.</summary>
    public static List<CustomMarking> FromStored(string? stored)
    {
        var worn = new List<CustomMarking>();
        if (string.IsNullOrEmpty(stored))
            return worn;

        foreach (var pair in stored.Split(WornSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var split = pair.IndexOf(PlacementSeparator);
            if (split < 0 || !int.TryParse(pair.AsSpan(split + 1), out var placement))
                continue;

            worn.Add(new CustomMarking(pair[..split], (CustomMarkingPlacement) placement));
        }

        return Clean(worn, int.MaxValue);
    }
}
