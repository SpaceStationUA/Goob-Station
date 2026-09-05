// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes.Layers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Noise;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Shared.Parallax.Biomes;

public abstract class SharedBiomeSystem : EntitySystem
{
    [Dependency] protected readonly IPrototypeManager ProtoManager = default!;
    [Dependency] private readonly ISerializationManager _serManager = default!;
    [Dependency] protected readonly ITileDefinitionManager TileDefManager = default!;
    [Dependency] private readonly TileSystem _tile = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    public const byte ChunkSize = 8; // Lavaland change - make it public

    // Goob - Cache Noise
    private readonly ConcurrentDictionary<(FastNoiseLite, int), FastNoiseLite> _noiseCache = new();

    /// <summary>
    /// Cached non-space tile pool for <see cref="BiomeRandomTileLayer"/> with an empty tile list.
    /// </summary>
    private List<ContentTileDefinition>? _nonSpaceTiles;

    protected void ClearNoiseCache()
    {
        _noiseCache.Clear();
    }

    private List<ContentTileDefinition> GetNonSpaceTiles()
    {
        if (_nonSpaceTiles != null)
            return _nonSpaceTiles;

        _nonSpaceTiles = new List<ContentTileDefinition>();
        foreach (var proto in ProtoManager.EnumeratePrototypes<ContentTileDefinition>())
        {
            if (proto.Abstract || proto.MapAtmosphere || proto.Sprite == null)
                continue;

            _nonSpaceTiles.Add(proto);
        }

        return _nonSpaceTiles;
    }

    private T Pick<T>(List<T> collection, float value)
    {
        // Listen I don't need this exact and I'm too lazy to finetune just for random ent picking.
        value %= 1f;
        value = Math.Clamp(value, 0f, 1f);

        if (collection.Count == 1)
            return collection[0];

        var randValue = value * collection.Count;

        foreach (var item in collection)
        {
            randValue -= 1f;

            if (randValue <= 0f)
            {
                return item;
            }
        }

        throw new ArgumentOutOfRangeException();
    }

    private int Pick(int count, float value)
    {
        value %= 1f;
        value = Math.Clamp(value, 0f, 1f);

        if (count == 1)
            return 0;

        value *= count;

        for (var i = 0; i < count; i++)
        {
            value -= 1f;

            if (value <= 0f)
            {
                return i;
            }
        }

        throw new ArgumentOutOfRangeException();
    }

    public bool TryGetBiomeTile(EntityUid uid, MapGridComponent grid, Vector2i indices, [NotNullWhen(true)] out Tile? tile)
    {
        if (_map.TryGetTileRef(uid, grid, indices, out var tileRef) && !tileRef.Tile.IsEmpty)
        {
            tile = tileRef.Tile;
            return true;
        }

        if (!TryComp<BiomeComponent>(uid, out var biome))
        {
            tile = null;
            return false;
        }

        return TryGetBiomeTile(indices, biome.Layers, biome.Seed, (uid, grid), out tile);
    }

    /// <summary>
    /// Tries to get the tile, real or otherwise, for the specified indices.
    /// </summary>
    public bool TryGetBiomeTile(Vector2i indices, List<IBiomeLayer> layers, int seed, Entity<MapGridComponent>? grid, [NotNullWhen(true)] out Tile? tile)
    {
        if (grid is { } gridEnt && _map.TryGetTileRef(gridEnt, gridEnt.Comp, indices, out var tileRef) && !tileRef.Tile.IsEmpty)
        {
            tile = tileRef.Tile;
            return true;
        }

        return TryGetTile(indices, layers, seed, grid, out tile);
    }

    /// <summary>
    /// Tries to get the tile, real or otherwise, for the specified indices.
    /// </summary>
    [Obsolete("Use the Entity<MapGridComponent>? overload")]
    public bool TryGetBiomeTile(Vector2i indices, List<IBiomeLayer> layers, int seed, MapGridComponent? grid, [NotNullWhen(true)] out Tile? tile)
    {
        return TryGetBiomeTile(indices, layers, seed, grid == null ? null : (grid.Owner, grid), out tile);
    }

    /// <summary>
    /// Gets the underlying biome tile, ignoring any existing tile that may be there.
    /// </summary>
    public bool TryGetTile(Vector2i indices, List<IBiomeLayer> layers, int seed, Entity<MapGridComponent>? grid, [NotNullWhen(true)] out Tile? tile)
    {
        for (var i = layers.Count - 1; i >= 0; i--)
        {
            var layer = layers[i];
            var noiseCopy = GetNoise(layer.Noise, seed);

            var invert = layer.Invert;
            var value = noiseCopy.GetNoise(indices.X, indices.Y);
            value = invert ? value * -1 : value;

            if (value < layer.Threshold)
                continue;

            // Check if the tile is from meta layer, otherwise fall back to default layers.
            if (layer is BiomeMetaLayer meta)
            {
                if (TryGetBiomeTile(indices, ProtoManager.Index<BiomeTemplatePrototype>(meta.Template).Layers, seed, grid, out tile))
                {
                    return true;
                }

                continue;
            }

            if (layer is BiomeRandomTileLayer randomTileLayer)
            {
                if (TryGetRandomTile(indices, noiseCopy, randomTileLayer, out tile))
                    return true;

                continue;
            }

            if (layer is not BiomeTileLayer tileLayer)
                continue;

            if (TryGetTile(indices, noiseCopy, tileLayer.Invert, tileLayer.Threshold, ProtoManager.Index(tileLayer.Tile), tileLayer.Variants, out tile))
            {
                return true;
            }
        }

        tile = null;
        return false;
    }

    private bool TryGetRandomTile(Vector2i indices, FastNoiseLite noise, BiomeRandomTileLayer layer, [NotNullWhen(true)] out Tile? tile)
    {
        List<ContentTileDefinition> pool;
        if (layer.Tiles.Count > 0)
        {
            pool = new List<ContentTileDefinition>(layer.Tiles.Count);
            foreach (var tileId in layer.Tiles)
            {
                if (!ProtoManager.TryIndex(tileId, out ContentTileDefinition? def) || def.MapAtmosphere)
                    continue;

                pool.Add(def);
            }
        }
        else
        {
            pool = GetNonSpaceTiles();
        }

        if (pool.Count == 0)
        {
            tile = null;
            return false;
        }

        // Separate noise axis so pick is stable and independent of threshold sampling.
        var pickValue = (noise.GetNoise(indices.X * 3f, indices.Y * 3f, pool.Count) + 1f) / 2f;
        var tileDef = Pick(pool, pickValue);

        byte variant = 0;
        if (tileDef.Variants > 1)
        {
            var variantValue = (noise.GetNoise(indices.X * 8, indices.Y * 8, tileDef.Variants) + 1f) * 100;
            variant = _tile.PickVariant(tileDef, (int)variantValue);
        }

        tile = new Tile(tileDef.TileId, variant);
        return true;
    }

    /// <summary>
    /// Gets the underlying biome tile, ignoring any existing tile that may be there.
    /// </summary>
    [Obsolete("Use the Entity<MapGridComponent>? overload")]
    public bool TryGetTile(Vector2i indices, List<IBiomeLayer> layers, int seed, MapGridComponent? grid, [NotNullWhen(true)] out Tile? tile)
    {
        return TryGetTile(indices, layers, seed, grid == null ? null : (grid.Owner, grid), out tile);
    }

    /// <summary>
    /// Gets the underlying biome tile, ignoring any existing tile that may be there.
    /// </summary>
    private bool TryGetTile(Vector2i indices, FastNoiseLite noise, bool invert, float threshold, ContentTileDefinition tileDef, List<byte>? variants, [NotNullWhen(true)] out Tile? tile)
    {
        var found = noise.GetNoise(indices.X, indices.Y);
        found = invert ? found * -1 : found;

        if (found < threshold)
        {
            tile = null;
            return false;
        }

        byte variant = 0;
        var variantCount = variants?.Count ?? tileDef.Variants;

        // Pick a variant tile if they're available as well
        if (variantCount > 1)
        {
            var variantValue = (noise.GetNoise(indices.X * 8, indices.Y * 8, variantCount) + 1f) * 100;
            variant = _tile.PickVariant(tileDef, (int)variantValue);
        }

        tile = new Tile(tileDef.TileId, variant);
        return true;
    }

    /// <summary>
    /// Tries to get the relevant entity for this tile.
    /// </summary>
    public bool TryGetEntity(Vector2i indices, BiomeComponent component, Entity<MapGridComponent>? grid,
        [NotNullWhen(true)] out string? entity)
    {
        if (!TryGetBiomeTile(indices, component.Layers, component.Seed, grid, out var tile))
        {
            entity = null;
            return false;
        }

        return TryGetEntity(indices, component.Layers, tile.Value, component.Seed, grid, out entity);
    }

    /// <summary>
    /// Tries to get the relevant entity for this tile.
    /// </summary>
    [Obsolete("Use the Entity<MapGridComponent>? overload")]
    public bool TryGetEntity(Vector2i indices, BiomeComponent component, MapGridComponent grid,
        [NotNullWhen(true)] out string? entity)
    {
        return TryGetEntity(indices, component, grid == null ? null : (grid.Owner, grid), out entity);
    }

    public bool TryGetEntity(Vector2i indices, List<IBiomeLayer> layers, Tile tileRef, int seed, Entity<MapGridComponent>? grid,
        [NotNullWhen(true)] out string? entity)
    {
        var tileId = TileDefManager[tileRef.TypeId].ID;

        for (var i = layers.Count - 1; i >= 0; i--)
        {
            var layer = layers[i];

            switch (layer)
            {
                case BiomeDummyLayer:
                    continue;
                case BiomeMazeEntityLayer mazeLayer:
                {
                    if (!mazeLayer.AllowAllTiles && !mazeLayer.AllowedTiles.Contains(tileId))
                        continue;

                    if (!IsMazeWall(indices, seed, mazeLayer.CellSize, mazeLayer.WallThickness, mazeLayer.LoopChance, mazeLayer.PillarChance))
                        continue;

                    var pickNoise = GetNoise(mazeLayer.Noise, seed);
                    var noiseValue = pickNoise.GetNoise(indices.X, indices.Y, i);
                    entity = Pick(mazeLayer.Entities, (noiseValue + 1f) / 2f);
                    return true;
                }
                case BiomeMazeAdjacentEntityLayer adjLayer:
                {
                    if (!adjLayer.AllowAllTiles && !adjLayer.AllowedTiles.Contains(tileId))
                        continue;

                    // Never on walls.
                    if (IsMazeWall(indices, seed, adjLayer.CellSize, adjLayer.WallThickness, adjLayer.LoopChance, adjLayer.PillarChance))
                        continue;

                    if (adjLayer.RequireAdjacentWall &&
                        !IsAdjacentToMazeWall(indices, seed, adjLayer.CellSize, adjLayer.WallThickness, adjLayer.LoopChance, adjLayer.PillarChance))
                        continue;

                    if (adjLayer.Spacing > 1 && PositiveMod(indices.X + indices.Y, adjLayer.Spacing) != 0)
                        continue;

                    // Per-layer salt so Chance rolls are independent across adjacent layers.
                    if (adjLayer.Chance < 1f &&
                        Hash01(seed, indices.X, indices.Y, 77 + i) >= adjLayer.Chance)
                        continue;

                    var adjNoise = GetNoise(adjLayer.Noise, seed);
                    var adjValue = adjNoise.GetNoise(indices.X, indices.Y);
                    adjValue = adjLayer.Invert ? adjValue * -1 : adjValue;
                    if (adjValue < adjLayer.Threshold)
                        continue;

                    var adjPick = adjNoise.GetNoise(indices.X, indices.Y, i);
                    entity = Pick(adjLayer.Entities, (adjPick + 1f) / 2f);
                    return true;
                }
                case IBiomeWorldLayer worldLayer:
                    if (layer is not BiomeEntityLayer { AllowAllTiles: true } &&
                        !worldLayer.AllowedTiles.Contains(tileId))
                        continue;

                    break;
                case BiomeMetaLayer:
                    break;
                default:
                    continue;
            }

            var noiseCopy = GetNoise(layer.Noise, seed);

            var invert = layer.Invert;
            var value = noiseCopy.GetNoise(indices.X, indices.Y);
            value = invert ? value * -1 : value;

            if (value < layer.Threshold)
                continue;

            if (layer is BiomeMetaLayer meta)
            {
                if (TryGetEntity(indices, ProtoManager.Index<BiomeTemplatePrototype>(meta.Template).Layers, tileRef, seed, grid, out entity))
                {
                    return true;
                }

                continue;
            }

            // Decals might block entity so need to check if there's one in front of us.
            if (layer is not BiomeEntityLayer biomeLayer)
            {
                entity = null;
                return false;
            }

            var entityNoiseValue = noiseCopy.GetNoise(indices.X, indices.Y, i);
            entity = Pick(biomeLayer.Entities, (entityNoiseValue + 1f) / 2f);
            return true;
        }

        entity = null;
        return false;
    }

    [Obsolete("Use the Entity<MapGridComponent>? overload")]
    public bool TryGetEntity(Vector2i indices, List<IBiomeLayer> layers, Tile tileRef, int seed, MapGridComponent grid,
        [NotNullWhen(true)] out string? entity)
    {
        return TryGetEntity(indices, layers, tileRef, seed, grid == null ? null : (grid.Owner, grid), out entity);
    }

    /// <summary>
    /// Tries to get the relevant decals for this tile.
    /// </summary>
    public bool TryGetDecals(Vector2i indices, List<IBiomeLayer> layers, int seed, Entity<MapGridComponent>? grid,
        [NotNullWhen(true)] out List<(string ID, Vector2 Position)>? decals)
    {
        if (!TryGetBiomeTile(indices, layers, seed, grid, out var tileRef))
        {
            decals = null;
            return false;
        }

        var tileId = TileDefManager[tileRef.Value.TypeId].ID;

        for (var i = layers.Count - 1; i >= 0; i--)
        {
            var layer = layers[i];

            // Entities might block decal so need to check if there's one in front of us.
            switch (layer)
            {
                case BiomeDummyLayer:
                    continue;
                case BiomeMazeEntityLayer mazeLayer:
                    if (!mazeLayer.AllowAllTiles && !mazeLayer.AllowedTiles.Contains(tileId))
                        continue;

                    if (IsMazeWall(indices, seed, mazeLayer.CellSize, mazeLayer.WallThickness, mazeLayer.LoopChance, mazeLayer.PillarChance))
                    {
                        decals = null;
                        return false;
                    }

                    continue;
                case BiomeMazeAdjacentEntityLayer adjLayer:
                    if (!adjLayer.AllowAllTiles && !adjLayer.AllowedTiles.Contains(tileId))
                        continue;

                    // Same as entity layer: blocks decals where a light/prop would spawn.
                    if (!IsMazeWall(indices, seed, adjLayer.CellSize, adjLayer.WallThickness, adjLayer.LoopChance, adjLayer.PillarChance) &&
                        (!adjLayer.RequireAdjacentWall ||
                         IsAdjacentToMazeWall(indices, seed, adjLayer.CellSize, adjLayer.WallThickness, adjLayer.LoopChance, adjLayer.PillarChance)) &&
                        (adjLayer.Spacing <= 1 || PositiveMod(indices.X + indices.Y, adjLayer.Spacing) == 0) &&
                        (adjLayer.Chance >= 1f || Hash01(seed, indices.X, indices.Y, 77 + i) < adjLayer.Chance))
                    {
                        var adjNoise = GetNoise(adjLayer.Noise, seed);
                        var adjValue = adjNoise.GetNoise(indices.X, indices.Y);
                        adjValue = adjLayer.Invert ? adjValue * -1 : adjValue;
                        if (adjValue >= adjLayer.Threshold)
                        {
                            decals = null;
                            return false;
                        }
                    }

                    continue;
                case IBiomeWorldLayer worldLayer:
                    if (layer is not BiomeEntityLayer { AllowAllTiles: true } &&
                        !worldLayer.AllowedTiles.Contains(tileId))
                        continue;

                    break;
                case BiomeMetaLayer:
                    break;
                default:
                    continue;
            }

            var invert = layer.Invert;
            var noiseCopy = GetNoise(layer.Noise, seed);
            var value = noiseCopy.GetNoise(indices.X, indices.Y);
            value = invert ? value * -1 : value;

            if (value < layer.Threshold)
                continue;

            if (layer is BiomeMetaLayer meta)
            {
                if (TryGetDecals(indices, ProtoManager.Index<BiomeTemplatePrototype>(meta.Template).Layers, seed, grid, out decals))
                {
                    return true;
                }

                continue;
            }

            // Check if the other layer should even render, if not then keep going.
            if (layer is not BiomeDecalLayer decalLayer)
            {
                decals = null;
                return false;
            }

            decals = new List<(string ID, Vector2 Position)>();

            for (var x = 0; x < decalLayer.Divisions; x++)
            {
                for (var y = 0; y < decalLayer.Divisions; y++)
                {
                    var index = new Vector2(indices.X + x * 1f / decalLayer.Divisions, indices.Y + y * 1f / decalLayer.Divisions);
                    var decalValue = noiseCopy.GetNoise(index.X, index.Y);
                    decalValue = invert ? decalValue * -1 : decalValue;

                    if (decalValue < decalLayer.Threshold)
                        continue;

                    decals.Add((Pick(decalLayer.Decals, (noiseCopy.GetNoise(indices.X, indices.Y, x + y * decalLayer.Divisions) + 1f) / 2f), index));
                }
            }

            // Check other layers
            if (decals.Count == 0)
                continue;

            return true;
        }

        decals = null;
        return false;
    }

    /// <summary>
    /// Tries to get the relevant decals for this tile.
    /// </summary>
    [Obsolete("Use the Entity<MapGridComponent>? overload")]
    public bool TryGetDecals(Vector2i indices, List<IBiomeLayer> layers, int seed, MapGridComponent grid,
        [NotNullWhen(true)] out List<(string ID, Vector2 Position)>? decals)
    {
        return TryGetDecals(indices, layers, seed, grid == null ? null : (grid.Owner, grid), out decals);
    }

    private FastNoiseLite GetNoise(FastNoiseLite seedNoise, int seed)
    {
        if (_noiseCache.TryGetValue((seedNoise, seed), out var cached)) // Goob - Cache Noise
            return cached;

        var noiseCopy = new FastNoiseLite();
        _serManager.CopyTo(seedNoise, ref noiseCopy, notNullableOverride: true);
        noiseCopy.SetSeed(noiseCopy.GetSeed() + seed);
        // Ensure re-calculate is run.
        noiseCopy.SetFractalOctaves(noiseCopy.GetFractalOctaves());
        _noiseCache[(seedNoise, seed)] = noiseCopy; // Goob - Cache Noise
        return noiseCopy;
    }

    /// <summary>
    /// Orthogonal lattice maze. Every cell has a passage (binary tree); optional loops.
    /// Dark = walls on the lattice; light = open corridor cells.
    /// </summary>
    private static bool IsMazeWall(Vector2i indices, BiomeMazeEntityLayer layer, int seed)
    {
        return IsMazeWall(indices, seed, layer.CellSize, layer.WallThickness, layer.LoopChance, layer.PillarChance);
    }

    private static bool IsMazeWall(
        Vector2i indices,
        int seed,
        int cellSize,
        int wallThickness,
        float loopChance,
        float pillarChance)
    {
        cellSize = Math.Max(3, cellSize);
        var thickness = Math.Clamp(wallThickness, 1, cellSize - 1);

        var lx = PositiveMod(indices.X, cellSize);
        var ly = PositiveMod(indices.Y, cellSize);
        var cx = FloorDiv(indices.X, cellSize);
        var cy = FloorDiv(indices.Y, cellSize);

        var onVert = lx < thickness;
        var onHoriz = ly < thickness;

        // Open corridor interior (light) — only rare pillars.
        if (!onVert && !onHoriz)
            return Hash01(seed, indices.X, indices.Y, 17) < pillarChance;

        // Lattice corner / intersection — always wall.
        if (onVert && onHoriz)
            return true;

        if (onVert)
        {
            // Vertical lattice at column cx = east wall of cell (cx - 1, cy).
            return !IsVerticalPassage(seed, cx - 1, cy, loopChance);
        }

        // Horizontal lattice at row cy = north wall of cell (cx, cy - 1).
        return !IsHorizontalPassage(seed, cx, cy - 1, loopChance);
    }

    private static bool IsAdjacentToMazeWall(
        Vector2i indices,
        int seed,
        int cellSize,
        int wallThickness,
        float loopChance,
        float pillarChance)
    {
        return IsMazeWall(indices + new Vector2i(1, 0), seed, cellSize, wallThickness, loopChance, pillarChance)
               || IsMazeWall(indices + new Vector2i(-1, 0), seed, cellSize, wallThickness, loopChance, pillarChance)
               || IsMazeWall(indices + new Vector2i(0, 1), seed, cellSize, wallThickness, loopChance, pillarChance)
               || IsMazeWall(indices + new Vector2i(0, -1), seed, cellSize, wallThickness, loopChance, pillarChance);
    }

    /// <summary>
    /// Offset from the corridor tile to a neighbouring maze wall tile.
    /// If several walls touch the tile, picks one stably from the seed.
    /// </summary>
    protected static bool TryGetNearestMazeWallOffset(
        Vector2i indices,
        int seed,
        int cellSize,
        int wallThickness,
        float loopChance,
        float pillarChance,
        out Vector2i wallOffset)
    {
        // Prefer cardinal order with a stable hash so chunk reloads match.
        ReadOnlySpan<Vector2i> dirs =
        [
            new Vector2i(0, 1),
            new Vector2i(1, 0),
            new Vector2i(0, -1),
            new Vector2i(-1, 0),
        ];

        Vector2i? best = null;
        var bestScore = float.MaxValue;

        foreach (var dir in dirs)
        {
            if (!IsMazeWall(indices + dir, seed, cellSize, wallThickness, loopChance, pillarChance))
                continue;

            var score = Hash01(seed, indices.X, indices.Y, 91 + dir.X * 5 + dir.Y * 11);
            if (best != null && score >= bestScore)
                continue;

            best = dir;
            bestScore = score;
        }

        if (best == null)
        {
            wallOffset = default;
            return false;
        }

        wallOffset = best.Value;
        return true;
    }

    /// <summary>Binary tree: each cell carves East or North; LoopChance also opens the other.</summary>
    private static bool CarvesEast(int seed, int cx, int cy)
    {
        return Hash01(seed, cx, cy, 41) < 0.5f;
    }

    private static bool IsVerticalPassage(int seed, int cx, int cy, float loopChance)
    {
        // East wall of cell (cx, cy).
        if (CarvesEast(seed, cx, cy))
            return true;

        // Loop: also open east even when tree chose north.
        return Hash01(seed, cx, cy, 43) < loopChance;
    }

    private static bool IsHorizontalPassage(int seed, int cx, int cy, float loopChance)
    {
        // North wall of cell (cx, cy).
        if (!CarvesEast(seed, cx, cy))
            return true;

        return Hash01(seed, cx, cy, 47) < loopChance;
    }

    private static int FloorDiv(int a, int b)
    {
        return a >= 0 ? a / b : (a - (b - 1)) / b;
    }

    private static int PositiveMod(int a, int b)
    {
        var r = a % b;
        return r < 0 ? r + b : r;
    }

    /// <summary>Deterministic 0..1 hash from seed + coords.</summary>
    private static float Hash01(int seed, int x, int y, int salt)
    {
        unchecked
        {
            var h = (uint)seed;
            h = (h ^ (uint)x) * 0x9E3779B1u;
            h = (h ^ (uint)y) * 0x85EBCA6Bu;
            h = (h ^ (uint)salt) * 0xC2B2AE35u;
            h ^= h >> 16;
            return (h & 0xFFFFFFu) / 16777215f;
        }
    }
}
