// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Maps;
using Robust.Shared.Noise;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Parallax.Biomes.Layers;

/// <summary>
/// Spawns entities only on open maze corridor tiles that touch a maze wall.
/// Maze geometry fields must match the companion <see cref="BiomeMazeEntityLayer"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class BiomeMazeAdjacentEntityLayer : IBiomeWorldLayer
{
    [DataField]
    public List<ProtoId<ContentTileDefinition>> AllowedTiles { get; private set; } = new();

    [DataField] public FastNoiseLite Noise { get; private set; } = new(0);

    /// <summary>Density gate (same meaning as <see cref="BiomeEntityLayer.Threshold"/>).</summary>
    [DataField]
    public float Threshold { get; private set; } = 0.7f;

    [DataField] public bool Invert { get; private set; } = false;

    [DataField]
    public bool AllowAllTiles = true;

    /// <summary>
    /// If true, only spawn on corridor tiles next to maze walls.
    /// If false, any non-wall maze floor tile is eligible.
    /// </summary>
    [DataField]
    public bool RequireAdjacentWall = true;

    [DataField(required: true)]
    public List<EntProtoId> Entities = new();

    [DataField]
    public int CellSize = 5;

    [DataField]
    public int WallThickness = 2;

    [DataField]
    public float LoopChance = 0.18f;

    [DataField]
    public float PillarChance = 0.012f;

    /// <summary>
    /// If &gt; 1, only tiles with (x + y) % Spacing == 0 may spawn.
    /// </summary>
    [DataField]
    public int Spacing = 1;

    /// <summary>
    /// Extra density gate 0–1 after adjacency/spacing (e.g. 0.05 ≈ 5% of eligible tiles).
    /// </summary>
    [DataField]
    public float Chance = 1f;
}
