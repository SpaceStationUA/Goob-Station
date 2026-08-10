// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Pirate.Server.Silicons.Borgs;

/// <summary>
/// Restricts an integrated security borg weapon's lethal fire mode to configured station alert levels.
/// </summary>
[RegisterComponent]
public sealed partial class SecurityBorgLethalModeComponent : Component
{
    [DataField]
    public int SafeMode;

    [DataField]
    public int LethalMode = 1;

    [DataField]
    public HashSet<string> AllowedAlertLevels = new();
}
