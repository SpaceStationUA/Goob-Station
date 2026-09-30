// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.CCVar;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Events;
using static Robust.Shared.GameObjects.SharedMapSystem;

namespace Content.Client._Pirate.ZLevels.Lighting;

public sealed partial class CMUZLevelProjectedLightingSystem
{
    private readonly ZLightOpeningCache _lightOpeningCache = new();

    private void InitializeLightOpeningCache()
    {
        SubscribeLocalEvent<TileChangedEvent>(OnLightOpeningTilesChanged);
        SubscribeLocalEvent<CollisionChangeEvent>(OnLightOpeningCollisionChanged);
    }

    private void OnLightOpeningTilesChanged(ref TileChangedEvent args)
    {
        _lightOpeningCache.Clear();
    }

    private void OnLightOpeningCollisionChanged(ref CollisionChangeEvent args)
    {
        // Only opaque bodies can block the light rays; items entering/leaving PVS would otherwise
        // wipe the cache every frame. Includes doors becoming solid/non-solid. Moving blockers and
        // fixture changes that do not raise this event are covered by the bounded lifetime (100 ms).
        if ((args.Body.CollisionLayer & (int) CollisionGroup.Opaque) == 0)
            return;

        _lightOpeningCache.Clear();
    }

    private List<(Vector2 Center, float Distance)> GetUnoccludedLightOpenings(
        SourceLight source, MapId sourceMap, EntityUid openingMap, MapId openingMapId)
    {
        var lifetime = _config.GetCVar(CCVars.ZProjectedLightCacheSeconds);
        lifetime = float.IsFinite(lifetime) ? Math.Clamp(lifetime, 0f, 0.25f) : 0f;
        var entry = _lightOpeningCache.Get(new ZLightOpeningCache.Key(source.Entity, sourceMap, openingMap),
            source.WorldPosition, source.Radius, _timing.RealTime, lifetime, out var refresh);
        if (!refresh)
            return entry.Openings;

        _tempOpenings.Clear();
        _zLevels.FindOpeningCentersNear(openingMapId, source.WorldPosition, source.Radius,
            _tempOpenings, _openingGrids);

        foreach (var opening in _tempOpenings)
        {
            var direction = opening.Center - source.WorldPosition;
            var length = direction.Length();
            var blocked = false;
            if (length > 0.01f)
            {
                var ray = new CollisionRay(source.WorldPosition, direction / length, (int) CollisionGroup.Opaque);
                foreach (var _ in _physics.IntersectRay(sourceMap, ray, length,
                             ignoredEnt: source.Entity, returnOnFirstHit: true))
                {
                    blocked = true;
                    break;
                }
            }

            if (!blocked)
                entry.Openings.Add(opening);
        }

        return entry.Openings;
    }
}
