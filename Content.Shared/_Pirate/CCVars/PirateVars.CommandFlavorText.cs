// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared._Pirate.CCVars;

public sealed partial class PirateVars
{
    public static readonly CVarDef<int> CommandFlavorTextMinLength =
        CVarDef.Create("ic.command_flavor_text_min_length", 300, CVar.SERVER | CVar.REPLICATED);
}
