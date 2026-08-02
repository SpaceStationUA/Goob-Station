// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.Coordinates;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Physics;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Pirate.Shared.Skia;

/// <summary>
/// Grants Skia its light-shattering scream without depending on the psionics subsystem.
/// </summary>
public sealed class SkiaScreamSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPoweredLightSystem _lights = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SkiaScreamComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SkiaScreamComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SkiaScreamComponent, SkiaScreamActionEvent>(OnScream);
    }

    private void OnMapInit(Entity<SkiaScreamComponent> entity, ref MapInitEvent args)
    {
        _actions.AddAction(entity, ref entity.Comp.ActionEntity, entity.Comp.ActionId);
    }

    private void OnShutdown(Entity<SkiaScreamComponent> entity, ref ComponentShutdown args)
    {
        _actions.RemoveAction(entity.Owner, entity.Comp.ActionEntity);
    }

    private void OnScream(Entity<SkiaScreamComponent> entity, ref SkiaScreamActionEvent args)
    {
        if (_timing.ApplyingState || args.Handled)
            return;

        _audio.PlayPredicted(entity.Comp.AbilitySound, entity, entity);
        ShatterLightsAround(
            entity.Owner,
            entity.Comp.Radius,
            entity.Comp.LineOfSight,
            entity.Comp.PenetratingRadius);
        SpawnAttachedTo(entity.Comp.Effect, entity.Owner.ToCoordinates());

        args.Handled = true;
    }

    public void ShatterLightsAround(EntityUid source, float range, bool lineOfSight, float penetratingRadius = 0f)
    {
        var sourcePosition = _transform.GetWorldPosition(source);

        HashSet<Entity<PoweredLightComponent>> lightsInRange = [];
        _lookup.GetEntitiesInRange(Transform(source).Coordinates, range, lightsInRange);
        foreach (var light in lightsInRange)
        {
            if (lineOfSight)
            {
                var lightPosition = _transform.GetWorldPosition(light);
                var squaredDistance = Vector2.DistanceSquared(sourcePosition, lightPosition);
                if (squaredDistance > penetratingRadius * penetratingRadius)
                {
                    var ray = new CollisionRay(
                        sourcePosition,
                        (lightPosition - sourcePosition).Normalized(),
                        (int) CollisionGroup.Opaque);
                    var hits = _physics.IntersectRay(
                        _transform.GetMapId(source),
                        ray,
                        MathF.Sqrt(squaredDistance) - 0.5f,
                        returnOnFirstHit: true);

                    if (hits.Any() && hits.First().Distance != 0)
                        continue;
                }
            }

            _lights.TryDestroyBulb(light, light.Comp, source);
        }

        HashSet<Entity<SkiaFlareGunPelletComponent>> flaresInRange = [];
        _lookup.GetEntitiesInRange(Transform(source).Coordinates, range, flaresInRange);
        foreach (var flare in flaresInRange)
        {
            PredictedQueueDel(flare);
        }
    }
}
