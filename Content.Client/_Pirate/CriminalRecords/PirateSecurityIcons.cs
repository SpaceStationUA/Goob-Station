// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Security;
using Robust.Shared.Utility;

namespace Content.Client._Pirate.CriminalRecords;

/// <summary>
/// Icons for the security statuses that only exist on Pirate Station.
/// Every other status keeps using the shared security icon sheet.
/// </summary>
public static class PirateSecurityIcons
{
    public static readonly ResPath Sheet = new("_Pirate/Interface/Misc/security_icons.rsi");

    /// <summary>
    /// Returns the icon of a pirate-only status, or null if the status is drawn from the shared sheet.
    /// </summary>
    public static SpriteSpecifier? FromStatus(SecurityStatus status)
    {
        return status switch
        {
            SecurityStatus.Warned => new SpriteSpecifier.Rsi(Sheet, "hud_warned"),
            SecurityStatus.Safe => new SpriteSpecifier.Rsi(Sheet, "hud_safe"),
            _ => null,
        };
    }

    /// <summary>
    /// Same as <see cref="FromStatus"/>, but for the serialized status name stored in record logs.
    /// </summary>
    public static SpriteSpecifier? FromStatusName(string? statusName)
    {
        return statusName switch
        {
            "Warned" => new SpriteSpecifier.Rsi(Sheet, "hud_warned"),
            "Safe" => new SpriteSpecifier.Rsi(Sheet, "hud_safe"),
            _ => null,
        };
    }

    /// <summary>
    /// Whether the given serialized status name belongs to the pirate icon sheet.
    /// </summary>
    public static bool IsPirateStatusName(string? statusName)
    {
        return FromStatusName(statusName) != null;
    }
}
