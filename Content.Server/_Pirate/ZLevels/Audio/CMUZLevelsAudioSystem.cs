// SPDX-FileCopyrightText: 2026 ColonialMarinesUniverse contributors <https://github.com/AU-14/ColonialMarinesUniverse>
// SPDX-License-Identifier: AGPL-3.0-only
// Ported from ColonialMarinesUniverse Content.Server/_CMU14/ZLevels/Core/CMUZLevelsSystem.Audio.cs.
// Re-emits cross-Z audio through floor openings so listeners on adjacent maps hear sound from the
// level above/below. lanos adaptations:
//   * Existing-tile-only opening check (off-grid deck-edge space doesn't count as a hole).
//   * MapInitEvent + MoveEvent both trigger, so PlayPvs(uid) (jukebox-style parenting, no MoveEvent
//     at spawn) gets picked up.
//   * PlayPvs instead of PlayStatic-with-filter so listeners arriving after projection still hear
//     long-lived audio (jukeboxes etc.).
//   * Tracks projections per source so looped audio's projections die with the source.

using System.Numerics;
using Content.Goobstation.Shared.StationRadio.Components;
using Content.Shared._Pirate.Audio;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Content.Shared.CCVar;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;

namespace Content.Server._Pirate.ZLevels.Audio;

public sealed class CMUZLevelsAudioSystem : EntitySystem
{
    private readonly record struct ProjectionTarget(EntityUid MapUid, Vector2 Position);

    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly CESharedZLevelsSystem _zLevels = default!;

    /// <summary>Tile-radius search around the audio source for a floor opening to project through.</summary>
    private const float CrossZAudioOpeningRadius = 1.5f;

    private readonly HashSet<EntityUid> _processed = new();
    private readonly HashSet<EntityUid> _projections = new();
    /// <summary>source audio entity -> list of projected child audio entities created for it.</summary>
    private readonly Dictionary<EntityUid, List<EntityUid>> _projectionsBySource = new();
    private EntityQuery<CEZLevelMapComponent> _zMapQuery;
    private bool _crossZAudioEnabled = true;
    private bool _creatingProjection;
    private bool _debug;

    public override void Initialize()
    {
        base.Initialize();

        _zMapQuery = GetEntityQuery<CEZLevelMapComponent>();

        Subs.CVar(_config, CCVars.CEZLevelsCrossZAudio, OnCrossZAudioChanged, true);
        Subs.CVar(_config, CCVars.CEZLevelsCrossZAudioDebug, v => _debug = v, true);

        // Parent changes and map init discover new sources. Only sources on a Z map receive the
        // movement marker, keeping ordinary-map MoveEvents out of this system entirely.
        SubscribeLocalEvent<AudioComponent, MapInitEvent>(OnAudioMapInit);
        SubscribeLocalEvent<AudioComponent, EntParentChangedMessage>(OnAudioParentChanged);
        SubscribeLocalEvent<AudioComponent, ComponentShutdown>(OnAudioShutdown);
        SubscribeLocalEvent<CMUZLevelAudioActiveComponent, MoveEvent>(OnAudioMove);
    }

    private void OnCrossZAudioChanged(bool enabled)
    {
        var wasEnabled = _crossZAudioEnabled;
        _crossZAudioEnabled = enabled;
        if (enabled)
        {
            if (!wasEnabled)
                RefreshActiveSources();

            return;
        }

        // Disabling mid-round: tear down live projections so looped audio stops on adjacent decks
        // immediately. QueueDel is deferred, so iterating the dictionary here is safe.
        foreach (var projections in _projectionsBySource.Values)
        {
            foreach (var projection in projections)
                RemoveProjection(projection);
        }

        _projectionsBySource.Clear();
        _processed.Clear();
    }

    private void RefreshActiveSources()
    {
        // Collect UIDs first because projecting a source creates AudioComponents and may change
        // the storage being enumerated.
        var sources = new List<EntityUid>();
        var query = EntityQueryEnumerator<CMUZLevelAudioActiveComponent>();
        while (query.MoveNext(out var uid, out _))
            sources.Add(uid);

        foreach (var source in sources)
        {
            if (TerminatingOrDeleted(source))
                continue;

            if (!TryComp<AudioComponent>(source, out var audio) ||
                !TryComp<TransformComponent>(source, out var xform))
            {
                RemComp<CMUZLevelAudioActiveComponent>(source);
                ClearSourceProjections(source);
                continue;
            }

            RefreshAudioSource((source, audio), xform);
        }
    }

    private void OnAudioMove(Entity<CMUZLevelAudioActiveComponent> active, ref MoveEvent args)
    {
        if (!TryComp<AudioComponent>(active, out var audio))
        {
            RemComp<CMUZLevelAudioActiveComponent>(active);
            return;
        }

        Entity<AudioComponent> ent = (active, audio);
        RefreshAudioSource(ent, args.Component, reconcileExisting: true);
    }

    private void OnAudioMapInit(Entity<AudioComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<TransformComponent>(ent, out var xform))
            return;

        RefreshAudioSource(ent, xform);
    }

    private void OnAudioParentChanged(Entity<AudioComponent> ent, ref EntParentChangedMessage args)
    {
        RefreshAudioSource(ent, args.Transform, reconcileExisting: true);
    }

    private void OnAudioShutdown(Entity<AudioComponent> ent, ref ComponentShutdown args)
    {
        RemComp<CMUZLevelAudioActiveComponent>(ent);
        _projections.Remove(ent);
        ClearSourceProjections(ent);
    }

    private void RefreshAudioSource(
        Entity<AudioComponent> ent,
        TransformComponent xform,
        bool reconcileExisting = false)
    {
        if (_creatingProjection || _projections.Contains(ent))
            return;

        if (xform.MapUid is not { } mapUid || !_zMapQuery.HasComp(mapUid))
        {
            RemComp<CMUZLevelAudioActiveComponent>(ent);
            ClearSourceProjections(ent);
            return;
        }

        EnsureComp<CMUZLevelAudioActiveComponent>(ent);
        if (reconcileExisting && _processed.Contains(ent))
            RefreshSourceProjections(ent, xform);
        else
            TryProject(ent, xform);
    }

    private void ClearSourceProjections(EntityUid source)
    {
        _processed.Remove(source);
        RemoveSourceProjections(source);
    }

    private void RemoveSourceProjections(EntityUid source)
    {
        if (_projectionsBySource.Remove(source, out var projections))
        {
            foreach (var projection in projections)
                RemoveProjection(projection);
        }
    }

    private void RemoveProjection(EntityUid projection)
    {
        _projections.Remove(projection);
        if (!TerminatingOrDeleted(projection))
            QueueDel(projection);
    }

    private void TryProject(Entity<AudioComponent> ent, TransformComponent xform)
    {
        if (_creatingProjection || _projections.Contains(ent))
            return;

        if (!_crossZAudioEnabled)
            return;

        if (ent.Comp.Global ||
            ent.Comp.IncludedEntities != null ||
            string.IsNullOrEmpty(ent.Comp.FileName))
        {
            return;
        }

        if (xform.MapUid is not { } sourceMap)
        {
            if (_debug) Log.Info($"[crossz-audio] {ToPrettyString(ent)} skipped: no MapUid (file={ent.Comp.FileName})");
            return;
        }

        if (!_zMapQuery.HasComp(sourceMap))
            return;

        // First fire creates the projections. Movement reconciles this same source separately.
        if (!_processed.Add(ent))
            return;

        RefreshSourceProjections(ent, xform);
    }

    private void RefreshSourceProjections(Entity<AudioComponent> ent, TransformComponent xform)
    {
        if (!_crossZAudioEnabled ||
            ent.Comp.Global ||
            ent.Comp.IncludedEntities != null ||
            string.IsNullOrEmpty(ent.Comp.FileName))
        {
            RemoveSourceProjections(ent);
            return;
        }

        if (xform.MapUid is not { } sourceMap ||
            !_zMapQuery.TryComp(sourceMap, out var sourceZMap))
        {
            RemoveSourceProjections(ent);
            return;
        }

        var sourcePosition = _transform.GetWorldPosition(xform);
        if (_debug) Log.Info($"[crossz-audio] {ToPrettyString(ent)} ENTER: file={ent.Comp.FileName} map={ToPrettyString(sourceMap)} grid={(xform.GridUid is { } g ? ToPrettyString(g) : "null")} pos={sourcePosition} MaxDistance={ent.Comp.Params.MaxDistance}");
        var targets = CollectProjectionTargets((ent.Owner, ent.Comp), (sourceMap, sourceZMap), sourcePosition, xform.GridUid);
        ReconcileProjections(ent, targets);
    }

    private List<ProjectionTarget> CollectProjectionTargets(
        Entity<AudioComponent> source,
        Entity<CEZLevelMapComponent> sourceMap,
        Vector2 sourcePosition,
        EntityUid? sourceGridUid)
    {
        var targets = new List<ProjectionTarget>();
        if (source.Comp.Params.MaxDistance <= 0f)
        {
            if (_debug) Log.Info($"[crossz-audio] {ToPrettyString(source)} bail: MaxDistance<=0");
            return targets;
        }

        CollectProjectionTargetsInDirection(source, sourceMap, sourcePosition, sourceGridUid, targets, -1);
        CollectProjectionTargetsInDirection(source, sourceMap, sourcePosition, sourceGridUid, targets, +1);
        return targets;
    }

    private void CollectProjectionTargetsInDirection(
        Entity<AudioComponent> source,
        Entity<CEZLevelMapComponent> sourceMap,
        Vector2 sourcePosition,
        EntityUid? sourceGridUid,
        List<ProjectionTarget> targets,
        int step)
    {
        // Each step crosses one barrier (the upper deck's floor = the lower deck's ceiling). For
        // DOWN that floor belongs to the current source; for UP it belongs to the next target. The
        // cascade advances "current" each step so a multi-deck drop stops at the first solid floor.

        var currentMap = sourceMap.Owner;
        var currentPos = sourcePosition;

        for (var depth = step; Math.Abs(depth) <= CESharedZLevelsSystem.MaxZLevelsBelowRendering; depth += step)
        {
            if (step < 0)
            {
                if (!_zLevels.TryFindRealOpeningNear(currentMap, currentPos, CrossZAudioOpeningRadius, out _))
                {
                    if (_debug) Log.Info($"[crossz-audio]   depth={depth}: no real hole in floor of {ToPrettyString(currentMap)} near {currentPos}");
                    return;
                }
            }

            // Resolve target via linked-grid peer (always from the original source so the PeerGrids
            // lookup uses the correct base depth) or plain map-step.
            if (!_zLevels.TryResolveLinkedTarget(sourceGridUid, sourceMap.Owner, depth, sourcePosition,
                    out var nextMap, out var nextPos))
            {
                if (_debug) Log.Info($"[crossz-audio]   depth={depth}: no map at that offset");
                return;
            }

            if (step > 0)
            {
                if (!_zLevels.TryFindRealOpeningNear(nextMap, nextPos, CrossZAudioOpeningRadius, out _))
                {
                    if (_debug) Log.Info($"[crossz-audio]   depth={depth}: no real hole in floor of {ToPrettyString(nextMap)} near {nextPos}");
                    return;
                }
            }

            targets.Add(new ProjectionTarget(nextMap, nextPos));

            currentMap = nextMap;
            currentPos = nextPos;
        }
    }

    private void ReconcileProjections(Entity<AudioComponent> source, List<ProjectionTarget> targets)
    {
        var remaining = _projectionsBySource.Remove(source, out var existing)
            ? existing
            : new List<EntityUid>();
        var retained = new List<EntityUid>(targets.Count);
        ResolvedSoundSpecifier? specifier = null;

        foreach (var target in targets)
        {
            EntityUid? matching = null;
            for (var i = remaining.Count - 1; i >= 0; i--)
            {
                var projection = remaining[i];
                if (TerminatingOrDeleted(projection) ||
                    !_projections.Contains(projection) ||
                    !TryComp<TransformComponent>(projection, out var projectionXform))
                {
                    RemoveProjection(projection);
                    remaining.RemoveAt(i);
                    continue;
                }

                if (projectionXform.MapUid != target.MapUid)
                    continue;

                matching = projection;
                remaining.RemoveAt(i);
                break;
            }

            if (matching is { } projected)
            {
                _transform.SetCoordinates(projected, new EntityCoordinates(target.MapUid, target.Position));
                retained.Add(projected);
                continue;
            }

            specifier ??= new ResolvedPathSpecifier(source.Comp.FileName!);
            if (CreateProjection(source, specifier, target.MapUid, target.Position) is { } created)
                retained.Add(created);
        }

        foreach (var projection in remaining)
            RemoveProjection(projection);

        if (retained.Count != 0)
            _projectionsBySource[source] = retained;
    }

    private EntityUid? CreateProjection(
        Entity<AudioComponent> source,
        ResolvedSoundSpecifier specifier,
        EntityUid targetMap,
        Vector2 sourcePosition)
    {
        _creatingProjection = true;
        try
        {
            // PlayPvs (not PlayStatic) so listeners arriving on the target map after projection
            // creation still hear it — e.g. a jukebox audible to a player descending later.
            var projectedAudio = _audio.PlayPvs(specifier, new EntityCoordinates(targetMap, sourcePosition), source.Comp.Params);

            if (projectedAudio is not { } projected)
            {
                if (_debug) Log.Info($"[crossz-audio]   FAILED to project {source.Comp.FileName} to {ToPrettyString(targetMap)} @ {sourcePosition}");
                return null;
            }

            if (HasComp<StationRadioReceiverAudioComponent>(source.Owner) ||
                HasComp<StationRadioReceiverComponent>(Transform(source.Owner).ParentUid))
            {
                EnsureComp<StationRadioReceiverAudioComponent>(projected.Entity);
            }

            _projections.Add(projected.Entity);
            projected.Component.Flags = source.Comp.Flags;
            Dirty(projected.Entity, projected.Component);
            if (_debug) Log.Info($"[crossz-audio]   PROJECTED {source.Comp.FileName} to {ToPrettyString(targetMap)} @ {sourcePosition}");
            return projected.Entity;
        }
        finally
        {
            _creatingProjection = false;
        }
    }
}
