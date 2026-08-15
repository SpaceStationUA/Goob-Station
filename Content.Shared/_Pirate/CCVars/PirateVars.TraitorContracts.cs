// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared._Pirate.CCVars;

public sealed partial class PirateVars
{
    /// <summary>
    /// Base ransom for a non-humanoid mob.
    /// </summary>
    public static readonly CVarDef<float> MobRansom =
        CVarDef.Create("game.ransom.mob_base", 5000f, CVar.REPLICATED);

    /// <summary>
    /// Base ransom for a humanoid.
    /// </summary>
    public static readonly CVarDef<float> HumanoidRansom =
        CVarDef.Create("game.ransom.humanoid_base", 10000f, CVar.REPLICATED);

    /// <summary>
    /// Ransom modifier for critical mobs.
    /// </summary>
    public static readonly CVarDef<float> RansomCritModifier =
        CVarDef.Create("game.ransom.critical_modifier", 0.5f, CVar.REPLICATED);

    /// <summary>
    /// Ransom modifier for dead mobs.
    /// </summary>
    public static readonly CVarDef<float> RansomDeadModifier =
        CVarDef.Create("game.ransom.dead_modifier", 0.2f, CVar.REPLICATED);
}
