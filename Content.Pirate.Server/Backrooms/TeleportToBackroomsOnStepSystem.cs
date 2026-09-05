// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Pirate.Shared.Backrooms;
using Content.Server.NPC.HTN;
using Content.Server.Parallax;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Physics;
using Content.Shared.StepTrigger.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Pirate.Server.Backrooms;

/// <summary>
/// Creates / caches a BackroomsLevel0 planet and teleports steppers there,
/// at least <see cref="MinMobDistance"/> tiles from living NPCs.
/// </summary>
public sealed class TeleportToBackroomsOnStepSystem : EntitySystem
{
    private static readonly ProtoId<BiomeTemplatePrototype> BackroomsBiome = "BackroomsLevel0";

    /// <summary>
    /// Minimum Euclidean tile distance from living HTN mobs.
    /// </summary>
    private const float MinMobDistance = 30f;

    /// <summary>
    /// Marker-layer chunk size; spawn in the inner region so adjacent chunks stay far.
    /// </summary>
    private const int MarkerChunkSize = 128;
    private const float EdgeMargin = 32f;
    private const int ChunkTries = 6;
    private const int TileSamples = 200;

    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    private EntityUid? _backroomsMap;
    private readonly List<Vector2> _npcPositions = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TeleportToBackroomsOnStepComponent, StepTriggeredOffEvent>(OnStepTriggered);
        SubscribeLocalEvent<TeleportToBackroomsOnStepComponent, StepTriggerAttemptEvent>(OnStepAttempt);
        SubscribeLocalEvent<TeleportToBackroomsOnStepComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _backroomsMap = null;
    }

    private void OnStepAttempt(Entity<TeleportToBackroomsOnStepComponent> ent, ref StepTriggerAttemptEvent args)
    {
        args.Continue = true;
    }

    private void OnMapInit(Entity<TeleportToBackroomsOnStepComponent> ent, ref MapInitEvent args)
    {
        var color = new Color(
            (byte) _random.Next(40, 256),
            (byte) _random.Next(40, 256),
            (byte) _random.Next(40, 256));

        if (_pointLight.TryGetLight(ent, out var light))
            _pointLight.SetColor(ent, color, light);
    }

    private void OnStepTriggered(Entity<TeleportToBackroomsOnStepComponent> ent, ref StepTriggeredOffEvent args)
    {
        var tripper = args.Tripper;
        if (!HasComp<MobStateComponent>(tripper) || TerminatingOrDeleted(tripper))
            return;

        if (!TryGetBackroomsDestination(out var dest))
            return;

        _transform.SetCoordinates(tripper, dest);

        if (!TryComp<StaminaComponent>(tripper, out var stam))
            return;

        var drain = MathF.Max(0f, stam.CritThreshold - _stamina.GetStaminaDamage(tripper, stam));
        if (drain > 0f)
            _stamina.TakeStaminaDamage(tripper, drain, stam, source: ent, visual: true, ignoreResist: true);
    }

    /// <summary>
    /// Ensures a BackroomsLevel0 map exists and returns a corridor tile at least
    /// <see cref="MinMobDistance"/> tiles from living NPCs.
    /// </summary>
    public bool TryGetBackroomsDestination(out EntityCoordinates coords)
    {
        coords = default;

        if (!TryEnsureBackroomsMap(out var mapUid) ||
            !TryComp<MapGridComponent>(mapUid, out var grid) ||
            !TryComp<BiomeComponent>(mapUid, out var biome))
            return false;

        EntityCoordinates? fallback = null;
        var fallbackDistSq = -1f;

        for (var chunkTry = 0; chunkTry < ChunkTries; chunkTry++)
        {
            var origin = PickChunkOrigin();
            var area = Box2.CenteredAround(origin, new Vector2(MarkerChunkSize, MarkerChunkSize));
            _biome.LoadArea(mapUid, biome, grid, area);

            CollectNpcPositions(mapUid);

            if (TryPickTile(mapUid, grid, origin, requireMinDistance: true, out coords, out _))
                return true;

            if (TryPickTile(mapUid, grid, origin, requireMinDistance: false, out var candidate, out var distSq) &&
                distSq > fallbackDistSq)
            {
                fallbackDistSq = distSq;
                fallback = candidate;
            }
        }

        if (fallback != null)
        {
            coords = fallback.Value;
            return true;
        }

        _biome.LoadArea(mapUid, biome, grid, Box2.CenteredAround(Vector2.Zero, new Vector2(32f, 32f)));
        coords = new EntityCoordinates(mapUid, Vector2.Zero);
        return true;
    }

    private Vector2 PickChunkOrigin()
    {
        var chunk = new Vector2i(_random.Next(-3, 4), _random.Next(-3, 4)) * MarkerChunkSize;
        return chunk + new Vector2(MarkerChunkSize / 2f, MarkerChunkSize / 2f);
    }

    private bool TryPickTile(
        EntityUid mapUid,
        MapGridComponent grid,
        Vector2 origin,
        bool requireMinDistance,
        out EntityCoordinates coords,
        out float distSq)
    {
        coords = default;
        distSq = -1f;

        var search = MarkerChunkSize / 2f - EdgeMargin;
        var minSq = MinMobDistance * MinMobDistance;
        EntityCoordinates? best = null;

        for (var i = 0; i < TileSamples; i++)
        {
            var tile = new Vector2i(
                (int) MathF.Floor(origin.X + _random.NextFloat(-search, search)),
                (int) MathF.Floor(origin.Y + _random.NextFloat(-search, search)));

            if (!_map.TryGetTileRef(mapUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
                continue;

            if (_turf.IsTileBlocked(tileRef, CollisionGroup.MobMask))
                continue;

            var local = _map.GridTileToLocal(mapUid, grid, tile);
            var world = _transform.ToMapCoordinates(local).Position;
            var npcDistSq = MinDistanceSquaredToNpcs(world);

            if (requireMinDistance)
            {
                if (npcDistSq < minSq)
                    continue;

                coords = local;
                distSq = npcDistSq;
                return true;
            }

            if (npcDistSq <= distSq)
                continue;

            distSq = npcDistSq;
            best = local;
        }

        if (best == null)
            return false;

        coords = best.Value;
        return true;
    }

    private void CollectNpcPositions(EntityUid mapUid)
    {
        _npcPositions.Clear();
        var query = EntityQueryEnumerator<HTNComponent, TransformComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out _, out var xform, out var mob))
        {
            if (xform.MapUid != mapUid)
                continue;

            if (_mobState.IsDead(uid, mob))
                continue;

            _npcPositions.Add(_transform.GetWorldPosition(xform));
        }
    }

    private float MinDistanceSquaredToNpcs(Vector2 worldPos)
    {
        if (_npcPositions.Count == 0)
            return float.MaxValue;

        var min = float.MaxValue;
        foreach (var pos in _npcPositions)
        {
            var d = Vector2.DistanceSquared(worldPos, pos);
            if (d < min)
                min = d;
        }

        return min;
    }

    private bool TryEnsureBackroomsMap(out EntityUid mapUid)
    {
        if (_backroomsMap != null && Exists(_backroomsMap.Value) && !TerminatingOrDeleted(_backroomsMap.Value))
        {
            mapUid = _backroomsMap.Value;
            return true;
        }

        if (!_proto.TryIndex(BackroomsBiome, out var template))
        {
            mapUid = default;
            return false;
        }

        mapUid = _map.CreateMap(out _);
        _biome.EnsurePlanet(mapUid, template, mapLight: Color.FromHex("#C4B56A"));
        _meta.SetEntityName(mapUid, "Backrooms");
        _backroomsMap = mapUid;
        return true;
    }
}
