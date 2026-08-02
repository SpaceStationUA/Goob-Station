/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._Pirate.ZLevels.Roof;

/// <summary>
/// Maintains roof coverage from tiles on higher Z-levels.
/// </summary>
public abstract class CESharedRoofSystem : EntitySystem
{
    [Dependency] protected readonly CESharedZLevelsSystem ZLevel = default!;
    [Dependency] protected readonly SharedRoofSystem Roof = default!;
    [Dependency] protected readonly SharedMapSystem Map = default!;
    [Dependency] protected readonly ITileDefinitionManager TilDefMan = default!;

    protected EntityQuery<MapGridComponent> GridQuery;
    protected EntityQuery<RoofComponent> RoofQuery;

    public override void Initialize()
    {
        base.Initialize();

        GridQuery = GetEntityQuery<MapGridComponent>();
        RoofQuery = GetEntityQuery<RoofComponent>();

        SubscribeLocalEvent<CEZLevelMapRoofComponent, TileChangedEvent>(OnTileChanged);
    }

    /// <summary>
    /// Propagates changed roof coverage down the Z-level stack.
    /// </summary>
    private void OnTileChanged(Entity<CEZLevelMapRoofComponent> ent, ref TileChangedEvent args)
    {
        if (!GridQuery.TryComp(ent, out var currentMapGrid))
            return;
        if (!RoofQuery.TryComp(ent, out var currentRoof))
            return;
        if (!TryComp<CEZLevelMapComponent>(ent, out var zLevelMapComp))
            return;

        if (args.Changes.Length == 0)
            return;

        Dictionary<Vector2i, bool> roofMap = new();
        foreach (var change in args.Changes)
        {
            var tileDef = (ContentTileDefinition)TilDefMan[change.NewTile.TypeId];

            var roovedAbove = Roof.IsRooved((ent, currentMapGrid, currentRoof), change.GridIndices);
            var roovedTile = !tileDef.ZTransparent;
            // Indexer overwrites; duplicate GridIndices in args.Changes would throw with Add.
            roofMap[change.GridIndices] = roovedAbove || roovedTile;
        }

        var mapsBelow = ZLevel.GetAllMapsBelow((ent, zLevelMapComp));

        if (mapsBelow.Count == 0)
            return;

        foreach (var mapBelow in mapsBelow)
        {
            if (!GridQuery.TryComp(mapBelow, out var mapGridBelow))
                continue;

            var roofBelow = EnsureComp<RoofComponent>(mapBelow);

            List<Vector2i>? promoted = null;
            foreach (var (indices, rooved) in roofMap)
            {
                Roof.SetRoof((mapBelow, mapGridBelow, roofBelow), indices, rooved);

                // Mutate after enumeration; the setter bumps dictionary version.
                if (Map.TryGetTile(mapGridBelow, indices, out var tile) && !tile.IsEmpty)
                    (promoted ??= new List<Vector2i>()).Add(indices);
            }

            if (promoted != null)
            {
                foreach (var indices in promoted)
                    roofMap[indices] = true;
            }
        }
    }
}
