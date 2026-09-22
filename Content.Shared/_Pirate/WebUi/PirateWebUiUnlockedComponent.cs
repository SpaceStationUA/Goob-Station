// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameObjects;

namespace Content.Shared._Pirate.WebUi;

/// <summary>
///     Extra WebUI themes this PDA's owner unlocked (e.g. emagging an NT
///     device grants the Syndicate family). Does not carry the LIVE theme:
///     <see cref="PirateWebUiThemeComponent"/> keeps doing that; this only
///     widens the switcher's allowed list.
/// </summary>
[RegisterComponent]
public sealed partial class PirateWebUiUnlockedComponent : Component
{
    /// <summary>PirateWebTheme prototype ids the device may now use.</summary>
    [DataField]
    public List<string> WebThemeIds = new();
}
