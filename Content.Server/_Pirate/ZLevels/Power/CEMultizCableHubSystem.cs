using Content.Server._Pirate.ZLevels.Atmos.Piping;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.Stack;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared.NodeContainer;
using Robust.Shared.Map;

namespace Content.Server._Pirate.ZLevels.Power;

public sealed class CEMultizCableHubSystem : EntitySystem
{
    [Dependency] private readonly CEMultizAtmosPipeAdapterSystem _pipeAdapter = default!;
    [Dependency] private readonly NodeGroupSystem _nodeGroup = default!;
    [Dependency] private readonly StackSystem _stack = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEMultizCableHubSupportComponent, AnchorStateChangedEvent>(OnHubAnchorChanged);
        SubscribeLocalEvent<CEZLinkedGridComponent, CEMultizLinkedGridPeersChangedEvent>(OnLinkedGridChanged);
    }

    private void OnHubAnchorChanged(Entity<CEMultizCableHubSupportComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored || TerminatingOrDeleted(ent.Owner))
            return;

        foreach (var (stack, amount) in ent.Comp.SupportLossStacks)
        {
            _stack.SpawnAtPosition(amount, stack, args.Transform.Coordinates); // Pirate: multiz - StackSystem.Spawn renamed to SpawnAtPosition upstream
        }

        QueueDel(ent.Owner);
    }

    private void OnLinkedGridChanged(Entity<CEZLinkedGridComponent> ent, ref CEMultizLinkedGridPeersChangedEvent args)
    {
        QueueHubRefloodsOnGrid(ent.Owner);
        _pipeAdapter.QueueAdapterRefloodsOnGrid(ent.Owner);

        foreach (var peerGrid in ent.Comp.PeerGrids.Values)
        {
            QueueHubRefloodsOnGrid(peerGrid);
            _pipeAdapter.QueueAdapterRefloodsOnGrid(peerGrid);
        }
    }

    private void QueueHubRefloodsOnGrid(EntityUid gridUid)
    {
        var query = AllEntityQuery<CEMultizCableHubSupportComponent, NodeContainerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var container, out var xform))
        {
            if (xform.GridUid == gridUid)
                QueueHubRefloods((uid, container));
        }
    }

    private void QueueHubRefloods(Entity<NodeContainerComponent> ent)
    {
        foreach (var node in ent.Comp.Nodes.Values)
        {
            if (node is CEMultizCableHubNode)
                _nodeGroup.QueueReflood(node);
        }
    }
}

[ByRefEvent]
public readonly struct CEMultizLinkedGridPeersChangedEvent;
