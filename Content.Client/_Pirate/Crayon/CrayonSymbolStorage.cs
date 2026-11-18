// SPDX-License-Identifier: AGPL-3.0-only
// Pirate: adapted from AU-14/ColonialMarinesUniverse#2179.

using Content.Shared._Pirate.Crayon;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Client._Pirate.Crayon;

/// <summary>
/// Reads pre-library drawings for players who choose to import them into a server library.
/// </summary>
public sealed class CrayonSymbolStorage
{
    [Dependency] private IResourceManager _resources = default!;

    private static readonly ResPath File = new("/pirate_crayon_symbols.txt");

    public readonly List<(string Name, CrayonSymbol Symbol)> Symbols = new();

    public CrayonSymbolStorage()
    {
        IoCManager.InjectDependencies(this);
        Load();
    }

    private void Load()
    {
        Symbols.Clear();
        if (!_resources.UserData.Exists(File))
            return;

        using var reader = _resources.UserData.OpenText(File);
        while (reader.ReadLine() is { } line)
        {
            if (Parse(line) is { } entry)
                Symbols.Add(entry);
        }
    }

    private static (string, CrayonSymbol)? Parse(string line)
    {
        var parts = line.Split('|');
        if (parts.Length != 4 ||
            !int.TryParse(parts[1], out var size) ||
            size is not (CrayonSymbol.Small or CrayonSymbol.Large))
        {
            return null;
        }

        var symbol = new CrayonSymbol { Size = size, Pixels = new byte[size * size] };
        foreach (var hex in parts[2].Split(','))
        {
            if (!Color.TryFromHex(hex, out var color))
                return null;

            symbol.Palette.Add(color);
        }

        if (!Convert.TryFromBase64String(parts[3], symbol.Pixels, out var written) || written != symbol.Pixels.Length)
            return null;

        return symbol.IsValid() ? (parts[0], symbol) : null;
    }
}
