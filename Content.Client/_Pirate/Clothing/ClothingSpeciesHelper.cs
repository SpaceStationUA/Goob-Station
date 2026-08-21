using System.Collections.Generic;
using System.Linq;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Client.Clothing;

public static class ClothingSpeciesHelper
{
    public static IEnumerable<string> GetClothingSpecies(
        IPrototypeManager prototypeManager,
        string speciesId,
        string slot)
    {
        yield return speciesId;

        var normalizedSpeciesId = speciesId.ToLowerInvariant();
        if (normalizedSpeciesId != speciesId)
            yield return normalizedSpeciesId;

        var species = prototypeManager.EnumeratePrototypes<SpeciesPrototype>()
            .FirstOrDefault(p => string.Equals(p.ID, speciesId, StringComparison.OrdinalIgnoreCase));
        if (species is not null
            && species.ClothingSpeciesFallback.FirstOrDefault(p => string.Equals(p.Key, slot, StringComparison.OrdinalIgnoreCase)) is { Key: not null, Value: var fallback }
            && !string.Equals(fallback.ToString(), speciesId, StringComparison.OrdinalIgnoreCase))
        {
            var fallbackId = fallback.ToString();
            yield return fallbackId;

            if (fallbackId.ToLowerInvariant() != fallbackId)
                yield return fallbackId.ToLowerInvariant();
        }
    }
}
