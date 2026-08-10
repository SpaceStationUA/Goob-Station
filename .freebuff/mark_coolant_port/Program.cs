// Draws a small "coolant port" marker onto a new RSI state (interface_coolant),
// a 32x128 sheet with 4 direction frames (S, N, E, W) of 32x32 each.
// The marker is an 8x8 pipe-port square (dark outline, light metal, dark hole)
// placed on the outward edge of each frame so it rotates with the part:
//   frame 0 (South) -> bottom edge, frame 1 (North) -> top edge,
//   frame 2 (East)  -> right edge, frame 3 (West)  -> left edge.
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

var input = args.Length > 0 ? args[0] : "Resources/Textures/_Pirate/Structures/Machines/hfr_parts.rsi/interface.png";
var output = args.Length > 1 ? args[1] : "Resources/Textures/_Pirate/Structures/Machines/hfr_parts.rsi/interface_coolant.png";

const int cell = 32;
const int frames = 4;

using var src = Image.Load<Rgba32>(input);
Console.WriteLine($"source: {src.Width}x{src.Height}");

using var img = new Image<Rgba32>(cell, cell * frames);
img.Mutate(x => x.BackgroundColor(Color.Transparent));

// 8x8 marker: outline, metal ring, dark hole + top-left highlight.
var outline = new Rgba32(44, 46, 54, 255);
var metal = new Rgba32(168, 170, 180, 255);
var hi = new Rgba32(208, 211, 221, 255);
var hole = new Rgba32(28, 30, 38, 255);

void DrawMarker(int frame, int ox, int oy)
{
    // outline ring
    for (var y = 0; y < 8; y++)
    for (var x = 0; x < 8; x++)
    {
        if (x == 0 || y == 0 || x == 7 || y == 7)
            img[ox + x, oy + y] = outline;
    }

    // metal ring (1px inside)
    for (var y = 1; y < 7; y++)
    for (var x = 1; x < 7; x++)
    {
        if (x == 1 || y == 1 || x == 6 || y == 6)
            img[ox + x, oy + y] = metal;
    }

    // hole 4x4 (x 2..5, y 2..5)
    for (var y = 2; y < 6; y++)
    for (var x = 2; x < 6; x++)
        img[ox + x, oy + y] = hole;

    // highlight on top-left metal corner
    img[ox + 1, oy + 1] = hi;
    img[ox + 2, oy + 1] = hi;
    img[ox + 1, oy + 2] = hi;
}

// frame 0 (South): bottom-center
DrawMarker(0, 12, 24);
// frame 1 (North): top-center
DrawMarker(1, 12, cell + 0);
// frame 2 (East): right-center
DrawMarker(2, 24, cell * 2 + 12);
// frame 3 (West): left-center
DrawMarker(3, 0, cell * 3 + 12);

img.SaveAsPng(output);
Console.WriteLine($"wrote {output} ({img.Width}x{img.Height})");

// sanity: count non-transparent pixels per frame
for (var f = 0; f < frames; f++)
{
    var n = 0;
    for (var y = f * cell; y < (f + 1) * cell; y++)
    for (var x = 0; x < cell; x++)
        if (img[x, y].A > 0)
            n++;
    Console.WriteLine($"frame {f}: {n} opaque px");
}
