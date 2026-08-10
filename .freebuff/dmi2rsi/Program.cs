// Converts a BYOND .dmi file into an SS14 .rsi folder (split-PNG format).
//
// DMI layout (verified against the DMISharp library):
//   States are stored as a FLAT sequence of cells, row-major across the whole image.
//   Each state contributes Frames * Dirs consecutive cells, ordered frame-major:
//   cell index within state = frame * Dirs + dir. DMI dir order = RSI dir order
//   (South, North, East, West), so no direction remap is needed.
// RSI folder layout: one PNG per state, Frames cells wide x Dirs cells tall,
//   rows = directions, columns = frames (matches RobustToolbox RsiLoading).
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

if (args.Length == 2 && args[0] == "notch")
{
    using var chk = Image.Load<Rgba32>(args[1]);
    var rows = chk.Height / 32;
    Console.WriteLine($"{Path.GetFileName(args[1])}: {rows} dir rows");
    var rowNames = new[] { "S", "N", "E", "W" };
    for (var r = 0; r < rows; r++)
    {
        var counts = new int[4]; // quadrants NW, NE, SW, SE (16x16 each)
        for (var px = 0; px < 32; px++)
        {
            for (var py = 0; py < 32; py++)
            {
                if (chk[px, r * 32 + py].A == 0)
                    continue;
                var q = (py < 16 ? 0 : 2) + (px < 16 ? 0 : 1); // NW,NE,SW,SE
                counts[q]++;
            }
        }
        var empty = new List<int>();
        for (var q = 0; q < 4; q++)
            if (counts[q] < 20)
                empty.Add(q);
        Console.WriteLine($"  row {r} ({rowNames[r]}): px={counts.Sum()} emptyQuadrants=[{string.Join(",", empty)}]");
    }
    return 0;
}

if (args.Length == 2 && args[0] == "rowdiff")
{
    using var chk = Image.Load<Rgba32>(args[1]);
    var rows = chk.Height / 32;
    var cols = chk.Width / 32;
    Console.WriteLine($"{Path.GetFileName(args[1])}: {cols}x{rows} cells");
    for (var r1 = 0; r1 < rows; r1++)
    {
        for (var r2 = r1 + 1; r2 < rows; r2++)
        {
            var same = true;
            for (var fr = 0; fr < cols && same; fr++)
            {
                for (var px = 0; px < 32 && same; px++)
                {
                    for (var py = 0; py < 32 && same; py++)
                    {
                        if (chk[fr * 32 + px, r1 * 32 + py] != chk[fr * 32 + px, r2 * 32 + py])
                            same = false;
                    }
                }
            }
            if (same)
                Console.WriteLine($"  rows {r1} and {r2} are IDENTICAL");
        }
    }
    Console.WriteLine("rowdiff done");
    return 0;
}

// Compares animation frames (columns) within each direction row.
if (args.Length == 2 && args[0] == "coldiff")
{
    using var chk = Image.Load<Rgba32>(args[1]);
    var rows = chk.Height / 32;
    var cols = chk.Width / 32;
    Console.WriteLine($"{Path.GetFileName(args[1])}: {cols}x{rows} cells");
    for (var r = 0; r < rows; r++)
    {
        var rowName = r < 4 ? new[] { "S", "N", "E", "W" }[r] : r.ToString();
        var distinct = 0;
        for (var c1 = 0; c1 < cols; c1++)
        {
            var seen = false;
            for (var c2 = 0; c2 < c1; c2++)
            {
                var same = true;
                for (var px = 0; px < 32 && same; px++)
                    for (var py = 0; py < 32 && same; py++)
                        if (chk[c1 * 32 + px, r * 32 + py] != chk[c2 * 32 + px, r * 32 + py])
                            same = false;
                if (same)
                    seen = true;
            }
            if (!seen)
                distinct++;
        }
        Console.WriteLine($"  row {r} ({rowName}): {distinct} distinct frames of {cols}");
    }
    return 0;
}

var srcPath = Path.GetFullPath(args[0]);
var outDir = Path.GetFullPath(args[1]);
Directory.CreateDirectory(outDir);

using var img = Image.Load<Rgba32>(srcPath);

var textData = img.Metadata.GetPngMetadata().TextData;
var desc = textData.FirstOrDefault(t => t.Keyword == "Description").Value
    ?? throw new Exception("No Description text chunk found in DMI.");

// --- Parse the DMI metadata ---
var iconSize = 32;
var rawStates = new List<(string Name, int Dirs, int Frames, List<float> Delays)>();
string? curName = null;
var curDirs = 1;
var curFrames = 1;
var curDelays = new List<float>();

void FlushState()
{
    if (curName == null)
        return;
    var frames = curDelays.Count > 0 ? curDelays.Count : curFrames;
    if (curDelays.Count == 0)
        curDelays = Enumerable.Repeat(1f, frames).ToList();
    rawStates.Add((curName, curDirs, frames, new List<float>(curDelays)));
}

foreach (var rawLine in desc.Split('\n'))
{
    var line = rawLine.Trim();
    if (line.Length == 0 || line.StartsWith('#'))
        continue;

    var eq = line.IndexOf('=');
    if (eq < 0)
        continue;
    var key = line[..eq].Trim();
    var val = line[(eq + 1)..].Trim();

    switch (key)
    {
        case "icon_size":
        case "width":
            iconSize = int.Parse(val);
            break;
        case "state":
            FlushState();
            curName = val.Trim('"');
            curDirs = 1;
            curFrames = 1;
            curDelays = new List<float>();
            break;
        case "dirs":
            curDirs = int.Parse(val);
            break;
        case "frames":
            curFrames = int.Parse(val);
            break;
        case "delay":
            foreach (var part in val.Split(','))
                curDelays.Add(float.Parse(part.Trim(), System.Globalization.CultureInfo.InvariantCulture));
            break;
    }
}
FlushState();

Console.WriteLine($"icon_size={iconSize}, image={img.Width}x{img.Height}, states={rawStates.Count}");
foreach (var s in rawStates)
    Console.WriteLine($"  {s.Name}: dirs={s.Dirs} frames={s.Frames}");

// --- Flat cell layout ---
var framesPerRow = img.Width / iconSize;
var totalCells = rawStates.Sum(s => s.Dirs * s.Frames);
Console.WriteLine($"framesPerRow={framesPerRow}, totalCells={totalCells}, capacity={framesPerRow * (img.Height / iconSize)}");

// --- Extract each state ---
var metaStates = new List<string>();
var cellCursor = 0;
foreach (var s in rawStates)
{
    var outW = iconSize * s.Frames;
    var outH = iconSize * s.Dirs;

    using var frame = new Image<Rgba32>(outW, outH);
    for (var dir = 0; dir < s.Dirs; dir++)
    {
        for (var fr = 0; fr < s.Frames; fr++)
        {
            var cellIndex = cellCursor + fr * s.Dirs + dir;
            var srcX0 = (cellIndex % framesPerRow) * iconSize;
            var srcY0 = (cellIndex / framesPerRow) * iconSize;
            if (srcX0 + iconSize > img.Width || srcY0 + iconSize > img.Height)
                throw new Exception($"State '{s.Name}' cell {cellIndex} OOB: src=({srcX0},{srcY0})");
            var dstX0 = fr * iconSize;
            var dstY0 = dir * iconSize;
            for (var px = 0; px < iconSize; px++)
            {
                for (var py = 0; py < iconSize; py++)
                {
                    frame[dstX0 + px, dstY0 + py] = img[srcX0 + px, srcY0 + py];
                }
            }
        }
    }
    cellCursor += s.Dirs * s.Frames;

    var pngPath = Path.Combine(outDir, s.Name + ".png");
    using (var stream = File.Create(pngPath))
    {
        frame.SaveAsPng(stream);
    }

    var stateJson = $"{{\"name\": \"{s.Name}\"";
    if (s.Dirs > 1)
        stateJson += $", \"directions\": {s.Dirs}";
    if (s.Frames > 1)
    {
        var dirs = new List<string>();
        for (var d = 0; d < s.Dirs; d++)
            dirs.Add("[" + string.Join(", ", s.Delays.Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "]");
        stateJson += $", \"delays\": [{string.Join(", ", dirs)}]";
    }
    stateJson += "}";
    metaStates.Add(stateJson);
}

var meta =
    "{\n" +
    "  \"version\": 1,\n" +
    "  \"license\": \"CC-BY-SA-3.0\",\n" +
    "  \"copyright\": \"Hypertorus fusion reactor sprite from /tg/station, converted from DMI\",\n" +
    "  \"size\": {\"x\": " + iconSize + ", \"y\": " + iconSize + "},\n" +
    "  \"states\": [\n" +
    string.Join(",\n", metaStates.Select(s => "    " + s)) +
    "\n  ]\n" +
    "}\n";
File.WriteAllText(Path.Combine(outDir, "meta.json"), meta);

Console.WriteLine($"Wrote {rawStates.Count} states + meta.json to {outDir}");
return 0;
