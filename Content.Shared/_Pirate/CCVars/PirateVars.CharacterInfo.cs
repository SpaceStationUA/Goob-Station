// SPDX-FileCopyrightText: 2025 Starlight
// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: MIT

using Robust.Shared.Configuration;

namespace Content.Shared._Pirate.CCVars;

public sealed partial class PirateVars
{
    public static readonly CVarDef<bool> ICSecrets =
        CVarDef.Create("ic.secrets_text", false, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<bool> ExploitableSecrets =
        CVarDef.Create("ic.secrets_exploitable", false, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<bool> CharacterInspectWindowEnabled =
        CVarDef.Create("ic.inspect_windows", false, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<bool> OOCNotes =
        CVarDef.Create("ooc.rp_notes", false, CVar.SERVER | CVar.REPLICATED);
}
