// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Pirate.Shared.Skia;

public abstract class SharedSkiaLightReactiveSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<SkiaLightReactiveComponent> _lightReactive;

    public override void Initialize()
    {
        base.Initialize();
        _lightReactive = GetEntityQuery<SkiaLightReactiveComponent>();
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<SkiaLightReactiveComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.Manual || _timing.CurTime < comp.NextUpdate)
                continue;

            comp.NextUpdate = _timing.CurTime + comp.UpdateFrequency;
            Dirty(uid, comp);

            if (_mobState.IsDead(uid) && comp.OnlyWhileAlive)
                continue;

            comp.CurrentLightLevel = GetLightLevelForPoint(uid, xform);
        }
    }

    public abstract HashSet<Entity<SharedPointLightComponent>> GetLights(EntityUid targetEntity);

    public float GetLightLevel(EntityUid uid, bool forceUpdate = false)
    {
        if (!_lightReactive.TryComp(uid, out SkiaLightReactiveComponent? comp))
            return 0f;

        if (forceUpdate)
            comp.CurrentLightLevel = GetLightLevelForPoint(uid);

        return comp.CurrentLightLevel;
    }

    public float GetLightLevelForPoint(EntityUid uid, TransformComponent? xform = null)
    {
        var value = 0f;
        var map = _transform.GetMap((uid, xform));
        if (TryComp(map, out MapLightComponent? mapLight))
            value += (mapLight.AmbientLightColor.R + mapLight.AmbientLightColor.G + mapLight.AmbientLightColor.B) / 3f;

        var position = _transform.GetWorldPosition(uid);
        foreach (var (lightUid, lightComp) in GetLights(uid))
        {
            var energy = lightComp.Energy;
            var radius = lightComp.Radius;
            if (!lightComp.NetSyncEnabled)
            {
                var lightEnergyEvent = new SkiaGetLightEnergyEvent();
                RaiseLocalEvent(lightUid, ref lightEnergyEvent);
                energy = lightEnergyEvent.LightEnergy;
                radius = lightEnergyEvent.LightRadius;
                if (MathHelper.CloseTo(energy, 0f))
                    continue;
            }

            energy = MathF.Min(energy, 2f);
            if (_transform.GetMap(lightUid) != map)
                continue;

            var lightPosition = _transform.GetWorldPosition(lightUid);
            var squaredDistance = Vector2.DistanceSquared(position, lightPosition);
            if (squaredDistance > radius * radius)
                continue;

            if (squaredDistance < 0.01f)
            {
                value += energy;
                continue;
            }

            // Cast from Skia toward the light. Skia's flying fixture is opaque, so ignore both
            // endpoints and inspect every hit; otherwise the target itself can hide a wall hit.
            var distance = MathF.Sqrt(squaredDistance);
            var ray = new CollisionRay(position, (lightPosition - position).Normalized(), (int) CollisionGroup.Opaque);
            var hits = _physics.IntersectRayWithPredicate(
                _transform.GetMapId((uid, xform)),
                ray,
                distance,
                predicate: hitEntity => hitEntity == uid || hitEntity == lightUid,
                returnOnFirstHit: false);
            if (hits.Any())
                continue;

            if (lightComp.MaskPath == "/Textures/Effects/LightMasks/cone.png")
            {
                var forward = _transform.GetWorldRotation(lightUid).RotateVec(new Vector2(0f, -1f));
                energy *= MathF.Max(0f, Vector2.Dot((position - lightPosition).Normalized(), forward));
            }
            else if (lightComp.MaskPath == "/Textures/Effects/LightMasks/double_cone.png")
            {
                var forward = _transform.GetWorldRotation(lightUid).RotateVec(new Vector2(0f, -1f));
                energy *= MathF.Abs(Vector2.Dot((position - lightPosition).Normalized(), forward));
            }

            value += energy * (1f - squaredDistance / (radius * radius));
        }

        return value;
    }
}

/// <summary>
/// Lets unsynchronized light sources report their current light output.
/// </summary>
[ByRefEvent]
public record struct SkiaGetLightEnergyEvent
{
    public float LightEnergy;
    public float LightRadius;
}
