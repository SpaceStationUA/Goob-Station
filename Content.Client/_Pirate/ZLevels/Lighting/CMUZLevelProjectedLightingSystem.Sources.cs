// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared.CCVar;
using Robust.Client.GameObjects;
using Robust.Shared.Map;

namespace Content.Client._Pirate.ZLevels.Lighting;

public sealed partial class CMUZLevelProjectedLightingSystem
{
    [Dependency] private readonly MetaDataSystem _sourceMeta = default!;
    private readonly Dictionary<MapId, List<Entity<PointLightComponent, TransformComponent>>> _sourceIndex = new();
    private readonly HashSet<MapId> _sourceMaps = new();
    private readonly List<MapId> _emptySourceMaps = new();
    private readonly Dictionary<MapId, SourceLight[]> _sourceComparison = new();
    private bool _sourceIndexDirty = true;
    private bool _sourceIndexTracking = true;

    internal SourceDiscoveryStats SourceStats { get; private set; }
    internal readonly record struct SourceDiscoveryStats(int Considered, int Positions, int Accepted,
        int IndexRebuilds, int IndexEntries, bool Compared, bool Mismatch);

    private void InitializeSourceIndex()
    {
        // The stock light-tree system owns PointLight's exclusive lifecycle subscriptions.
        EntityManager.ComponentAdded += OnSourceAdded;
        EntityManager.ComponentRemoved += OnSourceRemoved;
        SubscribeLocalEvent<PointLightComponent, MapUidChangedEvent>(OnSourceMapChanged);
        SubscribeLocalEvent<PointLightComponent, EntityPausedEvent>(OnSourcePaused);
        SubscribeLocalEvent<PointLightComponent, EntityUnpausedEvent>(OnSourceUnpaused);
        SubscribeLocalEvent<PointLightComponent, MetaFlagRemoveAttemptEvent>(OnSourceFlagRemoval);
    }

    private void OnSourceAdded(AddedComponentEventArgs args)
    {
        if (args.BaseArgs.Component is not (PointLightComponent or CMUZProjectedLightComponent))
            return;
        _sourceIndexDirty = true;
        if (args.BaseArgs.Component is PointLightComponent)
            _sourceMeta.AddFlag(args.BaseArgs.Owner, MetaDataFlags.ExtraTransformEvents);
    }

    private void OnSourceRemoved(RemovedComponentEventArgs args)
    {
        if (args.BaseArgs.Component is not (PointLightComponent or CMUZProjectedLightComponent))
            return;
        _sourceIndexDirty = true;
        if (args.BaseArgs.Component is PointLightComponent && !args.Terminating)
            _sourceMeta.RemoveFlag(args.BaseArgs.Owner, MetaDataFlags.ExtraTransformEvents);
    }

    private void OnSourceMapChanged(Entity<PointLightComponent> ent, ref MapUidChangedEvent args) => _sourceIndexDirty = true;
    private void OnSourcePaused(Entity<PointLightComponent> ent, ref EntityPausedEvent args) => _sourceIndexDirty = true;
    private void OnSourceUnpaused(Entity<PointLightComponent> ent, ref EntityUnpausedEvent args) => _sourceIndexDirty = true;

    private void OnSourceFlagRemoval(Entity<PointLightComponent> ent, ref MetaFlagRemoveAttemptEvent args)
    {
        // Other systems may release their own need for this shared flag while this light still needs it.
        if (_sourceIndexTracking && ent.Comp.LifeStage <= ComponentLifeStage.Running)
            args.ToRemove &= ~MetaDataFlags.ExtraTransformEvents;
    }

    private void CollectSourceMaps(Entity<CEZLevelMapComponent?> viewerMap, MapId viewerMapId, int maxDepth)
    {
        _sourceMaps.Clear();
        _sourceMaps.Add(viewerMapId); // Supplies the cascade onto the deck below.
        for (var depth = -maxDepth; depth <= 1; depth++)
        {
            if (depth != 0 && _zLevels.TryMapOffset(viewerMap, depth, out var adjacent) &&
                adjacent is { } map && _mapQuery.TryComp(map.Owner, out var comp) && comp.MapId != MapId.Nullspace)
                _sourceMaps.Add(comp.MapId);
        }
    }

    private void RebuildSourceIndex()
    {
        foreach (var entries in _sourceIndex.Values)
            entries.Clear();
        var query = EntityQueryEnumerator<PointLightComponent, TransformComponent>();
        var count = 0;
        while (query.MoveNext(out var uid, out var light, out var xform))
        {
            // Also covers a system initialized after existing entities. Do not cache light properties.
            _sourceMeta.AddFlag(uid, MetaDataFlags.ExtraTransformEvents);
            if (xform.MapID == MapId.Nullspace || _projectedQuery.HasComp(uid))
                continue;
            if (!_sourceIndex.TryGetValue(xform.MapID, out var entries))
                _sourceIndex[xform.MapID] = entries = new();
            entries.Add((uid, light, xform));
            count++;
        }
        _emptySourceMaps.Clear();
        foreach (var (map, entries) in _sourceIndex)
        {
            if (entries.Count == 0)
                _emptySourceMaps.Add(map);
        }
        foreach (var map in _emptySourceMaps)
            _sourceIndex.Remove(map);
        _sourceIndexDirty = false;
        SourceStats = SourceStats with { IndexRebuilds = 1, IndexEntries = count };
    }

    private void BuildIndexedSourceLightBuckets(Box2Rotated bounds, float minEnergy)
    {
        if (_sourceIndexDirty)
            RebuildSourceIndex();
        ClearSourceLightBuckets();
        foreach (var map in _sourceMaps)
        {
            if (!_sourceIndex.TryGetValue(map, out var entries))
                continue;
            // Preserve the original entity query order within each map, including equal-energy ties.
            foreach (var (uid, light, xform) in entries)
                AddSourceLight(uid, light, xform, bounds, minEnergy);
        }
    }

    private void DiscoverSourceLights(Box2Rotated bounds, float minEnergy)
    {
        SourceStats = default;
        var mode = Math.Clamp(_config.GetCVar(CCVars.ZProjectedLightSourceIndex), 0, 2);
        if (mode == 0)
        {
            BuildSourceLightBuckets(bounds, minEnergy);
            return;
        }
        BuildIndexedSourceLightBuckets(bounds, minEnergy);
        if (mode != 2)
            return;

        // Expensive opt-in oracle: compare full source records AND order on relevant maps.
        // Rendering uses the original scan in validation mode, even if the index differs.
        _sourceComparison.Clear();
        foreach (var map in _sourceMaps)
            _sourceComparison[map] = _sourceLightBuckets.TryGetValue(map, out var sources) ? sources.ToArray() : [];
        var indexedStats = SourceStats;
        BuildSourceLightBuckets(bounds, minEnergy);
        var mismatch = false;
        foreach (var (map, expected) in _sourceComparison)
        {
            if (_sourceLightBuckets.TryGetValue(map, out var actual))
                mismatch |= !expected.SequenceEqual(actual);
            else
                mismatch |= expected.Length != 0;
        }
        SourceStats = indexedStats with { Compared = true, Mismatch = mismatch };
        _sourceComparison.Clear();
        if (mismatch)
            _sourceIndexDirty = true; // Recover at the next frame; SourceStats still reports this frame's mismatch.
    }

    private void ShutdownSourceIndex()
    {
        EntityManager.ComponentAdded -= OnSourceAdded;
        EntityManager.ComponentRemoved -= OnSourceRemoved;
        _sourceIndexTracking = false;
        var query = EntityManager.AllEntityQueryEnumerator<PointLightComponent>();
        while (query.MoveNext(out var uid, out _))
            _sourceMeta.RemoveFlag(uid, MetaDataFlags.ExtraTransformEvents);
        _sourceIndex.Clear();
        _sourceComparison.Clear();
    }
}
