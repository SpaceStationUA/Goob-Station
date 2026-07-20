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

        // MoveEvent catches PlayPvs(coords) audio (footsteps, gunshots); MapInitEvent catches
        // PlayPvs(uid) audio (jukeboxes) parented to an entity without a world-position transition.
        SubscribeLocalEvent<AudioComponent, MoveEvent>(OnAudioMove);
        SubscribeLocalEvent<AudioComponent, MapInitEvent>(OnAudioMapInit);
        SubscribeLocalEvent<AudioComponent, ComponentShutdown>(OnAudioShutdown);
    }

    private void OnCrossZAudioChanged(bool enabled)
    {
        _crossZAudioEnabled = enabled;
        if (enabled)
            return;

        // Disabling mid-round: tear down live projections so looped audio stops on adjacent decks
        // immediately. QueueDel is deferred, so iterating the dictionary here is safe.
        foreach (var projections in _projectionsBySource.Values)
        {
            foreach (var projection in projections)
            {
                _projections.Remove(projection);
                if (!TerminatingOrDeleted(projection))
                    QueueDel(projection);
            }
        }

        _projectionsBySource.Clear();
        _processed.Clear();
    }

    private void OnAudioMove(Entity<AudioComponent> ent, ref MoveEvent args)
    {
        if (_processed.Remove(ent) && _projectionsBySource.Remove(ent, out var stale))
        {
            foreach (var p in stale)
            {
                _projections.Remove(p);
                if (!TerminatingOrDeleted(p))
                    QueueDel(p);
            }
        }
        TryProject(ent, args.Component);
    }

    private void OnAudioMapInit(Entity<AudioComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<TransformComponent>(ent, out var xform))
            return;
        TryProject(ent, xform);
    }

    private void OnAudioShutdown(Entity<AudioComponent> ent, ref ComponentShutdown args)
    {
        _processed.Remove(ent);
        _projections.Remove(ent);

        // Kill projected copies when the source dies so looped audio doesn't outlive it.
        if (_projectionsBySource.Remove(ent, out var projections))
        {
            foreach (var projection in projections)
            {
                _projections.Remove(projection);
                if (!TerminatingOrDeleted(projection))
                    QueueDel(projection);
            }
        }
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

        if (!_zMapQuery.TryComp(sourceMap, out var sourceZMap))
            return;

        // First fire wins; later MoveEvent/MapInitEvent on the same audio are no-ops.
        if (!_processed.Add(ent))
            return;

        var sourcePosition = _transform.GetWorldPosition(xform);
        if (_debug) Log.Info($"[crossz-audio] {ToPrettyString(ent)} ENTER: file={ent.Comp.FileName} map={ToPrettyString(sourceMap)} grid={(xform.GridUid is { } g ? ToPrettyString(g) : "null")} pos={sourcePosition} MaxDistance={ent.Comp.Params.MaxDistance}");
        ProjectCrossZAudio((ent.Owner, ent.Comp), (sourceMap, sourceZMap), sourcePosition, xform.GridUid);
    }

    private void ProjectCrossZAudio(
        Entity<AudioComponent> source,
        Entity<CEZLevelMapComponent> sourceMap,
        Vector2 sourcePosition,
        EntityUid? sourceGridUid)
    {
        if (source.Comp.Params.MaxDistance <= 0f)
        {
            if (_debug) Log.Info($"[crossz-audio] {ToPrettyString(source)} bail: MaxDistance<=0");
            return;
        }

        ResolvedSoundSpecifier? specifier = null;
        ProjectDirection(source, sourceMap, sourcePosition, sourceGridUid, ref specifier, -1);
        ProjectDirection(source, sourceMap, sourcePosition, sourceGridUid, ref specifier, +1);
    }

    private void ProjectDirection(
        Entity<AudioComponent> source,
        Entity<CEZLevelMapComponent> sourceMap,
        Vector2 sourcePosition,
        EntityUid? sourceGridUid,
        ref ResolvedSoundSpecifier? specifier,
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

            specifier ??= new ResolvedPathSpecifier(source.Comp.FileName!);
            CreateProjection(source, specifier, nextMap, nextPos);
            if (_debug) Log.Info($"[crossz-audio]   depth={depth}: PROJECTED {source.Comp.FileName} to {ToPrettyString(nextMap)} @ {nextPos}");

            currentMap = nextMap;
            currentPos = nextPos;
        }
    }

    private void CreateProjection(
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
                return;

            _projections.Add(projected.Entity);
            projected.Component.Flags = source.Comp.Flags;
            Dirty(projected.Entity, projected.Component);

            if (!_projectionsBySource.TryGetValue(source.Owner, out var list))
            {
                list = new List<EntityUid>();
                _projectionsBySource[source.Owner] = list;
            }
            list.Add(projected.Entity);
        }
        finally
        {
            _creatingProjection = false;
        }
    }
}
