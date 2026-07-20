/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Probe-eye PVS subsystem: per-player probe budget, opening-gated lower probes, stair preview,
// per-eye PvsScale sync. lanos adds TryResolveViewerMap (linked-grid peer reprojection) and
// fall popups (player + item).

using System.Numerics;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Content.Shared.Actions;
using Content.Shared.CCVar;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.ZLevels.Core;

public sealed partial class CEZLevelsSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly ViewSubscriberSystem _viewSubscriber = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;

    private readonly EntProtoId _zEyeProto = "CEZLevelEye";

    /// <summary>Tile-radius for the opening-near probe check. ~Half a normal viewport so probes wake just before a hole comes on-screen.</summary>
    private const int ZProbeOpeningTileRadius = 24;
    private const float StairPreviewProbeRadius = 5f;

    private int _maxViewProbesPerPlayer = 5;
    private float _minProbePvsScale = 1f;
    private TimeSpan _zLevelViewerUpdateRate = TimeSpan.FromSeconds(0.25f);
    private TimeSpan _nextZLevelViewerUpdate = TimeSpan.Zero;

    // viewer -> depth -> probe eye entity. Explicit depth keying lets us add/remove individual
    // probes without recreating the whole set every tick.
    private readonly Dictionary<EntityUid, Dictionary<int, EntityUid>> _viewerProbeEyes = new();
    private readonly Dictionary<EntityUid, (EntityUid Viewer, int Depth)> _probeEyeIndex = new();

    private readonly List<int> _wantedProbeDepths = new();
    private readonly List<int> _probeDepthsToRemove = new();
    private readonly List<Vector2> _stairPreviewPositions = new(CEZLevelViewerComponent.MaxStairPreviewPositions);

    private EntityQuery<CEZLevelHighGroundComponent> _viewHighGroundQuery;

    /// <summary>
    /// When true, <see cref="OnViewerMapUidChanged"/> is skipped. Set while a shuttle is being flown
    /// between z-levels: that relocates the grid (and aboard viewers) via a recursive ChangeMapId, and
    /// rebuilding viewer probes mid-recursion spawns entities into the children collection currently
    /// being enumerated. The periodic <see cref="UpdateView"/> poll rebuilds the probes right after.
    /// </summary>
    internal bool SuppressViewerMapChange;

    private void InitView()
    {
        _viewHighGroundQuery = GetEntityQuery<CEZLevelHighGroundComponent>();

        Subs.CVar(_config, CCVars.CEZMaxViewProbesPerPlayer, OnMaxViewProbesChanged, true);
        Subs.CVar(_config, CCVars.CEZMinProbePvsScale, OnMinProbePvsScaleChanged, true);
        Subs.CVar(_config, CCVars.CEZProbeUpdateHz, OnProbeUpdateHzChanged, true);

        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);

        SubscribeLocalEvent<CEZLevelViewerComponent, MapInitEvent>(OnViewerInit);
        SubscribeLocalEvent<CEZLevelViewerComponent, ComponentRemove>(OnCompRemove);
        SubscribeLocalEvent<CEZLevelViewerComponent, MetaFlagRemoveAttemptEvent>(OnViewerMetaFlagRemoveAttempt);
        SubscribeLocalEvent<CEZLevelViewerComponent, MapUidChangedEvent>(OnViewerMapUidChanged);
        SubscribeLocalEvent<CEZLevelViewerComponent, EntParentChangedMessage>(OnViewerParentChange);
        SubscribeLocalEvent<EyeComponent, EntityTerminatingEvent>(OnEyeTerminating);
        SubscribeLocalEvent<CEZPhysicsComponent, CEZLevelFallMapEvent>(OnZLevelFall);
        SubscribeLocalEvent<CEZItemPhysicsComponent, CEZLevelFallMapEvent>(OnItemZLevelFall);
    }

    private void UpdateView(float frameTime)
    {
        if (_timing.CurTime < _nextZLevelViewerUpdate)
            return;
        _nextZLevelViewerUpdate = _timing.CurTime + _zLevelViewerUpdateRate;

        var query = EntityQueryEnumerator<CEZLevelViewerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var viewer, out var xform))
        {
            UpdateLookUpAction((uid, viewer), xform);
            SyncViewerProbes((uid, viewer), xform);
        }
    }

    private void OnViewerInit(Entity<CEZLevelViewerComponent> ent, ref MapInitEvent args)
    {
        UpdateLookUpAction(ent);
        _meta.AddFlag(ent, MetaDataFlags.ExtraTransformEvents);
    }

    private void OnCompRemove(Entity<CEZLevelViewerComponent> ent, ref ComponentRemove args)
    {
        _actions.RemoveAction(ent.Comp.ZLevelActionEntity);
        _meta.RemoveFlag(ent, MetaDataFlags.ExtraTransformEvents);

        QueueDeleteViewerProbeEyes(ent);
    }

    // Guard so nothing else strips ExtraTransformEvents and silently breaks MapUidChangedEvent
    // delivery while the viewer is still alive.
    private void OnViewerMetaFlagRemoveAttempt(Entity<CEZLevelViewerComponent> ent, ref MetaFlagRemoveAttemptEvent args)
    {
        if ((args.ToRemove & MetaDataFlags.ExtraTransformEvents) != 0 &&
            ent.Comp.LifeStage <= ComponentLifeStage.Running)
        {
            args.ToRemove &= ~MetaDataFlags.ExtraTransformEvents;
        }
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        var viewer = EnsureComp<CEZLevelViewerComponent>(ev.Entity);
        UpdateLookUpAction((ev.Entity, viewer));
        SyncViewerProbes((ev.Entity, viewer));
    }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        RemComp<CEZLevelViewerComponent>(ev.Entity);
    }

    private void OnViewerMapUidChanged(Entity<CEZLevelViewerComponent> ent, ref MapUidChangedEvent args)
    {
        if (SuppressViewerMapChange)
            return;

        // UpdateLookUpAction is intentionally omitted: AddAction creates a child entity, which
        // violates ChangeMapIdRecursive's assert that ChildCount stays constant during MapUidChangedEvent.
        // The UpdateView poll handles the action update with negligible delay.
        ClearAllViewerProbes(ent);
        SyncViewerProbes(ent);
    }

    private void OnViewerParentChange(Entity<CEZLevelViewerComponent> ent, ref EntParentChangedMessage args)
    {
        ClearAllViewerProbes(ent);
        SyncViewerProbes(ent);
    }

    private void UpdateLookUpAction(Entity<CEZLevelViewerComponent> ent, TransformComponent? xform = null)
    {
        if (!Resolve(ent, ref xform, false))
            return;

        if (TryResolveViewerMap((ent.Owner, xform), 1, out _))
        {
            if (ent.Comp.ZLevelActionEntity is { } existing && Exists(existing))
                return;

            ent.Comp.ZLevelActionEntity = null;
            _actions.AddAction(ent, ref ent.Comp.ZLevelActionEntity, ent.Comp.ActionProto);
            DirtyField(ent, ent.Comp, nameof(CEZLevelViewerComponent.ZLevelActionEntity));
            return;
        }

        if (ent.Comp.LookUp)
        {
            ent.Comp.LookUp = false;
            DirtyField(ent, ent.Comp, nameof(CEZLevelViewerComponent.LookUp));
        }

        if (ent.Comp.ZLevelActionEntity is not { } action)
            return;

        _actions.RemoveAction(action);
        ent.Comp.ZLevelActionEntity = null;
        DirtyField(ent, ent.Comp, nameof(CEZLevelViewerComponent.ZLevelActionEntity));
    }

    private void SyncViewerProbes(Entity<CEZLevelViewerComponent> ent, TransformComponent? xform = null)
    {
        if (_maxViewProbesPerPlayer <= 0 || !HasComp<ActorComponent>(ent))
        {
            ClearAllViewerProbes(ent);
            return;
        }

        if (!Resolve(ent, ref xform, false))
        {
            ClearAllViewerProbes(ent);
            return;
        }

        if (xform.MapUid is not { } map)
        {
            ClearAllViewerProbes(ent);
            return;
        }

        var globalPos = _transform.GetWorldPosition(xform);
        var eyeOffset = GetViewerProbeOffset(ent);
        var probeGlobalPos = globalPos + eyeOffset;

        // Compute stair-preview state first so the +1 probe can be retargeted at the stairwell
        // origin instead of straight overhead.
        var stairPreviewUp = CanPreviewUpperZFromStair((ent.Owner, ent.Comp), xform, map, globalPos, _stairPreviewPositions);
        SetStairPreviewUp(ent, stairPreviewUp, _stairPreviewPositions);

        BuildWantedProbeDepths((ent.Owner, xform), probeGlobalPos, _wantedProbeDepths, stairPreviewUp);

        if (!_viewerProbeEyes.TryGetValue(ent.Owner, out var probes))
        {
            probes = new Dictionary<int, EntityUid>();
            _viewerProbeEyes[ent.Owner] = probes;
        }

        // Remove probes no longer wanted, or whose eye terminated externally.
        _probeDepthsToRemove.Clear();
        foreach (var (depth, eye) in probes)
        {
            if (!_wantedProbeDepths.Contains(depth) || TerminatingOrDeleted(eye))
                _probeDepthsToRemove.Add(depth);
        }

        foreach (var depth in _probeDepthsToRemove)
        {
            if (!probes.Remove(depth, out var eye))
                continue;

            ent.Comp.Eyes.Remove(eye);
            QueueDeleteProbeEye(eye);
        }

        if (!TryComp<ActorComponent>(ent, out var actor))
        {
            _wantedProbeDepths.Clear();
            _probeDepthsToRemove.Clear();
            return;
        }

        foreach (var depth in _wantedProbeDepths)
        {
            if (!TryResolveViewerMap((ent.Owner, xform), depth, out var target))
                continue;

            var probePosition = GetProbeWorldPosition(ent.Comp, depth, target.WorldPosition, eyeOffset);

            if (probes.TryGetValue(depth, out var existingEye) && !TerminatingOrDeleted(existingEye))
            {
                if (Transform(existingEye).MapUid != target.MapUid)
                {
                    // Adjacent map changed (FTL, linked-grid retarget) — recreate the probe.
                    probes.Remove(depth);
                    ent.Comp.Eyes.Remove(existingEye);
                    QueueDeleteProbeEye(existingEye);
                }
                else
                {
                    _transform.SetWorldPosition(existingEye, probePosition);
                    SyncZLevelEye(ent, existingEye);
                    continue;
                }
            }

            var newEye = SpawnAtPosition(_zEyeProto, new EntityCoordinates(target.MapUid, probePosition));
            Transform(newEye).GridTraversal = false;
            SyncZLevelEye(ent, newEye);

            probes[depth] = newEye;
            _probeEyeIndex[newEye] = (ent.Owner, depth);
            ent.Comp.Eyes.Add(newEye);
            _viewSubscriber.AddViewSubscriber(newEye, actor.PlayerSession);
        }

        _wantedProbeDepths.Clear();
        _probeDepthsToRemove.Clear();
    }

    /// <summary>
    /// Decide which Z-level offsets to keep a probe-eye on, respecting the per-player budget.
    /// Priority: stair-preview +1 first, then -1..-maxDepth as long as openings exist, then a
    /// fallback +1 if there's an opening near above and budget remains.
    /// </summary>
    private void BuildWantedProbeDepths(Entity<TransformComponent> ent, Vector2 globalPos, List<int> depths, bool forceUpperPreview)
    {
        depths.Clear();

        var remainingProbes = _maxViewProbesPerPlayer;
        var upperPreviewReserved = false;

        if (forceUpperPreview &&
            remainingProbes > 0 &&
            TryResolveViewerMap(ent, 1, out _))
        {
            depths.Add(1);
            remainingProbes--;
            upperPreviewReserved = true;
        }

        for (var i = 1; i <= CESharedZLevelsSystem.MaxZLevelsBelowRendering && remainingProbes > 0; i++)
        {
            if (!TryResolveViewerMap(ent, -i, out _))
                break;

            if (!HasZOpeningPath(ent, globalPos, -i))
                break;

            depths.Add(-i);
            remainingProbes--;
        }

        if (remainingProbes <= 0 || upperPreviewReserved)
            return;

        if (!TryResolveViewerMap(ent, 1, out var aboveTarget))
            return;

        // Subscribe above only if there's a hole nearby — otherwise the upper PVS stays cold until
        // the player steps onto a stair or uses LookUp.
        if (!HasZOpeningNear(aboveTarget.MapUid, aboveTarget.WorldPosition))
            return;

        depths.Add(1);
    }

    /// <summary>Walks from the viewer's map toward <paramref name="targetDepth"/>, requiring an opening on every step.</summary>
    private bool HasZOpeningPath(Entity<TransformComponent> ent, Vector2 globalPos, int targetDepth)
    {
        var step = targetDepth < 0 ? -1 : 1;

        for (var depth = 0; depth != targetDepth; depth += step)
        {
            EntityUid checkingMap;
            Vector2 checkingPos;
            if (depth == 0)
            {
                if (ent.Comp.MapUid is not { } currentMap)
                    return false;
                checkingMap = currentMap;
                checkingPos = globalPos;
            }
            else
            {
                if (!TryResolveViewerMap(ent, depth, out var stepTarget))
                    return false;
                checkingMap = stepTarget.MapUid;
                // Use the per-depth reprojected position — globalPos has the wrong XY on linked decks.
                checkingPos = stepTarget.WorldPosition;
            }

            if (!HasZOpeningNear(checkingMap, checkingPos))
                return false;
        }

        return true;
    }

    private bool HasZOpeningNear(EntityUid map, Vector2 globalPos)
    {
        if (!TryComp<TransformComponent>(map, out var mapXform) || mapXform.MapID == MapId.Nullspace)
            return true;

        // Use a spatial grid lookup rather than querying the map entity for MapGridComponent
        // (which only works on planet-style maps).
        if (!_mapMan.TryFindGridAt(mapXform.MapID, globalPos, out var gridUid, out var grid))
            return true;

        var mapCoordinates = new MapCoordinates(globalPos, mapXform.MapID);
        var center = _map.TileIndicesFor(gridUid, grid, mapCoordinates);
        var start = center - new Vector2i(ZProbeOpeningTileRadius, ZProbeOpeningTileRadius);
        var end = center + new Vector2i(ZProbeOpeningTileRadius, ZProbeOpeningTileRadius);

        return HasOpeningInTileBounds((gridUid, grid), start, end);
    }

    /// <summary>
    /// Finds nearby high-ground (stair-like) entities with <c>PreviewUpLevel</c> set that are
    /// LOS-visible to the viewer; their positions become FOV origins for the +1 stair-preview probe.
    /// </summary>
    private bool CanPreviewUpperZFromStair(
        Entity<CEZLevelViewerComponent> viewer,
        TransformComponent viewerXform,
        EntityUid map,
        Vector2 globalPos,
        List<Vector2> previewPositions)
    {
        previewPositions.Clear();

        if (!TryResolveViewerMap((viewer.Owner, viewerXform), 1, out _))
            return false;

        if (!_mapMan.TryFindGridAt(viewerXform.MapID, globalPos, out var gridUid, out var grid))
            return false;

        var origin = new MapCoordinates(globalPos, viewerXform.MapID);
        var centerTile = _map.WorldToTile(gridUid, grid, globalPos);
        var tileRadius = Math.Max(1, (int) MathF.Ceiling(StairPreviewProbeRadius / grid.TileSize));

        for (var x = -tileRadius; x <= tileRadius; x++)
        {
            for (var y = -tileRadius; y <= tileRadius; y++)
            {
                var tile = centerTile + new Vector2i(x, y);
                var query = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
                while (query.MoveNext(out var uid))
                {
                    if (uid is not { } highGroundUid ||
                        !_viewHighGroundQuery.TryComp(highGroundUid, out var highGround) ||
                        !highGround.PreviewUpLevel ||
                        highGround.SupportOnlyFromAbove ||
                        highGround.PreviewRange <= 0f)
                    {
                        continue;
                    }

                    var target = _transform.GetMapCoordinates(highGroundUid);
                    var range = highGround.PreviewRange + 0.05f;
                    if (Vector2.DistanceSquared(origin.Position, target.Position) > range * range)
                        continue;

                    if (_examine.InRangeUnOccluded(origin, target, highGround.PreviewRange,
                            ent => ent == viewer.Owner || ent == highGroundUid))
                    {
                        AddStairPreviewPosition(previewPositions, target.Position);
                        if (previewPositions.Count >= CEZLevelViewerComponent.MaxStairPreviewPositions)
                            return true;
                    }
                }
            }
        }

        return previewPositions.Count > 0;
    }

    private static void AddStairPreviewPosition(List<Vector2> previewPositions, Vector2 position)
    {
        foreach (var existing in previewPositions)
        {
            if (Vector2.DistanceSquared(existing, position) < 0.001f)
                return;
        }

        previewPositions.Add(position);
    }

    private void SetStairPreviewUp(
        Entity<CEZLevelViewerComponent> viewer,
        bool enabled,
        IReadOnlyList<Vector2>? previewPositions = null)
    {
        var changed = false;

        if (viewer.Comp.StairPreviewUp != enabled)
        {
            viewer.Comp.StairPreviewUp = enabled;
            changed = true;
        }

        var count = enabled && previewPositions != null
            ? Math.Min(previewPositions.Count, CEZLevelViewerComponent.MaxStairPreviewPositions)
            : 0;

        if (viewer.Comp.StairPreviewPositionCount != count)
        {
            viewer.Comp.StairPreviewPositionCount = count;
            changed = true;
        }

        for (var i = 0; i < CEZLevelViewerComponent.MaxStairPreviewPositions; i++)
        {
            var position = i < count ? previewPositions![i] : default;
            if (Vector2.DistanceSquared(viewer.Comp.GetStairPreviewPosition(i), position) <= 0.001f)
                continue;

            viewer.Comp.SetStairPreviewPosition(i, position);
            changed = true;
        }

        if (changed)
            Dirty(viewer.Owner, viewer.Comp);
    }

    private Vector2 GetViewerProbeOffset(EntityUid viewer)
    {
        return TryComp<EyeComponent>(viewer, out var eye) ? eye.Offset : Vector2.Zero;
    }

    private static Vector2 GetProbeWorldPosition(CEZLevelViewerComponent viewer, int depth, Vector2 globalPos, Vector2 eyeOffset)
    {
        if (depth == 1 &&
            viewer.StairPreviewUp &&
            !viewer.LookUp &&
            viewer.StairPreviewPositionCount > 0)
        {
            return viewer.StairPreviewPosition;
        }

        return globalPos + eyeOffset;
    }

    /// <summary>Mirrors the viewer's PvsScale (clamped to <see cref="_minProbePvsScale"/>) and visibility mask onto the probe eye.</summary>
    private void SyncZLevelEye(EntityUid viewer, EntityUid zEye)
    {
        var eye = EnsureComp<EyeComponent>(zEye);
        var pvsScale = _minProbePvsScale;

        if (TryComp<EyeComponent>(viewer, out var viewerEye))
        {
            pvsScale = MathF.Max(pvsScale, viewerEye.PvsScale);
            _eye.SetVisibilityMask(zEye, viewerEye.VisibilityMask, eye);
        }

        _eye.SetPvsScale((zEye, eye), pvsScale);
    }

    private void OnEyeTerminating(Entity<EyeComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!_probeEyeIndex.Remove(ent.Owner, out var probe))
            return;

        if (_viewerProbeEyes.TryGetValue(probe.Viewer, out var probes) &&
            probes.TryGetValue(probe.Depth, out var indexedEye) &&
            indexedEye == ent.Owner)
        {
            probes.Remove(probe.Depth);

            if (probes.Count == 0)
                _viewerProbeEyes.Remove(probe.Viewer);
        }

        if (TryComp<CEZLevelViewerComponent>(probe.Viewer, out var viewer))
            viewer.Eyes.Remove(ent.Owner);
    }

    private void QueueDeleteViewerProbeEyes(Entity<CEZLevelViewerComponent> ent)
    {
        if (_viewerProbeEyes.TryGetValue(ent.Owner, out var probes))
        {
            foreach (var eye in probes.Values)
            {
                _probeEyeIndex.Remove(eye);
                QueueDel(eye);
            }

            _viewerProbeEyes.Remove(ent.Owner);
        }

        foreach (var eye in ent.Comp.Eyes)
        {
            _probeEyeIndex.Remove(eye);
            QueueDel(eye);
        }

        ent.Comp.Eyes.Clear();
    }

    private void QueueDeleteProbeEye(EntityUid eye)
    {
        _probeEyeIndex.Remove(eye);
        QueueDel(eye);
    }

    private void ClearAllViewerProbes(Entity<CEZLevelViewerComponent> ent)
    {
        SetStairPreviewUp(ent, false);
        QueueDeleteViewerProbeEyes(ent);
    }

    // --- CVar wiring -------------------------------------------------------------------------

    private void OnMaxViewProbesChanged(int value)
    {
        _maxViewProbesPerPlayer = Math.Max(0, value);
        RefreshAllViewers();
    }

    private void OnMinProbePvsScaleChanged(float value)
    {
        _minProbePvsScale = Math.Clamp(value, 0.1f, 100f);
        RefreshAllViewers();
    }

    private void OnProbeUpdateHzChanged(float value)
    {
        var hz = Math.Clamp(value, 0.1f, 20f);
        _zLevelViewerUpdateRate = TimeSpan.FromSeconds(1f / hz);
    }

    private void RefreshAllViewers()
    {
        var query = EntityQueryEnumerator<CEZLevelViewerComponent>();
        while (query.MoveNext(out var uid, out var viewer))
        {
            ClearAllViewerProbes((uid, viewer));
            SyncViewerProbes((uid, viewer));
        }
    }

    // --- Linked-grid-aware adjacent-map resolution (lanos-specific) ---------------------------

    private bool TryResolveViewerMap(Entity<TransformComponent> ent, int depthOffset, out CEZLevelViewerTarget target)
    {
        target = default;
        var viewerWorld = _transform.GetWorldPosition(ent.Comp);

        if (ent.Comp.GridUid is { } gridUid &&
            TryComp<CEZLinkedGridComponent>(gridUid, out var linked))
        {
            var targetDepth = linked.Depth + depthOffset;

            if (linked.PeerGrids.TryGetValue(targetDepth, out var peerGridUid) &&
                TryComp<TransformComponent>(peerGridUid, out var peerXform) &&
                peerXform.MapUid is { } peerMapUid)
            {
                // Reproject viewer world pos through source grid inverse then peer grid matrix so
                // the eye lines up with the viewer's corresponding tile on the peer deck.
                var srcInv = _transform.GetInvWorldMatrix(gridUid);
                var local = Vector2.Transform(viewerWorld, srcInv);
                var peerWorld = Vector2.Transform(local, _transform.GetWorldMatrix(peerGridUid));
                target = new CEZLevelViewerTarget(peerMapUid, peerWorld);
                return true;
            }
        }

        if (ent.Comp.MapUid is not { } currentMapUid ||
            !TryMapOffset(currentMapUid, depthOffset, out var targetMapUid))
        {
            return false;
        }

        target = new CEZLevelViewerTarget(targetMapUid.Value, viewerWorld);
        return true;
    }

    private readonly record struct CEZLevelViewerTarget(EntityUid MapUid, Vector2 WorldPosition);

    // --- Fall popups (lanos-specific; CMU has only the player version) ------------------------

    private void OnZLevelFall(Entity<CEZPhysicsComponent> ent, ref CEZLevelFallMapEvent args)
    {
        // PredictedPopup on the falling entity from the SERVER: the faller doesn't see the popup,
        // but everyone around them does — which is what we want.
        _popup.PopupPredictedCoordinates(Loc.GetString("ce-zlevel-falling-popup", ("name", Identity.Name(ent, EntityManager))), Transform(ent).Coordinates, ent);
    }

    private void OnItemZLevelFall(Entity<CEZItemPhysicsComponent> ent, ref CEZLevelFallMapEvent args)
    {
        _popup.PopupPredictedCoordinates(Loc.GetString("ce-zlevel-falling-popup", ("name", Identity.Name(ent, EntityManager))), Transform(ent).Coordinates, ent);
    }
}
