// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Maps;
using Robust.Shared.Noise;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Parallax.Biomes.Layers;

/// <summary>
/// Picks a floor tile deterministically from a pool. If <see cref="Tiles"/> is empty,
/// uses every non-abstract, non-space tile that has a sprite.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class BiomeRandomTileLayer : IBiomeLayer
{
    [DataField] public FastNoiseLite Noise { get; private set; } = new(0);

    /// <inheritdoc/>
    [DataField]
    public float Threshold { get; private set; } = -1f;

    /// <inheritdoc/>
    [DataField] public bool Invert { get; private set; } = false;

    /// <summary>
    /// Explicit tile pool. Empty means all non-space tiles with sprites.
    /// </summary>
    [DataField]
    public List<ProtoId<ContentTileDefinition>> Tiles = new();
}
