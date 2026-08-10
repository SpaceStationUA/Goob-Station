using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

// Recolors an animated overlay PNG (frames laid out horizontally) to a target
// color, preserving the alpha mask and per-pixel luminance so the haze shape
// survives. Usage: tint_overlay <src.png> <out.png> <R,G,B>
var src = args[0];
var dst = args[1];
var rgb = args[2].Split(',').Select(int.Parse).ToArray();

using var img = Image.Load<Rgba32>(src);
for (var y = 0; y < img.Height; y++)
{
    for (var x = 0; x < img.Width; x++)
    {
        var p = img[x, y];
        if (p.A == 0)
            continue;
        var lum = (p.R * 0.299f + p.G * 0.587f + p.B * 0.114f) / 255f;
        var scale = 0.35f + 0.65f * lum;
        img[x, y] = new Rgba32(
            (byte)Math.Clamp(rgb[0] * scale, 0, 255),
            (byte)Math.Clamp(rgb[1] * scale, 0, 255),
            (byte)Math.Clamp(rgb[2] * scale, 0, 255),
            p.A);
    }
}

img.Save(dst);
Console.WriteLine($"wrote {dst} ({img.Width}x{img.Height})");
