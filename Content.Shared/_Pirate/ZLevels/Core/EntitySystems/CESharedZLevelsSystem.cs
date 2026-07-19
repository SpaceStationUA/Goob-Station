/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Diagnostics.CodeAnalysis;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Gravity;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using JetBrains.Annotations;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using SharedCCVars = Content.Shared.CCVar.CCVars;

namespace Content.Shared._Pirate.ZLevels.Core.EntitySystems;

public abstract partial class CESharedZLevelsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedGravitySystem _gravity = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    /// <summary>
    /// Per-grid cache of "opening" (empty/transparent) tiles, so cross-Z systems gate projection
    /// without scanning tiles on every event.
    /// </summary>
    private readonly CMUZLevelOpeningCache _openingCache = new();
    private readonly List<Entity<MapGridComponent>> _openingGridScratch = new();

    private EntityQuery<MapComponent> _mapQuery;
    private EntityQuery<CEZLevelMapComponent> _zMapQuery;
    private EntityQuery<CEZLevelsNetworkComponent> _zNetworkQuery;
    private EntityQuery<FTLMapComponent> _ftlMapQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    protected EntityQuery<TransformComponent> TransformQuery;
    protected EntityQuery<PhysicsComponent> PhysicsQuery;
    private bool _sharedInitialized;

    protected EntityQuery<CEZPhysicsComponent> ZPhysQuery;

    /// <summary>Accumulator for fixed-timestep z-physics; see <see cref="Update"/> in Movement.cs.</summary>
    private TimeSpan _accumulatedTime;

    /// <summary>Size of a single z-physics substep. Driven by <see cref="SharedCCVars.CEZPhysicsTickRate"/>.</summary>
    private TimeSpan _fixedTimestep = TimeSpan.FromSeconds(1d / 30d);

    public override void Initialize()
    {
        if (_sharedInitialized)
            return;

        base.Initialize();
        _sharedInitialized = true;

        _mapQuery = GetEntityQuery<MapComponent>();
        _zMapQuery = GetEntityQuery<CEZLevelMapComponent>();
        _zNetworkQuery = GetEntityQuery<CEZLevelsNetworkComponent>();
        _ftlMapQuery = GetEntityQuery<FTLMapComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        TransformQuery = GetEntityQuery<TransformComponent>();
        PhysicsQuery = GetEntityQuery<PhysicsComponent>();
        ZPhysQuery = GetEntityQuery<CEZPhysicsComponent>();

        SubscribeLocalEvent<CEZLevelMapComponent, ComponentShutdown>(OnZLevelMapShutdown);

        _config.OnValueChanged(SharedCCVars.CEZPhysicsTickRate, OnPhysicsTickRateChanged, true);

        InitializeDebug();
        InitOpeningCache();
        InitMovement();
        InitView();
        InitOccluders();
        InitializeActivation();
    }

    public override void Shutdown()
    {
        base.Shutdown();

        if (!_sharedInitialized)
            return;

        _config.UnsubValueChanged(SharedCCVars.CEZPhysicsTickRate, OnPhysicsTickRateChanged);
        ShutdownOpeningCache();
        ShutdownActivation();
        ShutdownDebug();
    }

    private void OnPhysicsTickRateChanged(float hz)
    {
        var clamped = MathF.Max(1f, hz);
        _fixedTimestep = TimeSpan.FromSeconds(1d / clamped);
    }

    private void OnZLevelMapShutdown(Entity<CEZLevelMapComponent> ent, ref ComponentShutdown args)
    {
        if (!_zNetworkQuery.TryComp(ent.Comp.NetworkUid, out var network))
            return;

        DetachMapFromNetwork((ent.Comp.NetworkUid, network), ent);
    }

    /// <summary>
    /// Adds a map at the given depth and updates all derived indexes (ZLevels, ZLevelByEntity,
    /// SortedZLevels, neighbour MapAbove/MapBelow). Caller must have validated the depth is free
    /// and the map isn't already linked elsewhere.
    /// </summary>
    protected void AttachMapToNetwork(Entity<CEZLevelsNetworkComponent> network, Entity<CEZLevelMapComponent> map, int depth)
    {
        network.Comp.ZLevels[depth] = map.Owner;
        network.Comp.ZLevelByEntity[map.Owner] = depth;

        // Dense sorted view with EntityUid.Invalid for gaps. First entry initialises min/max.
        if (network.Comp.SortedZLevels.Count == 0)
        {
            network.Comp.SortedMin = depth;
            network.Comp.SortedMax = depth;
            network.Comp.SortedZLevels.Add(map.Owner);
        }
        else if (depth > network.Comp.SortedMax)
        {
            for (var d = network.Comp.SortedMax + 1; d < depth; d++)
                network.Comp.SortedZLevels.Add(EntityUid.Invalid);
            network.Comp.SortedZLevels.Add(map.Owner);
            network.Comp.SortedMax = depth;
        }
        else if (depth < network.Comp.SortedMin)
        {
            var prefix = new List<EntityUid> { map.Owner };
            for (var d = depth + 1; d < network.Comp.SortedMin; d++)
                prefix.Add(EntityUid.Invalid);
            prefix.AddRange(network.Comp.SortedZLevels);
            network.Comp.SortedZLevels = prefix;
            network.Comp.SortedMin = depth;
        }
        else
        {
            network.Comp.SortedZLevels[depth - network.Comp.SortedMin] = map.Owner;
        }

        map.Comp.Depth = depth;
        map.Comp.NetworkUid = network.Owner;

        // Wire neighbour shortcuts and update their opposite refs.
        if (network.Comp.ZLevels.TryGetValue(depth + 1, out var aboveOpt) && aboveOpt is { } above &&
            _zMapQuery.TryComp(above, out var aboveComp))
        {
            map.Comp.MapAbove = above;
            aboveComp.MapBelow = map.Owner;
            Dirty(above, aboveComp);
        }
        else
        {
            map.Comp.MapAbove = null;
        }

        if (network.Comp.ZLevels.TryGetValue(depth - 1, out var belowOpt) && belowOpt is { } below &&
            _zMapQuery.TryComp(below, out var belowComp))
        {
            map.Comp.MapBelow = below;
            belowComp.MapAbove = map.Owner;
            Dirty(below, belowComp);
        }
        else
        {
            map.Comp.MapBelow = null;
        }

        Dirty(network);
        Dirty(map, map.Comp);
    }

    /// <summary>
    /// Removes the map's entry from every network index and clears its neighbours' opposite refs.
    /// </summary>
    protected void DetachMapFromNetwork(Entity<CEZLevelsNetworkComponent> network, Entity<CEZLevelMapComponent> map)
    {
        var depth = map.Comp.Depth;

        network.Comp.ZLevels.Remove(depth);
        network.Comp.ZLevelByEntity.Remove(map.Owner);

        if (network.Comp.SortedZLevels.Count > 0 &&
            depth >= network.Comp.SortedMin && depth <= network.Comp.SortedMax)
        {
            network.Comp.SortedZLevels[depth - network.Comp.SortedMin] = EntityUid.Invalid;
            CompactSortedZLevels(network.Comp);
        }

        if (map.Comp.MapAbove is { } above && _zMapQuery.TryComp(above, out var aboveComp))
        {
            aboveComp.MapBelow = null;
            Dirty(above, aboveComp);
        }

        if (map.Comp.MapBelow is { } below && _zMapQuery.TryComp(below, out var belowComp))
        {
            belowComp.MapAbove = null;
            Dirty(below, belowComp);
        }

        map.Comp.NetworkUid = EntityUid.Invalid;
        map.Comp.MapAbove = null;
        map.Comp.MapBelow = null;

        Dirty(network);
        Dirty(map, map.Comp);
    }

    /// <summary>
    /// Trims leading/trailing <see cref="EntityUid.Invalid"/> slots so SortedMin/SortedMax
    /// stay tight around the actual extent.
    /// </summary>
    private static void CompactSortedZLevels(CEZLevelsNetworkComponent network)
    {
        var list = network.SortedZLevels;
        var start = 0;
        while (start < list.Count && list[start] == EntityUid.Invalid)
            start++;

        if (start >= list.Count)
        {
            list.Clear();
            network.SortedMin = 0;
            network.SortedMax = 0;
            return;
        }

        var end = list.Count - 1;
        while (end > start && list[end] == EntityUid.Invalid)
            end--;

        if (start == 0 && end == list.Count - 1)
            return;

        network.SortedZLevels = list.GetRange(start, end - start + 1);
        network.SortedMin += start;
        network.SortedMax = network.SortedMin + (end - start);
    }

    /// <summary>
    /// Checks whether the map is in the zLevels network. If so, returns true and the current depth + Entity of the current zLevels network.
    /// </summary>
    [PublicAPI]
    public bool TryGetZNetwork(EntityUid mapUid, [NotNullWhen(true)] out Entity<CEZLevelsNetworkComponent>? zLevel)
    {
        zLevel = null;

        if (!_zMapQuery.TryComp(mapUid, out var zMap))
            return false;

        if (!_zNetworkQuery.TryComp(zMap.NetworkUid, out var network))
            return false;

        zLevel = (zMap.NetworkUid, network);
        return true;
    }

    [PublicAPI]
    public bool TryMapOffset(Entity<CEZLevelMapComponent?> inputMapUid,
        int offset,
        [NotNullWhen(true)] out Entity<CEZLevelMapComponent>? outputMapUid)
    {
        outputMapUid = null;
        if (!Resolve(inputMapUid, ref inputMapUid.Comp, false))
            return false;

        EntityUid? target = offset switch
        {
            1 => inputMapUid.Comp.MapAbove,
            -1 => inputMapUid.Comp.MapBelow,
            _ => null,
        };

        if (target is null)
        {
            if (!_zNetworkQuery.TryComp(inputMapUid.Comp.NetworkUid, out var network))
                return false;

            if (!network.ZLevels.TryGetValue(inputMapUid.Comp.Depth + offset, out var fallback) || fallback is null)
                return false;

            target = fallback.Value;
        }

        if (!_zMapQuery.TryComp(target, out var targetZLevelComp))
            return false;

        outputMapUid = (target.Value, targetZLevelComp);
        return true;
    }

    /// <summary>
    /// Resolves an adjacent-Z target, accounting for linked-grid peer shuttles (multi-deck shuttle
    /// whose decks are separate grids on different network maps). For a <see cref="CEZLinkedGridComponent"/>
    /// grid, takes the peer at the depth offset and reprojects <paramref name="sourceWorld"/> through
    /// the two grids' matrices so the XY lines up with the same tile on the peer deck. Otherwise
    /// falls back to <see cref="TryMapOffset"/> + the source XY.
    /// </summary>
    [PublicAPI]
    public bool TryResolveLinkedTarget(
        EntityUid? sourceGridUid,
        EntityUid sourceMap,
        int depthOffset,
        System.Numerics.Vector2 sourceWorld,
        out EntityUid targetMap,
        out System.Numerics.Vector2 targetWorld)
    {
        targetMap = default;
        targetWorld = sourceWorld;

        if (sourceGridUid is { } gridUid &&
            TryComp<CEZLinkedGridComponent>(gridUid, out var linked) &&
            linked.PeerGrids.TryGetValue(linked.Depth + depthOffset, out var peerGridUid) &&
            TryComp<TransformComponent>(peerGridUid, out var peerXform) &&
            peerXform.MapUid is { } peerMapUid)
        {
            var srcInv = _transform.GetInvWorldMatrix(gridUid);
            var local = System.Numerics.Vector2.Transform(sourceWorld, srcInv);
            targetWorld = System.Numerics.Vector2.Transform(local, _transform.GetWorldMatrix(peerGridUid));
            targetMap = peerMapUid;
            return true;
        }

        if (!TryMapOffset(sourceMap, depthOffset, out var offsetMap))
            return false;

        targetMap = offsetMap.Value.Owner;
        return true;
    }

    [PublicAPI]
    public bool TryZNetwork(Entity<CEZLevelMapComponent?> inputMapUid,
        [NotNullWhen(true)] out Entity<CEZLevelsNetworkComponent>? zNetwork)
    {
        zNetwork = null;
        if (!Resolve(inputMapUid, ref inputMapUid.Comp, false))
            return false;

        if (!_zNetworkQuery.TryComp(inputMapUid.Comp.NetworkUid, out var network))
            return false;

        zNetwork = (inputMapUid.Comp.NetworkUid, network);
        return true;
    }

    [PublicAPI]
    public bool TryMapUp(Entity<CEZLevelMapComponent?> inputMapUid,
        [NotNullWhen(true)] out Entity<CEZLevelMapComponent>? aboveMapUid)
    {
        return TryMapOffset(inputMapUid, 1, out aboveMapUid);
    }

    [PublicAPI]
    public bool TryMapDown(Entity<CEZLevelMapComponent?> inputMapUid,
        [NotNullWhen(true)] out Entity<CEZLevelMapComponent>? belowMapUid)
    {
        return TryMapOffset(inputMapUid, -1, out belowMapUid);
    }

    private bool TryResolveTraversalMapOffset(EntityUid currentMapUid, int offset, out EntityUid targetMapUid, out int targetDepth)
    {
        targetMapUid = EntityUid.Invalid;
        targetDepth = default;

        if (_zMapQuery.TryComp(currentMapUid, out var zMapComp) &&
            TryMapOffset((currentMapUid, zMapComp), offset, out var targetMap))
        {
            targetMapUid = targetMap.Value.Owner;
            targetDepth = targetMap.Value.Comp.Depth;
            return true;
        }

        if (!_ftlMapQuery.TryComp(currentMapUid, out var ftlMapComp))
            return false;

        targetDepth = ftlMapComp.Depth + offset;
        var query = EntityQueryEnumerator<FTLMapComponent>();
        while (query.MoveNext(out var uid, out var candidate))
        {
            if (candidate.Depth != targetDepth)
                continue;

            targetMapUid = uid;
            return true;
        }

        return false;
    }

    private bool TryGetTraversalDepth(TransformComponent xform, out int depth)
    {
        if (xform.MapUid is { } mapUid)
        {
            if (_zMapQuery.TryComp(mapUid, out var zMapComp))
            {
                depth = zMapComp.Depth;
                return true;
            }

            if (_ftlMapQuery.TryComp(mapUid, out var ftlMapComp))
            {
                depth = ftlMapComp.Depth;
                return true;
            }
        }

        if (xform.GridUid is { } gridUid &&
            TryComp<CEZLinkedGridComponent>(gridUid, out var linked))
        {
            depth = linked.Depth;
            return true;
        }

        depth = default;
        return false;
    }

    private bool HasTraversalContext(TransformComponent xform)
    {
        if (xform.MapUid is { } mapUid &&
            (_zMapQuery.HasComp(mapUid) || _ftlMapQuery.HasComp(mapUid)))
        {
            return true;
        }

        return xform.GridUid is { } gridUid && HasComp<CEZLinkedGridComponent>(gridUid);
    }

    /// <summary>
    /// Returns a list of all maps above the specified map. The closest map at the top is returned first.
    /// </summary>
    [PublicAPI]
    public List<EntityUid> GetAllMapsAbove(Entity<CEZLevelMapComponent> inputMapUid)
    {
        if (!_zNetworkQuery.TryComp(inputMapUid.Comp.NetworkUid, out var network) ||
            inputMapUid.Comp.Depth >= network.SortedMax)
            return new List<EntityUid>();

        var sorted = network.SortedZLevels;
        var startIndex = inputMapUid.Comp.Depth < network.SortedMin
            ? 0
            : inputMapUid.Comp.Depth - network.SortedMin + 1;

        var result = new List<EntityUid>(sorted.Count - startIndex);
        for (var i = startIndex; i < sorted.Count; i++)
        {
            var uid = sorted[i];
            if (uid != EntityUid.Invalid && _zMapQuery.HasComp(uid))
                result.Add(uid);
        }
        return result;
    }

    /// <summary>
    /// Returns a list of all maps below the specified map. The closest map at the bottom is returned first.
    /// </summary>
    [PublicAPI]
    public List<EntityUid> GetAllMapsBelow(Entity<CEZLevelMapComponent> inputMapUid)
    {
        if (!_zNetworkQuery.TryComp(inputMapUid.Comp.NetworkUid, out var network) ||
            inputMapUid.Comp.Depth <= network.SortedMin)
            return new List<EntityUid>();

        var sorted = network.SortedZLevels;
        var endIndex = inputMapUid.Comp.Depth > network.SortedMax
            ? sorted.Count
            : inputMapUid.Comp.Depth - network.SortedMin;

        var result = new List<EntityUid>(endIndex);
        for (var i = endIndex - 1; i >= 0; i--)
        {
            var uid = sorted[i];
            if (uid != EntityUid.Invalid && _zMapQuery.HasComp(uid))
                result.Add(uid);
        }
        return result;
    }
}
