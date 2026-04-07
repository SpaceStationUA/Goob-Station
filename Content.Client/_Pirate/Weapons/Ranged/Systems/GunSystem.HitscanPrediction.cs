using System.Linq;
using System.Numerics;
using Content.Goobstation.Common.CCVar;
using Content.Shared.Damage.Components;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Reflect;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Utility;
using Robust.Shared.Containers;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    [Dependency] private readonly IConfigurationManager _cfgPirate = default!;
    [Dependency] private readonly SharedContainerSystem _containerPirate = default!;

    protected override void HandleHitscanShot(
        EntityUid gunUid,
        GunComponent gun,
        HitscanPrototype hitscan,
        EntityCoordinates fromCoordinates,
        EntityCoordinates fromEffect,
        MapCoordinates fromMap,
        Vector2 toMapBeforeRecoil,
        Vector2 mapDirection,
        EntityUid? user,
        ref bool userImpulse,
        List<EntityUid> shotProjectiles)
    {
        EntityUid? lastHit = null;

        var from = fromMap;
        var effectCoordinates = fromEffect;
        var dir = mapDirection.Normalized();
        var lastUser = user ?? gunUid;

        if (hitscan.Reflective != ReflectType.None)
        {
            for (var reflectAttempt = 0; reflectAttempt < 3; reflectAttempt++)
            {
                var ray = new CollisionRay(from.Position, dir, hitscan.CollisionMask);
                var rayCastResults = Physics.IntersectRay(from.MapId, ray, hitscan.MaxLength, lastUser, false).ToList();
                if (!rayCastResults.Any())
                    break;

                var result = rayCastResults[0];

                if (!_containerPirate.IsEntityOrParentInContainer(lastUser))
                {
                    foreach (var collide in rayCastResults)
                    {
                        if (collide.HitEntity != gun.Target &&
                            CompOrNull<RequireProjectileTargetComponent>(collide.HitEntity)?.Active == true &&
                            (_xform.GetMapCoordinates(collide.HitEntity).Position - toMapBeforeRecoil).Length() >
                            _cfgPirate.GetCVar(GoobCVars.CrawlHitzoneSize))
                        {
                            continue;
                        }

                        result = collide;
                        break;
                    }
                }

                var hit = result.HitEntity;
                lastHit = hit;

                FirePredictedHitscanEffects(effectCoordinates, result.Distance, dir.Normalized().ToAngle(), hitscan, hit);

                var ev = new HitScanReflectAttemptEvent(user, gunUid, hitscan.Reflective, dir, false, hitscan.Damage, hit);
                RaiseLocalEvent(hit, ref ev);

                if (!ev.Reflected)
                    break;

                effectCoordinates = Transform(hit).Coordinates;
                from = TransformSystem.ToMapCoordinates(effectCoordinates);
                dir = ev.Direction;
                lastUser = hit;
            }
        }

        if (lastHit == null)
            FirePredictedHitscanEffects(effectCoordinates, hitscan.MaxLength, dir.ToAngle(), hitscan);

        base.HandleHitscanShot(
            gunUid,
            gun,
            hitscan,
            fromCoordinates,
            fromEffect,
            fromMap,
            toMapBeforeRecoil,
            mapDirection,
            user,
            ref userImpulse,
            shotProjectiles);
    }

    private void FirePredictedHitscanEffects(
        EntityCoordinates fromCoordinates,
        float distance,
        Angle angle,
        HitscanPrototype hitscan,
        EntityUid? hitEntity = null)
    {
        if (!Timing.IsFirstTimePredicted)
            return;

        var sprites = new List<(NetCoordinates coordinates, Angle angle, SpriteSpecifier Sprite, float Distance)>();
        var fromXform = Transform(fromCoordinates.EntityId);

        var gridUid = fromXform.GridUid;
        if (gridUid != fromCoordinates.EntityId && TryComp(gridUid, out TransformComponent? gridXform))
        {
            var (_, gridRot, gridInvMatrix) = TransformSystem.GetWorldPositionRotationInvMatrix(gridXform);
            var map = TransformSystem.ToMapCoordinates(fromCoordinates);
            fromCoordinates = new EntityCoordinates(gridUid.Value, Vector2.Transform(map.Position, gridInvMatrix));
            angle -= gridRot;
        }
        else
        {
            angle -= TransformSystem.GetWorldRotation(fromXform);
        }

        if (distance >= 1f)
        {
            if (hitscan.MuzzleFlash != null)
            {
                var coords = fromCoordinates.Offset(angle.ToVec().Normalized() / 2);
                var netCoords = GetNetCoordinates(coords);

                sprites.Add((netCoords, angle, hitscan.MuzzleFlash, 1f));
            }

            if (hitscan.TravelFlash != null)
            {
                var coords = fromCoordinates.Offset(angle.ToVec() * (distance + 0.5f) / 2);
                var netCoords = GetNetCoordinates(coords);

                sprites.Add((netCoords, angle, hitscan.TravelFlash, distance - 1.5f));
            }
        }

        if (hitscan.ImpactFlash != null)
        {
            var coords = fromCoordinates.Offset(angle.ToVec() * distance);
            var netCoords = GetNetCoordinates(coords);

            sprites.Add((netCoords, angle.FlipPositive(), hitscan.ImpactFlash, 1f));
        }

        if (sprites.Count > 0)
            OnHitscan(new HitscanEvent { Sprites = sprites });
    }
}
