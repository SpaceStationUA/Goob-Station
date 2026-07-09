using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Pathfinding;
using Content.Server.NPC.Systems;
using Content.Shared._White.Jump;
using Content.Shared.Throwing;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// Pirate/Starlight: moves an NPC away from the specified target key.
/// </summary>
public sealed partial class MoveFromOperator : HTNOperator, IHtnConditionalShutdown
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private NPCSteeringSystem _steering = default!;
    private PathfindingSystem _pathfind = default!;
    private SharedTransformSystem _transform = default!;
    private ThrowingSystem _throwing = default!;

    private static readonly float[] FleeAngles =
        { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f, 150f, -150f, 180f };

    private const string FleePosKey = "_FleeTargetCoordinates";
    private const float DefaultSafeDistance = 5f;
    private const float FleeDistanceMultiplier = 1.5f;

    [DataField("shutdownState")]
    public HTNPlanState ShutdownState { get; private set; } = HTNPlanState.TaskFinished;

    [DataField]
    public bool PathfindInPlanning = true;

    [DataField]
    public bool RemoveKeyOnFinish = true;

    [DataField]
    public string TargetKey = "TargetCoordinates";

    [DataField]
    public string PathfindKey = NPCBlackboard.PathfindKey;

    [DataField]
    public string RangeKey = "MovementRange";

    [DataField]
    public bool StopOnLineOfSight;

    [DataField]
    public bool UseJump = false;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pathfind = sysManager.GetEntitySystem<PathfindingSystem>();
        _steering = sysManager.GetEntitySystem<NPCSteeringSystem>();
        _transform = sysManager.GetEntitySystem<SharedTransformSystem>();
        _throwing = sysManager.GetEntitySystem<ThrowingSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue<EntityCoordinates>(TargetKey, out var threatCoords, _entManager))
            return (false, null);

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<TransformComponent>(owner, out var xform) ||
            !_entManager.HasComponent<PhysicsComponent>(owner))
            return (false, null);

        var ownerPos = xform.Coordinates;
        var safeDistance = blackboard.GetValueOrDefault<float>(RangeKey, _entManager);
        if (safeDistance == 0f)
            safeDistance = DefaultSafeDistance;

        if (ownerPos.TryDistance(_entManager, threatCoords, out var dist) && dist >= safeDistance)
            return (true, null);

        if (!PathfindInPlanning)
        {
            var rawFlee = ComputeFleePos(ownerPos, threatCoords, safeDistance, 0f);
            return (true, new Dictionary<string, object>
            {
                { FleePosKey, rawFlee },
                { NPCBlackboard.OwnerCoordinates, rawFlee },
            });
        }

        var flags = _pathfind.GetFlags(blackboard);

        foreach (var angleDeg in FleeAngles)
        {
            if (cancelToken.IsCancellationRequested)
                break;

            var fleePos = ComputeFleePos(ownerPos, threatCoords, safeDistance, angleDeg);
            var path = await _pathfind.GetPath(owner, ownerPos, fleePos, safeDistance, cancelToken, flags);

            if (path.Result != PathResult.Path)
                continue;

            return (true, new Dictionary<string, object>
            {
                { FleePosKey, fleePos },
                { PathfindKey, path },
                { NPCBlackboard.OwnerCoordinates, fleePos },
            });
        }

        var randomPath = await _pathfind.GetRandomPath(owner, safeDistance, cancelToken, flags: flags);

        if (randomPath.Result != PathResult.Path)
            return (false, null);

        var randomTarget = randomPath.Path[^1].Coordinates;
        return (true, new Dictionary<string, object>
        {
            { FleePosKey, randomTarget },
            { PathfindKey, randomPath },
            { NPCBlackboard.OwnerCoordinates, randomTarget },
        });
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);

        blackboard.Remove<EntityCoordinates>(NPCBlackboard.OwnerCoordinates);

        var uid = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!blackboard.TryGetValue<EntityCoordinates>(FleePosKey, out var fleePos, _entManager))
        {
            var threatCoords = blackboard.GetValue<EntityCoordinates>(TargetKey);
            var ownerPos = _transform.GetMoverCoordinates(uid);
            var safeDistance = blackboard.GetValueOrDefault<float>(RangeKey, _entManager);
            if (safeDistance == 0f)
                safeDistance = DefaultSafeDistance;
            fleePos = ComputeFleePos(ownerPos, threatCoords, safeDistance, 0f);
        }

        if (UseJump && _entManager.TryGetComponent<JumpComponent>(uid, out var jumpComp))
            _throwing.TryThrow(uid, fleePos, jumpComp.JumpSpeed, uid, 10f);

        var comp = _steering.Register(uid, fleePos);
        comp.ArriveOnLineOfSight = StopOnLineOfSight;

        if (blackboard.TryGetValue<float>(RangeKey, out var range, _entManager))
            comp.Range = range;

        if (blackboard.TryGetValue<PathResultEvent>(PathfindKey, out var result, _entManager))
        {
            var ownerMapCoords = _transform.ToMapCoordinates(_transform.GetMoverCoordinates(uid));
            var fleeMapCoords = _transform.ToMapCoordinates(fleePos);
            var path = result.Path;
            _steering.PrunePath(uid, ownerMapCoords, fleeMapCoords.Position - ownerMapCoords.Position, path);
            comp.CurrentPath = new Queue<PathPoly>(path);
        }
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        if (!_entManager.TryGetComponent<NPCSteeringComponent>(owner, out var steering))
            return HTNOperatorStatus.Failed;

        if (blackboard.TryGetValue<EntityCoordinates>(TargetKey, out var threatCoords, _entManager))
        {
            var xform = _entManager.GetComponent<TransformComponent>(owner);
            if (xform.Coordinates.TryDistance(_entManager, threatCoords, out var dist))
            {
                var safeDistance = blackboard.GetValueOrDefault<float>(RangeKey, _entManager);
                if (dist >= safeDistance)
                    return HTNOperatorStatus.Finished;
            }
        }

        return steering.Status switch
        {
            SteeringStatus.InRange => HTNOperatorStatus.Finished,
            SteeringStatus.NoPath => HTNOperatorStatus.Failed,
            SteeringStatus.Moving => HTNOperatorStatus.Continuing,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public void ConditionalShutdown(NPCBlackboard blackboard)
    {
        blackboard.Remove<PathResultEvent>(PathfindKey);
        blackboard.Remove<EntityCoordinates>(FleePosKey);

        if (RemoveKeyOnFinish)
            blackboard.Remove<EntityCoordinates>(TargetKey);

        _steering.Unregister(blackboard.GetValue<EntityUid>(NPCBlackboard.Owner));
    }

    private static EntityCoordinates ComputeFleePos(
        EntityCoordinates ownerPos,
        EntityCoordinates threatCoords,
        float safeDistance,
        float angleDeg)
    {
        var away = ownerPos.Position - threatCoords.Position;

        if (away == Vector2.Zero)
            away = Vector2.UnitX;
        else
            away = away.Normalized();

        if (angleDeg != 0f)
        {
            var rad = MathHelper.DegreesToRadians(angleDeg);
            var cos = MathF.Cos(rad);
            var sin = MathF.Sin(rad);
            away = new Vector2(
                (away.X * cos) - (away.Y * sin),
                (away.X * sin) + (away.Y * cos));
        }

        return threatCoords.Offset(away * safeDistance * FleeDistanceMultiplier);
    }
}
