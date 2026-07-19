/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Content.Shared.ActionBlocker;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Dynamics.Joints;
using Robust.Shared.Timing;

namespace Content.Shared._Pirate.ZLevels.Pulling;

public sealed class CEZLevelPullingSystem : EntitySystem
{
    private const float PullRangeTolerance = 0.15f;

    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly CESharedZLevelsSystem _zLevels = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActivePullerComponent, CEZLevelBeforeMapMoveEvent>(OnPullerMove);
        SubscribeLocalEvent<CEZLevelPullCarryComponent, CEZLevelMapMoveEvent>(OnPullerMoved);
    }

    private void OnPullerMove(Entity<ActivePullerComponent> ent, ref CEZLevelBeforeMapMoveEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (!TryComp<PullerComponent>(ent, out var puller) || puller.Pulling == null)
            return;

        var pulledEntity = puller.Pulling.Value;
        var pullDistance = (_transform.GetWorldPosition(pulledEntity) - _transform.GetWorldPosition(ent)).Length();

        // This fires before the puller reparents to the target z-map, while no grid traversal is in
        // progress. Record the carry so the matching CEZLevelMapMoveEvent (raised after the puller
        // is on its final tile, guard clear) can move the pulled entity straight there and rebuild
        // the pull — all in this same tick, so the mob never lingers a frame on the old level.
        var carry = EnsureComp<CEZLevelPullCarryComponent>(ent);
        carry.Pulled = pulledEntity;
        carry.Offset = args.Offset;
        carry.TargetZLevel = args.CurrentZLevel;
        carry.PullDistance = pullDistance;

        // Stop the pull now so the puller crosses alone: a distance joint can't span two maps — the
        // engine nukes a cross-map joint mid-move and cascades a StopPulling (breaking the pull and
        // dropping the held virtual item), and an active joint / held virtual item is what tripped
        // the "ineligible bodies" and re-entrant grid-traversal errors during the puller's move.
        if (TryComp<PullableComponent>(pulledEntity, out var pullable) && pullable.Puller == ent.Owner)
            _pulling.TryStopPull(pulledEntity, pullable, ent.Owner, ignoreGrab: true);

        Log.Debug(
            $"[ZPull] puller={ToPrettyString(ent)} crossing offset={args.Offset} targetZ={args.CurrentZLevel} " +
            $"pulled={ToPrettyString(pulledEntity)} dist={pullDistance:F2}; pull stopped, carry queued");
    }

    private void OnPullerMoved(Entity<CEZLevelPullCarryComponent> ent, ref CEZLevelMapMoveEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        var comp = ent.Comp;
        var pulled = comp.Pulled;
        RemComp<CEZLevelPullCarryComponent>(ent);

        if (!Exists(pulled))
            return;

        // The puller is now on its final landing tile (solid ground after an ascent, the stair/exit
        // tile after a descent). Carry the pulled entity straight there in this same tick — a single
        // move with no intermediate frame on the old level, so no twitch — then rebuild the pull.
        _zLevels.TeleportToZLevelCoordinates(pulled, Transform(ent).Coordinates, comp.TargetZLevel, comp.Offset);

        if (TryComp<CEZPhysicsComponent>(pulled, out _))
            _zLevels.NormalizeTransferredPullable(pulled, comp.Offset);

        if (Transform(pulled).MapUid != Transform(ent).MapUid)
            return;

        if (!_actionBlocker.CanInteract(ent.Owner, pulled))
            return;

        if (TryComp<PullableComponent>(pulled, out var pullable) && pullable.Puller != null)
            _pulling.TryStopPull(pulled, pullable, ent.Owner, true);

        if (_pulling.TryStartPull(ent.Owner, pulled, force: true))
        {
            RestorePullRange(pulled, comp.PullDistance);
        }
        else
        {
            Log.Warning(
                $"[ZPull] failed to resume pull of {ToPrettyString(pulled)} by {ToPrettyString(ent)} " +
                $"(pulledMap={Transform(pulled).MapID} pullerMap={Transform(ent).MapID})");
        }
    }

    private void RestorePullRange(EntityUid pulled, float pullDistance)
    {
        if (pullDistance <= 0.001f ||
            !TryComp<PullableComponent>(pulled, out var pullable) ||
            pullable.PullJointId is not { } jointId ||
            !TryComp<JointComponent>(pulled, out var jointComp) ||
            !jointComp.GetJoints.TryGetValue(jointId, out var joint) ||
            joint is not DistanceJoint distanceJoint)
        {
            return;
        }

        distanceJoint.Length = pullDistance;
        distanceJoint.MaxLength = pullDistance + PullRangeTolerance;
        distanceJoint.MinLength = 0f;
    }
}
