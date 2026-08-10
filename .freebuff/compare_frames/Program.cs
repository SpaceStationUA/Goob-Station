// Compares the frames of two RSI PNGs frame by frame.
// Our frezon.png is 96x96 (3 cols x 3 rows) with 8 frames claimed in delays;
// TG freon.png is 256x32 (8 cols x 1 row). We read our frames the way
// RobustToolbox does: col = i % (width/32), row = i / (width/32).
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: compare_frames <ourPng> <tgPng> [frameCount]");
    return 1;
}

var ours = Image.Load<Rgba32>(args[0]);
var tg = Image.Load<Rgba32>(args[1]);
var frameCount = args.Length > 2 ? int.Parse(args[2]) : 8;

var ourCols = ours.Width / 32;
var ourRows = ours.Height / 32;
var tgCols = tg.Width / 32;
var tgRows = tg.Height / 32;

Console.WriteLine($"ours: {ours.Width}x{ours.Height} = {ourCols} cols x {ourRows} rows");
Console.WriteLine($"tg:   {tg.Width}x{tg.Height} = {tgCols} cols x {tgRows} rows");

for (var i = 0; i < frameCount; i++)
{
    var ourCol = i % ourCols;
    var ourRow = i / ourCols;
    var tgCol = i % tgCols;
    var tgRow = i / tgCols;

    var diff = 0;
    for (var y = 0; y < 32; y++)
    {
        for (var x = 0; x < 32; x++)
        {
            if (ours[ourCol * 32 + x, ourRow * 32 + y] != tg[tgCol * 32 + x, tgRow * 32 + y])
                diff++;
        }
    }

    Console.WriteLine($"frame {i}: our cell ({ourCol},{ourRow}) vs tg cell ({tgCol},{tgRow}) -> {diff} px different");
}

return 0;
