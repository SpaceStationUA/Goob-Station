using Content.Server._DV.GlimmerWisp;
using Content.Server.NPC.Pathfinding;
using Content.Shared.Interaction;
using Content.Shared.NPC.Systems;
using System.Threading;
using System.Threading.Tasks;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators.Specific;

public sealed partial class PickDrainTargetOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entMan = default!;

    private EntityLookupSystem _lookup = default!;
    private LifeDrainerSystem _drainer = default!;
    private NpcFactionSystem _faction = default!;
    private PathfindingSystem _pathfinding = default!;

    private EntityQuery<LifeDrainerComponent> _drainerQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    [DataField(required: true)]
    public string RangeKey = string.Empty;

    [DataField(required: true)]
    public string TargetKey = string.Empty;

    [DataField(required: true)]
    public string DrainKey = string.Empty;

    /// <summary>
    /// Where the pathfinding result will be stored (if applicable). This gets removed after execution.
    /// </summary>
    [DataField]
    public string PathfindKey = NPCBlackboard.PathfindKey;

    public override void Initialize(IEntitySystemManager sysMan)
    {
        base.Initialize(sysMan);

        _lookup = sysMan.GetEntitySystem<EntityLookupSystem>();
        _drainer = sysMan.GetEntitySystem<LifeDrainerSystem>();
        _faction = sysMan.GetEntitySystem<NpcFactionSystem>();
        _pathfinding = sysMan.GetEntitySystem<PathfindingSystem>();

        _drainerQuery = _entMan.GetEntityQuery<LifeDrainerComponent>();
        _xformQuery = _entMan.GetEntityQuery<TransformComponent>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        if (!_drainerQuery.TryComp(owner, out var drainer))
            return (false, null);

        var ent = (owner, drainer);

        if (!blackboard.TryGetValue<float>(RangeKey, out var range, _entMan))
            return (false, null);

        // find crit psionics nearby. Planning doesn't require adjacency - the
        // MoveToOperator in the compound will walk us to the target first.
        foreach (var target in _faction.GetNearbyHostiles(owner, range))
        {
            if (!_drainer.CanTarget(ent, target))
                continue;

            if (!_xformQuery.TryComp(target, out var xform))
                continue;

            // pathfind all the way up to the target so we're in range to start
            // draining once we arrive (don't use the search range here - that
            // would leave us short of the target).
            var targetCoords = xform.Coordinates;
            var path = await _pathfinding.GetPath(owner, target, SharedInteractionSystem.InteractionRange, cancelToken);
            if (path.Result != PathResult.Path)
                continue;

            return (true, new Dictionary<string, object>()
            {
                { TargetKey, targetCoords },
                { DrainKey, target },
                { PathfindKey, path}
            });
        }

        return (false, null);
    }
}
