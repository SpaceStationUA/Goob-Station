// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared._Pirate.CCVars;

[CVarDefs]
public sealed class NoviceProtectionCVars
{
    // Preserve the development default, while allowing integration tests to exercise the real system.
#if DEBUG
    private const bool PacificationDefault = false;
#else
    private const bool PacificationDefault = true;
#endif

    public static readonly CVarDef<bool> PacificationEnabled =
        CVarDef.Create("pirate.novice_pacification_enabled", PacificationDefault, CVar.SERVERONLY);
}
