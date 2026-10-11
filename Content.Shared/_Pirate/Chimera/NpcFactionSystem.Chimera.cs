// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.NPC.Systems;

public sealed partial class NpcFactionSystem
{
    private void AddChimeraHostility()
    {
        if (!_factions.TryGetValue("Chimera", out var chimera))
            return;

        // NPC targeting uses the attacker's relations, so install the reverse edges too.
        foreach (var hostile in chimera.Hostile)
        {
            if (_factions.TryGetValue(hostile, out var faction))
                faction.Hostile.Add("Chimera");
        }
    }
}
