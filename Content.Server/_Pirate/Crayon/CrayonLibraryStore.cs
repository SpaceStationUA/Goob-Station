// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Linq;
using System.Text.Json;
using Content.Shared._Pirate.Crayon;
using Robust.Shared.ContentPack;
using Robust.Shared.Network;
using Robust.Shared.Log;
using Robust.Shared.Utility;

namespace Content.Server._Pirate.Crayon;

/// <summary>One bounded library per account in server user data. Only the game thread updates it.</summary>
public sealed class CrayonLibraryStore(IWritableDirProvider data)
{
    private static readonly ResPath Directory = new("/crayon-drawings");

    private sealed record StoredDrawing(Guid Id, string Name, int Size, string[] Colors, byte[] Pixels);

    public List<CrayonDrawing> Read(NetUserId owner)
    {
        var path = PathFor(owner);
        if (!data.Exists(path))
        {
            // The process can stop between moving the old library aside and publishing the new one.
            var backup = path.WithName(path.Filename + ".bak");
            if (!data.Exists(backup))
                return [];

            data.Rename(backup, path);
        }

        using var stream = data.OpenRead(path);
        var stored = JsonSerializer.Deserialize<List<StoredDrawing>>(stream);
        if (stored == null || stored.Count > CrayonDrawing.Limit)
            throw new InvalidDataException("Invalid crayon library.");

        var drawings = new List<CrayonDrawing>(stored.Count);
        foreach (var row in stored)
        {
            var symbol = new CrayonSymbol { Size = row.Size, Pixels = row.Pixels };
            foreach (var hex in row.Colors)
            {
                if (!Color.TryFromHex(hex, out var color))
                    throw new InvalidDataException("Invalid crayon color.");

                symbol.Palette.Add(color);
            }

            if (!symbol.IsValid())
                throw new InvalidDataException("Invalid crayon drawing.");

            drawings.Add(new CrayonDrawing(row.Id, row.Name, symbol));
        }

        return drawings;
    }

    public void Write(NetUserId owner, List<CrayonDrawing> drawings)
    {
        var path = PathFor(owner);
        var temp = path.WithName(path.Filename + ".tmp");
        var backup = path.WithName(path.Filename + ".bak");
        data.CreateDir(Directory);
        using (var stream = data.OpenWrite(temp))
        {
            JsonSerializer.Serialize(stream, drawings.Select(d => new StoredDrawing(
                d.Id, d.Name, d.Symbol.Size, d.Symbol.Palette.Select(c => c.ToHex()).ToArray(), d.Symbol.Pixels)));
        }

        if (data.Exists(path))
        {
            if (data.Exists(backup))
                data.Delete(backup);

            data.Rename(path, backup);
        }

        data.Rename(temp, path);
        // The new library is already committed even if backup cleanup has to wait for the next write.
        try
        {
            if (data.Exists(backup))
                data.Delete(backup);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Logger.WarningS("crayon", $"Removing the old library backup for {owner} failed: {e.Message}");
        }
    }

    private static ResPath PathFor(NetUserId owner) => Directory / $"{owner.UserId:N}.json";
}
