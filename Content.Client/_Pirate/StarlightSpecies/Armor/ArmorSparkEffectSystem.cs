// Pirate: ported from ss14Starlight/space-station-14.
using Content.Shared._Pirate.StarlightSpecies.Armor;
using Robust.Shared.Map;

namespace Content.Client._Pirate.StarlightSpecies.Armor;

/// <summary>
/// Client-side implementation of the armor spark effect system.
/// </summary>
public sealed class ArmorSparkEffectSystem : SharedArmorSparkEffectSystem
{
    protected override void SpawnSparkEffectAt(EntityCoordinates coordinates, string effectPrototype)
    {
        // Client doesn't spawn effects directly - handled by server
    }

    protected override void PlayRicochetSound(EntityCoordinates coordinates, string soundCollection)
    {
        // Client doesn't play sounds directly - handled by server
    }
}
