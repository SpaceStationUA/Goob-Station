using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.Nodes;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared.NodeContainer;
using Robust.Shared.Map.Components;

namespace Content.Server._Pirate.ZLevels.Power;

[DataDefinition]
public sealed partial class CEMultizCableHubNode : CableDeviceNode
{
    private static readonly int[] DepthOffsets = [-1, 1];

    public override IEnumerable<Node> GetReachableNodes(
        TransformComponent xform,
        EntityQuery<NodeContainerComponent> nodeQuery,
        EntityQuery<TransformComponent> xformQuery,
        MapGridComponent? grid,
        IEntityManager entMan)
    {
        foreach (var node in base.GetReachableNodes(xform, nodeQuery, xformQuery, grid, entMan))
        {
            yield return node;
        }

        if (!xform.Anchored ||
            grid == null ||
            xform.GridUid is not { } gridUid ||
            !entMan.TryGetComponent<CEZLinkedGridComponent>(gridUid, out var linked))
        {
            yield break;
        }

        var mapSys = entMan.System<SharedMapSystem>();
        var transformSys = entMan.System<SharedTransformSystem>();
        var worldPos = transformSys.GetWorldPosition(xform);

        foreach (var depthOffset in DepthOffsets)
        {
            if (!linked.PeerGrids.TryGetValue(linked.Depth + depthOffset, out var peerGridUid) ||
                !entMan.TryGetComponent<MapGridComponent>(peerGridUid, out var peerGrid))
            {
                continue;
            }

            // Peer decks may have a different transform than the source grid; reproject via world pos.
            var peerTile = mapSys.WorldToTile(peerGridUid, peerGrid, worldPos);
            foreach (var node in NodeHelpers.GetNodesInTile(nodeQuery, peerGrid, peerTile))
            {
                if (node is CEMultizCableHubNode && node != this && node.NodeGroupID == NodeGroupID)
                    yield return node;
            }
        }
    }
}
