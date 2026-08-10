// Bakes a final colored canister PNG from the /tg/ modular canister pieces,
// replicating the /tg/ greyscale pipeline (canister_base + double_stripe +
// post_effects JSON configs):
//   1. base tinted with color1 (multiply by grey value)
//   2. add_shader blended "add"
//   3. multi_shader blended "multiply"
//   4. double_stripe tinted with color2, drawn on top (overlay)
//   5. double_stripe_shader blended "subtract"
//   6. outline tinted with color1, drawn on top
//   7. lights drawn on top
// Usage: bake_canisters <inputDir> <outDir> <name> <color1Hex> <color2Hex>
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

if (args.Length != 5)
{
    Console.Error.WriteLine("Usage: bake_canisters <inputDir> <outDir> <name> <color1Hex> <color2Hex>");
    return 1;
}

var inputDir = Path.GetFullPath(args[0]);
var outDir = Path.GetFullPath(args[1]);
var name = args[2];
var color1 = (Rgba32) Color.ParseHex(args[3]);
var color2 = (Rgba32) Color.ParseHex(args[4]);
Directory.CreateDirectory(outDir);

using var baseImg = Image.Load<Rgba32>(Path.Combine(inputDir, "base.png"));
using var addShader = Image.Load<Rgba32>(Path.Combine(inputDir, "add_shader.png"));
using var multiShader = Image.Load<Rgba32>(Path.Combine(inputDir, "multi_shader.png"));
using var doubleStripe = Image.Load<Rgba32>(Path.Combine(inputDir, "double_stripe.png"));
using var doubleStripeShader = Image.Load<Rgba32>(Path.Combine(inputDir, "double_stripe_shader.png"));
using var outline = Image.Load<Rgba32>(Path.Combine(inputDir, "outline.png"));
using var lights = Image.Load<Rgba32>(Path.Combine(inputDir, "lights.png"));

using var result = new Image<Rgba32>(32, 32);

// Step 1: base tinted with color1 (multiply by luminance).
for (var y = 0; y < 32; y++)
{
    for (var x = 0; x < 32; x++)
    {
        var p = baseImg[x, y];
        if (p.A == 0)
            continue;
        // /tg/ greyscale: new = color * (grey / 255)
        var lum = (p.R + p.G + p.B) / 3f / 255f;
        result[x, y] = new Rgba32(
            (byte)Math.Clamp(color1.R * lum, 0, 255),
            (byte)Math.Clamp(color1.G * lum, 0, 255),
            (byte)Math.Clamp(color1.B * lum, 0, 255),
            p.A);
    }
}

// Step 2: add_shader blended "add".
BlendAdd(result, addShader);

// Step 3: multi_shader blended "multiply".
BlendMultiply(result, multiShader);

// Step 4: double_stripe tinted with color2, drawn on top.
TintAndOverlay(result, doubleStripe, color2);

// Step 5: double_stripe_shader blended "subtract".
BlendSubtract(result, doubleStripeShader);

// Step 6: outline tinted with color1, drawn on top.
TintAndOverlay(result, outline, color1);

// Step 7: lights drawn on top.
Overlay(result, lights);

result.SaveAsPng(Path.Combine(outDir, name + ".png"));
Console.WriteLine($"Baked {name}.png");

return 0;

static void BlendAdd(Image<Rgba32> dst, Image<Rgba32> src)
{
    for (var y = 0; y < 32; y++)
    {
        for (var x = 0; x < 32; x++)
        {
            var s = src[x, y];
            if (s.A == 0)
                continue;
            var d = dst[x, y];
            var a = s.A / 255f;
            dst[x, y] = new Rgba32(
                (byte)Math.Clamp(d.R + s.R * a, 0, 255),
                (byte)Math.Clamp(d.G + s.G * a, 0, 255),
                (byte)Math.Clamp(d.B + s.B * a, 0, 255),
                (byte)Math.Max(d.A, s.A));
        }
    }
}

static void BlendMultiply(Image<Rgba32> dst, Image<Rgba32> src)
{
    for (var y = 0; y < 32; y++)
    {
        for (var x = 0; x < 32; x++)
        {
            var s = src[x, y];
            if (s.A == 0)
                continue;
            var d = dst[x, y];
            var a = s.A / 255f;
            // Mix between dst and dst*src, weighted by shader alpha.
            var mix = a;
            dst[x, y] = new Rgba32(
                (byte)Math.Clamp(d.R * (1 - mix) + d.R * s.R / 255f * mix, 0, 255),
                (byte)Math.Clamp(d.G * (1 - mix) + d.G * s.G / 255f * mix, 0, 255),
                (byte)Math.Clamp(d.B * (1 - mix) + d.B * s.B / 255f * mix, 0, 255),
                d.A);
        }
    }
}

static void BlendSubtract(Image<Rgba32> dst, Image<Rgba32> src)
{
    for (var y = 0; y < 32; y++)
    {
        for (var x = 0; x < 32; x++)
        {
            var s = src[x, y];
            if (s.A == 0)
                continue;
            var d = dst[x, y];
            var a = s.A / 255f;
            dst[x, y] = new Rgba32(
                (byte)Math.Clamp(d.R - s.R * a, 0, 255),
                (byte)Math.Clamp(d.G - s.G * a, 0, 255),
                (byte)Math.Clamp(d.B - s.B * a, 0, 255),
                d.A);
        }
    }
}

static void TintAndOverlay(Image<Rgba32> dst, Image<Rgba32> src, Rgba32 color)
{
    for (var y = 0; y < 32; y++)
    {
        for (var x = 0; x < 32; x++)
        {
            var p = src[x, y];
            if (p.A == 0)
                continue;
            var lum = (p.R + p.G + p.B) / 3f / 255f;
            dst[x, y] = new Rgba32(
                (byte)Math.Clamp(color.R * lum, 0, 255),
                (byte)Math.Clamp(color.G * lum, 0, 255),
                (byte)Math.Clamp(color.B * lum, 0, 255),
                (byte)Math.Max(dst[x, y].A, p.A));
        }
    }
}

static void Overlay(Image<Rgba32> dst, Image<Rgba32> src)
{
    for (var y = 0; y < 32; y++)
    {
        for (var x = 0; x < 32; x++)
        {
            var s = src[x, y];
            if (s.A == 0)
                continue;
            var d = dst[x, y];
            var a = s.A / 255f;
            dst[x, y] = new Rgba32(
                (byte)(s.R * a + d.R * (1 - a)),
                (byte)(s.G * a + d.G * (1 - a)),
                (byte)(s.B * a + d.B * (1 - a)),
                (byte)Math.Max(d.A, s.A));
        }
    }
}
