// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.Preferences.Loadouts;

public sealed partial class LoadoutPrototype
{
    /// <summary>
    /// Keeps a locked loadout visible so its requirement can be shown in the tooltip.
    /// </summary>
    [DataField]
    public bool ShowWhenUnavailable;
}
