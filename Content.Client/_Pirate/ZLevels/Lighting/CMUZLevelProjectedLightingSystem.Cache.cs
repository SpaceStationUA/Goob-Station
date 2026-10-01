// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._Pirate.ZLevels.Core;
using Content.Shared.CCVar;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Events;
using static Robust.Shared.GameObjects.SharedMapSystem;

namespace Content.Client._Pirate.ZLevels.Lighting;

public sealed partial class CMUZLevelProjectedLightingSystem
{
    private const float RayGrazeOffset = 0.01f;

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

    /// <summary>
    /// The frame a light's projection geometry is decided in: its grid's. On a moving grid, world positions
    /// carry float noise that changes every frame, and exact ties (equal openings, a lamp diagonal to a hole,
    /// a ray through a wall corner) would flip from frame to frame. Lights off any grid use world space.
    /// </summary>
    private readonly record struct LightFrame(EntityUid Grid, Matrix3x2 ToWorld, Matrix3x2 ToLocal, Vector2 Source)
    {
        public Vector2 World(Vector2 local) => Grid.IsValid() ? Vector2.Transform(local, ToWorld) : local;

        public Vector2 Local(Vector2 world) =>
            Grid.IsValid() ? CMUZLevelOpeningCache.SnapLocalPosition(Vector2.Transform(world, ToLocal)) : world;
    }

    private LightFrame GetLightFrame(SourceLight source)
    {
        if (!_xformQuery.TryComp(source.Entity, out var xform) || xform.GridUid is not { } grid)
            return new LightFrame(EntityUid.Invalid, Matrix3x2.Identity, Matrix3x2.Identity, source.WorldPosition);

        var frame = new LightFrame(grid, _transform.GetWorldMatrix(grid), _transform.GetInvWorldMatrix(grid), default);
        // A light parented straight to its grid already has an exact local position.
        return frame with { Source = xform.ParentUid == grid ? xform.LocalPosition : frame.Local(source.WorldPosition) };
    }

    /// <returns>Unoccluded openings in <paramref name="frame"/>, with their distance from the light.</returns>
    private List<(Vector2 Center, float Distance)> GetUnoccludedLightOpenings(
        SourceLight source, LightFrame frame, MapId sourceMap, EntityUid openingMap, MapId openingMapId)
    {
        var lifetime = _config.GetCVar(CCVars.ZProjectedLightCacheSeconds);
        lifetime = float.IsFinite(lifetime) ? Math.Clamp(lifetime, 0f, 0.25f) : 0f;
        var entry = _lightOpeningCache.Get(new ZLightOpeningCache.Key(source.Entity, sourceMap, openingMap, frame.Grid),
            frame.Source, source.Radius, _timing.RealTime, lifetime, out var refresh);
        if (!refresh)
            return entry.Openings;

        _tempOpenings.Clear();
        _zLevels.FindOpeningCentersNear(openingMapId, source.WorldPosition, source.Radius,
            _tempOpenings, _openingGrids);

        foreach (var (center, _) in _tempOpenings)
        {
            if (IsLightRayBlocked(sourceMap, source.WorldPosition, center, source.Entity))
                continue;

            var local = frame.Local(center);
            entry.Openings.Add((local, Vector2.Distance(local, frame.Source)));
        }

        return entry.Openings;
    }

    // A ray through a wall corner hits or misses on float noise. Two rays a hair to either side decide it:
    // the opening counts as blocked only if both are.
    private bool IsLightRayBlocked(MapId map, Vector2 from, Vector2 to, EntityUid source)
    {
        var direction = to - from;
        var length = direction.Length();
        if (length <= 0.01f)
            return false;

        direction /= length;
        var side = new Vector2(-direction.Y, direction.X) * RayGrazeOffset;
        return RayHits(map, from + side, direction, length, source) &&
               RayHits(map, from - side, direction, length, source);
    }

    private bool RayHits(MapId map, Vector2 from, Vector2 direction, float length, EntityUid source)
    {
        var ray = new CollisionRay(from, direction, (int) CollisionGroup.Opaque);
        foreach (var _ in _physics.IntersectRay(map, ray, length, ignoredEnt: source, returnOnFirstHit: true))
            return true;

        return false;
    }
}
