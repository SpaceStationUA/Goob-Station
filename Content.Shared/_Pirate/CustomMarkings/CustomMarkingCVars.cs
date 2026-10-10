// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared._Pirate.CustomMarkings;

/// <summary>Server settings for custom markings.</summary>
[CVarDefs]
public sealed class CustomMarkingCVars
{
    /// <summary>
    /// Whether characters get their custom markings and players can save new ones. Replicated so the creator can
    /// hide its button.
    /// </summary>
    public static readonly CVarDef<bool> Enabled =
        CVarDef.Create("wf.custom_markings.enabled", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>Most custom markings one character can wear. Replicated so the creator can show it.</summary>
    public static readonly CVarDef<int> MaxWorn =
        CVarDef.Create("wf.custom_markings.max_worn", 4, CVar.SERVER | CVar.REPLICATED);

    /// <summary>Most markings a player's library holds. Replicated so the creator can show it.</summary>
    public static readonly CVarDef<int> LibraryLimit =
        CVarDef.Create("wf.custom_markings.library_limit", 24, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Most frames a marking may be saved with; 1 allows only still markings. Art saved under a higher setting
    /// keeps its frames. Replicated so the editor can stop at it.
    /// </summary>
    public static readonly CVarDef<int> MaxFrames =
        CVarDef.Create("wf.custom_markings.max_frames", CustomMarkingRules.MaxFrames, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Whether a marking may erase parts of the body under it. Off, masks are dropped from what is saved and those
    /// already saved are not applied. Replicated so the editor can hide its tool and bodies are drawn whole.
    /// </summary>
    public static readonly CVarDef<bool> EraseBody =
        CVarDef.Create("wf.custom_markings.erase_body", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Most new drawings one player may store in a day. Saving a drawing the server already holds doesn't count,
    /// and neither does changing an entry's name or placement. 0 for no limit.
    /// </summary>
    public static readonly CVarDef<int> DailyArtLimit =
        CVarDef.Create("wf.custom_markings.daily_art_limit", 100, CVar.SERVERONLY);

    /// <summary>
    /// How many days art is kept once the server has found nothing using it: no library holds it and no saved
    /// character wears it. The server looks when it starts. Blocked art is always kept. 0 never deletes any.
    /// </summary>
    public static readonly CVarDef<int> UnusedArtDays =
        CVarDef.Create("wf.custom_markings.unused_art_days", 30, CVar.SERVERONLY);
}
