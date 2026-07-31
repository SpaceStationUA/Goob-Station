using System.Linq;
using System.Numerics;
using Content.Shared.Light.Components;
using Content.Server.Light.EntitySystems;
using Content.Shared._DV.Psionics.Events;
using Content.Shared.Physics;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Pirate.Server.Psionics.Systems;

public sealed class PsychokineticScreamLightSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly PoweredLightSystem _poweredLight = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PsychokineticScreamShatterLightEvent>(OnShatterLights);
    }

    private void OnShatterLights(ref PsychokineticScreamShatterLightEvent args)
    {
        if (!Exists(args.Source))
            return;

        var sourcePos = _transform.GetWorldPosition(args.Source);
        var sourceCoords = Transform(args.Source).Coordinates;

        HashSet<Entity<PoweredLightComponent>> lightsInRange = [];
        _lookup.GetEntitiesInRange(sourceCoords, args.Range, lightsInRange);

        foreach (var light in lightsInRange)
        {
            if (args.LineOfSight)
            {
                var lightPos = _transform.GetWorldPosition(light);
                var sqrDistance = Vector2.DistanceSquared(sourcePos, lightPos);
                if (sqrDistance > args.PenetratingRadius * args.PenetratingRadius)
                {
                    var ray = new CollisionRay(sourcePos, (lightPos - sourcePos).Normalized(), (int) CollisionGroup.Opaque);
                    var hit = _physics.IntersectRay(_transform.GetMapId(args.Source), ray, MathF.Sqrt(sqrDistance) - 0.5f, returnOnFirstHit: true);
                    if (hit.Any() && hit.First().Distance != 0)
                        continue;
                }
            }

            _poweredLight.TryDestroyBulb(light, light.Comp);
        }
    }
}
