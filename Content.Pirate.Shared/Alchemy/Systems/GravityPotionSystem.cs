using Content.Pirate.Shared.Alchemy.Components;
using Content.Shared.Item;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Pirate.Shared.Alchemy.Systems;

public sealed class GravityPotionSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<GravityPotionComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (_timing.CurTime < comp.NextUpdate)
                continue;

            comp.NextUpdate = _timing.CurTime + TimeSpan.FromSeconds(comp.Interval);
            var origin = xform.MapPosition.Position;

            foreach (var nearby in _lookup.GetEntitiesInRange(uid, comp.Radius, LookupFlags.Dynamic | LookupFlags.Sundries))
            {
                if (nearby == uid
                    || !HasComp<ItemComponent>(nearby)
                    || !TryComp<TransformComponent>(nearby, out var nearbyXform)
                    || !TryComp<PhysicsComponent>(nearby, out var physics)
                    || physics.BodyType == Robust.Shared.Physics.BodyType.Static)
                    continue;

                var displacement = origin - nearbyXform.MapPosition.Position;
                var distance = displacement.Length();
                if (distance < 0.1f)
                    continue;

                var impulse = Vector2.Normalize(displacement) * (comp.PullStrength / MathF.Max(distance, 0.75f)) * physics.Mass;
                _physics.ApplyLinearImpulse(nearby, impulse, body: physics);
            }
        }
    }
}
