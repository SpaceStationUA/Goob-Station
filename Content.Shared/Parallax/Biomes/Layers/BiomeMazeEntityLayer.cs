// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Maps;
using Robust.Shared.Noise;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Parallax.Biomes.Layers;

/// <summary>
/// Orthogonal grid maze with guaranteed corridor connectivity (binary-tree + optional loops).
/// Dark lattice walls; light open cells are walkable halls linked by passages.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class BiomeMazeEntityLayer : IBiomeWorldLayer
{
    [DataField]
    public List<ProtoId<ContentTileDefinition>> AllowedTiles { get; private set; } = new();

    /// <summary>Unused for maze geometry; kept for <see cref="IBiomeLayer"/>.</summary>
    [DataField] public FastNoiseLite Noise { get; private set; } = new(0);

    /// <summary>Always pass noise gate (maze decides walls itself).</summary>
    [DataField]
    public float Threshold { get; private set; } = -1f;

    [DataField] public bool Invert { get; private set; } = false;

    [DataField]
    public bool AllowAllTiles = true;

    [DataField(required: true)]
    public List<EntProtoId> Entities = new();

    /// <summary>Lattice period in tiles. Corridor width ≈ CellSize - WallThickness.</summary>
    [DataField]
    public int CellSize = 5;

    /// <summary>How many tiles thick the lattice walls are.</summary>
    [DataField]
    public int WallThickness = 2;

    /// <summary>
    /// Extra chance to open the second binary-tree wall (adds loops).
    /// Base tree already opens exactly one of East/North per cell.
    /// </summary>
    [DataField]
    public float LoopChance = 0.18f;

    /// <summary>Chance of a 1×1 pillar on open floor (does not seal corridors).</summary>
    [DataField]
    public float PillarChance = 0.012f;
}
