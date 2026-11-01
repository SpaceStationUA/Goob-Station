// SPDX-License-Identifier: AGPL-3.0-only
// Pirate: adapted from AU-14/ColonialMarinesUniverse#2179.

using Content.Shared._Pirate.Crayon;
using Robust.Client.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client._Pirate.Crayon;

public static class CrayonSymbolImage
{
    public static OwnedTexture ToTexture(IClyde clyde, CrayonSymbol symbol)
    {
        using var image = new Image<Rgba32>(symbol.Size, symbol.Size);
        for (var y = 0; y < symbol.Size; y++)
        {
            for (var x = 0; x < symbol.Size; x++)
            {
                var pixel = symbol.Pixels[y * symbol.Size + x];
                if (pixel == 0)
                    continue;

                var c = symbol.Palette[pixel - 1];
                image[x, y] = new Rgba32(c.R, c.G, c.B, c.A);
            }
        }

        return clyde.LoadTextureFromImage(image);
    }
}
