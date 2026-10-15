// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Database;

namespace Content.Server._Pirate.CustomMarkings;

/// <summary>What saving a library entry came to: the saved row, or a loc id saying why there is none.</summary>
/// <param name="PreviousHash">The art the entry had before, when the save replaced it.</param>
public readonly record struct CustomMarkingSaveResult(PirateCustomMarking? Entry, string? PreviousHash, string? Error)
{
    public static CustomMarkingSaveResult Fail(string error)
    {
        return new CustomMarkingSaveResult(null, null, error);
    }
}
