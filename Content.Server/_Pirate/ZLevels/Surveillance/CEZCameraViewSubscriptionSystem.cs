using System.Numerics;
using System.Linq;
using Content.Server._Pirate.ZLevels.Core;
using Content.Server.SurveillanceCamera;
using Content.Shared.SurveillanceCamera.Components; // Pirate: multiz - SurveillanceCameraComponent moved to shared upstream
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Content.Shared._Pirate.ZLevels.Surveillance;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.ZLevels.Surveillance;

public sealed class CEZCameraViewSubscriptionSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ViewSubscriberSystem _viewSubscriber = default!;
    [Dependency] private readonly CEZLevelsSystem _zLevels = default!;

    private readonly EntProtoId _zEyeProto = "CEZLevelEye";
    private const float UpdateRate = 0.1f;
    private float _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurveillanceCameraComponent, ViewSubscriberAddedEvent>(OnViewSubscriberAdded);
        SubscribeLocalEvent<SurveillanceCameraComponent, ViewSubscriberRemovedEvent>(OnViewSubscriberRemoved);
        // Remote eyes reuse the same z-level PVS feed as surveillance cameras.
        SubscribeLocalEvent<CEZViewSourceComponent, ViewSubscriberAddedEvent>(OnViewSourceSubscriberAdded);
        SubscribeLocalEvent<CEZViewSourceComponent, ViewSubscriberRemovedEvent>(OnViewSourceSubscriberRemoved);
        SubscribeLocalEvent<CEZCameraViewSubscriptionComponent, ComponentShutdown>(OnSubscriptionShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _nextUpdate -= frameTime;
        if (_nextUpdate > 0f)
            return;

        _nextUpdate = UpdateRate;

        var query = EntityQueryEnumerator<CEZCameraViewSubscriptionComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var subscriptions, out var xform))
        {
            // Drop remote-eye subscribers left behind after aghost, mind transfer, etc.
            var pruneStale = HasComp<CEZViewSourceComponent>(uid);

            var targets = GetZViewTargets((uid, xform));
            foreach (var session in subscriptions.SessionEyes.Keys.ToArray())
            {
                if (pruneStale && !IsLookingThrough(session, uid))
                {
                    _viewSubscriber.RemoveViewSubscriber(uid, session);
                    continue;
                }

                RefreshSessionEyes((uid, subscriptions), session, targets);
            }
        }
    }

    // True while the session's controlled entity is looking through <paramref name="eye"/>.
    private bool IsLookingThrough(ICommonSession session, EntityUid eye)
    {
        return session.AttachedEntity is { } attached
            && TryComp<EyeComponent>(attached, out var eyeComp)
            && eyeComp.Target == eye;
    }

    private void OnViewSubscriberAdded(Entity<SurveillanceCameraComponent> ent, ref ViewSubscriberAddedEvent args)
        => AddViewSource(ent.Owner, args.Subscriber);

    private void OnViewSubscriberRemoved(Entity<SurveillanceCameraComponent> ent, ref ViewSubscriberRemovedEvent args)
        => RemoveViewSource(ent.Owner, args.Subscriber);

    private void OnViewSourceSubscriberAdded(Entity<CEZViewSourceComponent> ent, ref ViewSubscriberAddedEvent args)
        => AddViewSource(ent.Owner, args.Subscriber);

    private void OnViewSourceSubscriberRemoved(Entity<CEZViewSourceComponent> ent, ref ViewSubscriberRemovedEvent args)
        => RemoveViewSource(ent.Owner, args.Subscriber);

    private void AddViewSource(EntityUid uid, ICommonSession subscriber)
    {
        if (!TryComp<TransformComponent>(uid, out var xform))
            return;

        var subscriptions = EnsureComp<CEZCameraViewSubscriptionComponent>(uid);
        RefreshSessionEyes((uid, subscriptions), subscriber, GetZViewTargets((uid, xform)));
    }

    private void RemoveViewSource(EntityUid uid, ICommonSession subscriber)
    {
        if (!TryComp<CEZCameraViewSubscriptionComponent>(uid, out var subscriptions))
            return;

        RemoveSessionEyes(subscriber, subscriptions);

        if (subscriptions.SessionEyes.Count == 0)
            RemCompDeferred<CEZCameraViewSubscriptionComponent>(uid);
    }

    private void OnSubscriptionShutdown(Entity<CEZCameraViewSubscriptionComponent> ent, ref ComponentShutdown args)
    {
        foreach (var session in ent.Comp.SessionEyes.Keys.ToArray())
        {
            RemoveSessionEyes(session, ent.Comp);
        }
    }

    private void RefreshSessionEyes(Entity<CEZCameraViewSubscriptionComponent> camera, ICommonSession session, List<ZViewTarget> targets)
    {
        if (camera.Comp.SessionEyes.TryGetValue(session, out var existing) && EyesMatchTargets(existing, targets))
        {
            for (var i = 0; i < existing.Count; i++)
            {
                _transform.SetCoordinates(existing[i], new EntityCoordinates(targets[i].MapUid, targets[i].WorldPosition));
            }

            return;
        }

        RemoveSessionEyes(session, camera.Comp);

        var eyes = new List<EntityUid>(targets.Count);
        foreach (var target in targets)
        {
            var eye = SpawnAtPosition(_zEyeProto, new EntityCoordinates(target.MapUid, target.WorldPosition));
            Transform(eye).GridTraversal = false;
            _viewSubscriber.AddViewSubscriber(eye, session);
            eyes.Add(eye);
        }

        if (eyes.Count != 0)
            camera.Comp.SessionEyes[session] = eyes;
    }

    private bool EyesMatchTargets(List<EntityUid> eyes, List<ZViewTarget> targets)
    {
        if (eyes.Count != targets.Count)
            return false;

        for (var i = 0; i < eyes.Count; i++)
        {
            if (!TryComp<TransformComponent>(eyes[i], out var xform) ||
                xform.MapUid != targets[i].MapUid)
            {
                return false;
            }
        }

        return true;
    }

    private void RemoveSessionEyes(ICommonSession session, CEZCameraViewSubscriptionComponent subscriptions)
    {
        if (!subscriptions.SessionEyes.Remove(session, out var eyes))
            return;

        foreach (var eye in eyes)
        {
            _viewSubscriber.RemoveViewSubscriber(eye, session);
            QueueDel(eye);
        }
    }

    private List<ZViewTarget> GetZViewTargets(Entity<TransformComponent> camera)
    {
        var targets = new List<ZViewTarget>();

        for (var i = 1; i <= CESharedZLevelsSystem.MaxZLevelsBelowRendering; i++)
        {
            if (!TryResolveCameraZViewTarget(camera, -i, out var target))
                break;

            targets.Add(target);
        }

        if (TryResolveCameraZViewTarget(camera, 1, out var aboveTarget))
            targets.Add(aboveTarget);

        return targets;
    }

    private bool TryResolveCameraZViewTarget(Entity<TransformComponent> camera, int depthOffset, out ZViewTarget target)
    {
        target = default;

        if (camera.Comp.GridUid is { } gridUid &&
            TryComp<CEZLinkedGridComponent>(gridUid, out var linked))
        {
            var targetDepth = linked.Depth + depthOffset;

            if (!linked.PeerGrids.TryGetValue(targetDepth, out var peerGridUid))
                return false;

            if (TryComp<TransformComponent>(peerGridUid, out var peerXform) &&
                peerXform.MapUid is { } peerMapUid)
            {
                target = new ZViewTarget(peerMapUid, GetPeerGridWorldPosition(camera.Comp, gridUid, peerGridUid));
                return true;
            }

            return false;
        }

        if (camera.Comp.MapUid is not { } currentMapUid ||
            !_zLevels.TryMapOffset((currentMapUid, null), depthOffset, out var targetMapUid))
        {
            return false;
        }

        target = new ZViewTarget(targetMapUid.Value.Owner, _transform.GetWorldPosition(camera.Comp));
        return true;
    }

    private Vector2 GetPeerGridWorldPosition(TransformComponent cameraXform, EntityUid currentGridUid, EntityUid peerGridUid)
    {
        var worldPosition = _transform.GetWorldPosition(cameraXform);
        var currentGridMatrix = _transform.GetWorldMatrix(currentGridUid);
        var peerGridMatrix = _transform.GetWorldMatrix(peerGridUid);

        if (!Matrix3x2.Invert(currentGridMatrix, out var inverseCurrentGrid))
            return worldPosition;

        var localPosition = Vector2.Transform(worldPosition, inverseCurrentGrid);
        return Vector2.Transform(localPosition, peerGridMatrix);
    }

    private readonly record struct ZViewTarget(EntityUid MapUid, Vector2 WorldPosition);
}
