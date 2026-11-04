// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Antag.Components;
using Content.Shared.Antag;
using Content.Shared.Database;

namespace Content.Server.Antag;

public sealed partial class AntagSelectionSystem
{
    // Pre-selection happens before jobs; by assignment a player may have a blacklisted job, ghost, or leave.
    // PrePlayerSpawn rules are skipped: started mid-round they may only make spawners.
    private void DropInvalidPreSelected(Entity<AntagSelectionComponent> ent)
    {
        if (ent.Comp.SelectionTime == AntagSelectionTime.PrePlayerSpawn)
            return;

        foreach (var def in ent.Comp.Definitions)
        {
            if (!ent.Comp.PreSelectedSessions.TryGetValue(def, out var set))
                continue;

            set.RemoveWhere(session =>
            {
                if (ValidAntagPreference(session, def.PrefRoles)
                    && IsSessionValid(ent, session, def)
                    && IsEntityValid(session.AttachedEntity, def))
                {
                    return false;
                }

                Log.Info($"Pre-selected {session.Name} can no longer be an antagonist for {ToPrettyString(ent)}, picking again");
                _adminLogger.Add(LogType.AntagSelection,
                    $"Pre-selected {session.Name} can no longer be an antagonist for {ToPrettyString(ent)}, picking again");
                return true;
            });
        }
    }
}
