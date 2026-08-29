// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Pathfinding;
using Content.Shared._Pirate.Silicons;
using Content.Shared.Interaction;

namespace Content.Server._Pirate.Silicons.HTN;

/// <summary>
/// Selects one explicit commander waypoint for an assigned bot. It performs no entity lookup.
/// </summary>
[DataDefinition]
public sealed partial class PickPatrolCoordinateOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    [DataField(required: true)]
    public string TargetMoveKey = string.Empty;

    private PathfindingSystem _pathfinding = default!;
    private EntityQuery<PatrolSlaveComponent> _slaveQuery;
    private EntityQuery<PatrolCommanderComponent> _commanderQuery;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pathfinding = sysManager.GetEntitySystem<PathfindingSystem>();
        _slaveQuery = _entityManager.GetEntityQuery<PatrolSlaveComponent>();
        _commanderQuery = _entityManager.GetEntityQuery<PatrolCommanderComponent>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        if (!_slaveQuery.TryComp(owner, out var slave) ||
            slave.MasterEntity is not { } commanderUid ||
            !_commanderQuery.TryComp(commanderUid, out var commander) ||
            !commander.IsPatrolling)
        {
            return (false, null);
        }

        commander.Waypoints.RemoveWhere(uid => !_entityManager.EntityExists(uid));
        var target = SelectNextTarget(blackboard, commanderUid, commander.Waypoints);
        if (!_entityManager.EntityExists(target))
            return (false, null);

        var pathRange = SharedInteractionSystem.InteractionRange - 1f;
        var path = await _pathfinding.GetPath(owner, target, pathRange, cancelToken, PathFlags.Access);
        if (path.Result != PathResult.Path)
            return (false, null);

        return (true, new Dictionary<string, object>
        {
            ["LastPatrolWaypoint"] = target,
            [TargetMoveKey] = _entityManager.GetComponent<TransformComponent>(target).Coordinates,
            [NPCBlackboard.PathfindKey] = path,
        });
    }

    public EntityUid SelectNextTarget(
        NPCBlackboard blackboard,
        EntityUid commander,
        IReadOnlyCollection<EntityUid> waypoints)
    {
        if (waypoints.Count == 0)
            return commander;

        var ordered = waypoints.OrderBy(uid => uid.Id).ToArray();
        if (!blackboard.TryGetValue<EntityUid>("LastPatrolWaypoint", out var last, _entityManager))
            return ordered[0];

        var index = Array.IndexOf(ordered, last);
        return ordered[(index + 1 + ordered.Length) % ordered.Length];
    }
}
