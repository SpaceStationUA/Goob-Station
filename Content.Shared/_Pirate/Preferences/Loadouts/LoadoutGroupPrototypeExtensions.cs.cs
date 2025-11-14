using Content.Shared.Preferences.Loadouts;
using Robust.Shared.Prototypes;
using System.Collections.Generic;
using System.Linq;

namespace Content.Shared.Preferences.Loadouts;

public static class LoadoutGroupPrototypeExtensions
{
    public static IEnumerable<ProtoId<LoadoutPrototype>> GetAllLoadouts(
        this LoadoutGroupPrototype group,
        IPrototypeManager protoManager,
        HashSet<string>? visitedGroups = null)
    {
        visitedGroups ??= new HashSet<string>();

        if (!visitedGroups.Add(group.ID))
            yield break;

        foreach (var loadoutId in group.Loadouts)
        {
            yield return loadoutId;
        }

        foreach (var subgroupId in group.Subgroups)
        {
            if (!protoManager.TryIndex(subgroupId, out var subgroupProto))
                continue;

            foreach (var loadoutId in subgroupProto.GetAllLoadouts(protoManager, visitedGroups))
            {
                yield return loadoutId;
            }
        }
    }
}