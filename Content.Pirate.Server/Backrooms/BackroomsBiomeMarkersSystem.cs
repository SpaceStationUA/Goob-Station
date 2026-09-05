// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Parallax;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Parallax.Biomes.Markers;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.Backrooms;

/// <summary>
/// Adds expedition-style biome marker layers to backrooms planets (finite density, no timed respawn).
/// </summary>
public sealed class BackroomsBiomeMarkersSystem : EntitySystem
{
    private static readonly ProtoId<BiomeMarkerLayerPrototype>[] Markers =
    [
        "BackroomsTarantulas",
    ];

    [Dependency] private readonly BiomeSystem _biome = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BiomeComponent, ComponentStartup>(OnBiomeStartup);
    }

    private void OnBiomeStartup(Entity<BiomeComponent> ent, ref ComponentStartup args)
    {
        if (!IsBackroomsBiome(ent.Comp))
            return;

        foreach (var marker in Markers)
        {
            _biome.AddMarkerLayer(ent, ent.Comp, marker);
        }
    }

    private static bool IsBackroomsBiome(BiomeComponent biome)
    {
        if (biome.Template is not { } template)
            return false;

        var id = template.Id;
        return id == "backrooms" || id.StartsWith("Backrooms", StringComparison.Ordinal);
    }
}
