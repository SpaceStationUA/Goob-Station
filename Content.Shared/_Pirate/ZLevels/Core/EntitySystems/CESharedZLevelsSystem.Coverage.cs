using Content.Shared._Pirate.ZLevels.Core.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._Pirate.ZLevels.Core.EntitySystems;

public readonly record struct CEZGridCoverage(HashSet<EntityUid> GridUids, MapId FallbackMapId, bool HasGrid);

public abstract partial class CESharedZLevelsSystem
{
    public HashSet<EntityUid> GetLinkedGrids(EntityUid gridUid)
    {
        var grids = new HashSet<EntityUid> { gridUid };

        if (!TryComp<CEZLinkedGridComponent>(gridUid, out var linked))
            return grids;

        foreach (var peerGrid in linked.PeerGrids.Values)
        {
            grids.Add(peerGrid);
        }

        return grids;
    }

    /// <summary>Checks whether two grids share a z-network without allocating a linked-grid set.</summary>
    public bool AreGridsLinked(EntityUid gridA, EntityUid gridB)
    {
        if (gridA == gridB)
            return true;

        return TryComp<CEZLinkedGridComponent>(gridA, out var linked) && linked.PeerGrids.ContainsValue(gridB);
    }

    public CEZGridCoverage GetGridCoverage(EntityUid source, TransformComponent? xform = null)
    {
        xform ??= Transform(source);

        if (TryGetEffectiveGrid(source, xform, out var gridUid))
            return new CEZGridCoverage(GetLinkedGrids(gridUid), xform.MapID, true);

        return new CEZGridCoverage([], xform.MapID, false);
    }

    public bool IsInCoverage(CEZGridCoverage coverage, EntityUid target, TransformComponent? targetXform = null)
    {
        targetXform ??= Transform(target);

        if (coverage.HasGrid)
            return TryGetEffectiveGrid(target, targetXform, out var gridUid) && coverage.GridUids.Contains(gridUid);

        return targetXform.MapID == coverage.FallbackMapId;
    }

    public HashSet<MapId> GetCoverageMapIds(EntityUid source, TransformComponent? xform = null)
    {
        var coverage = GetGridCoverage(source, xform);

        if (!coverage.HasGrid)
            return [coverage.FallbackMapId];

        var maps = new HashSet<MapId>();
        foreach (var gridUid in coverage.GridUids)
        {
            if (TryComp<TransformComponent>(gridUid, out var gridXform))
                maps.Add(gridXform.MapID);
        }

        return maps;
    }

    private bool TryGetEffectiveGrid(EntityUid uid, TransformComponent xform, out EntityUid gridUid)
    {
        if (xform.GridUid is { } directGrid)
        {
            gridUid = directGrid;
            return true;
        }

        if (HasComp<MapGridComponent>(uid))
        {
            gridUid = uid;
            return true;
        }

        var parent = xform.ParentUid;
        var remaining = 16;
        while (parent.IsValid() && remaining-- > 0)
        {
            if (HasComp<MapGridComponent>(parent))
            {
                gridUid = parent;
                return true;
            }

            if (!TryComp<TransformComponent>(parent, out var parentXform))
                break;

            if (parentXform.GridUid is { } parentGrid)
            {
                gridUid = parentGrid;
                return true;
            }

            if (parent == parentXform.ParentUid)
                break;

            parent = parentXform.ParentUid;
        }

        gridUid = EntityUid.Invalid;
        return false;
    }
}
