# Regenerates a clean 32x32 "fusion torus" icon from the real /tg/ core sprite.
# Desaturates by the MAX RGB channel: colored glow/details become neutral light
# grays (stay visible) instead of darkening into muddy noise (pure luminance).
Add-Type -AssemblyName System.Drawing

$root = 'C:\Users\fentanil\Documents\GitHub\Goob-Station'
$srcPath = "$root\Resources\Textures\_Pirate\Structures\Machines\hfr_parts.rsi\core.png"
$outDir  = "$root\Resources\Textures\_Pirate\Research\hfr_icon.rsi"

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$src = New-Object System.Drawing.Bitmap($srcPath)
$out = New-Object System.Drawing.Bitmap(32, 32, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

for ($y = 0; $y -lt 32; $y++) {
    for ($x = 0; $x -lt 32; $x++) {
        $p = $src.GetPixel($x, $y)
        if ($p.A -lt 60) { continue }
        $mx = [Math]::Max($p.R, [Math]::Max($p.G, $p.B))
        # Mild contrast stretch for a crisp metallic look.
        $v = [int]((($mx - 128) * 1.15) + 128)
        $v = [Math]::Min(255, [Math]::Max(0, $v))
        $out.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($p.A, $v, $v, $v))
    }
}

$outPath = "$outDir\icon.png"
$out.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Output "saved: $outPath"
