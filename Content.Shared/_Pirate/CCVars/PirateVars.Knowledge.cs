// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared._Pirate.CCVars;

[CVarDefs]
public static class KnowledgeCVars
{
    public static readonly CVarDef<bool> SkillsEnabled =
        CVarDef.Create("pirate.skills_enabled", true, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<bool> SkillGain =
        CVarDef.Create("pirate.skill_gain", true, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<bool> SkillPopups =
        CVarDef.Create("pirate.skill_popups", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
