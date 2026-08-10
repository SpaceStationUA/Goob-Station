using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared.NodeContainer;
using Robust.Shared.Map.Components;

namespace Content.Server._Pirate.ZLevels.Atmos.Piping;

public sealed class CEMultizAtmosPipeAdapterSystem : EntitySystem
{
    [Dependency] private readonly NodeGroupSystem _nodeGroup = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEMultizAtmosPipeAdapterComponent, AnchorStateChangedEvent>(OnAdapterAnchorChanged);
    }

    private void OnAdapterAnchorChanged(Entity<CEMultizAtmosPipeAdapterComponent> ent, ref AnchorStateChangedEvent args)
    {
        QueueAdapterRefloodsInColumn(args.Transform);
    }

    public void QueueAdapterRefloodsOnGrid(EntityUid gridUid)
    {
        var query = AllEntityQuery<CEMultizAtmosPipeAdapterComponent, NodeContainerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var container, out var xform))
        {
            if (xform.GridUid == gridUid)
                QueueAdapterRefloods((uid, container));
        }
    }

    private void QueueAdapterRefloods(Entity<NodeContainerComponent> ent)
    {
        foreach (var node in ent.Comp.Nodes.Values)
        {
            if (node is CEMultizAtmosPipeAdapterNode)
                _nodeGroup.QueueReflood(node);
        }
    }

    private void QueueAdapterRefloodsInColumn(TransformComponent xform)
    {
        if (xform.GridUid is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            return;
        }

        var nodeQuery = GetEntityQuery<NodeContainerComponent>();
        var tile = grid.TileIndicesFor(xform.Coordinates);
        QueueAdapterRefloodsInTile(nodeQuery, grid, tile);

        if (!TryComp<CEZLinkedGridComponent>(gridUid, out var linked))
            return;

        // Source-grid tile won't line up on peer decks that have a different transform; reproject via world pos.
        var worldPos = _transform.GetWorldPosition(xform);

        foreach (var peerGridUid in linked.PeerGrids.Values)
        {
            if (!TryComp<MapGridComponent>(peerGridUid, out var peerGrid))
                continue;

            var peerTile = _mapSystem.WorldToTile(peerGridUid, peerGrid, worldPos);
            QueueAdapterRefloodsInTile(nodeQuery, peerGrid, peerTile);
        }
    }

    private void QueueAdapterRefloodsInTile(
        EntityQuery<NodeContainerComponent> nodeQuery,
        MapGridComponent grid,
        Vector2i tile)
    {
        foreach (var node in NodeHelpers.GetNodesInTile(nodeQuery, grid, tile))
        {
            if (node is CEMultizAtmosPipeAdapterNode)
                _nodeGroup.QueueReflood(node);
        }
    }
}
