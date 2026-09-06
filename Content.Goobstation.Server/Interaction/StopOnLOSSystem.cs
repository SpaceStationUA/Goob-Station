// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Interaction;
using Content.Shared.ActionBlocker;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Events;
using Content.Shared.NPC.Systems;

namespace Content.Goobstation.Server.Interaction;

public sealed class StopOnLOSSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<StopOnLOSComponent, UpdateCanMoveEvent>(OnAttempt);
        SubscribeLocalEvent<StopOnLOSComponent, AttackAttemptEvent>(OnAttempt);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<StopOnLOSComponent>();
        while (query.MoveNext(out var entity, out var comp))
        {
            var observers = new List<EntityUid>();

            foreach (var ent in _faction.GetNearbyHostiles(entity, comp.SightRange))
            {
                if (!TryComp<MobStateComponent>(ent, out var state) || HasComp<StopOnLOSComponent>(ent))
                    continue;

                if (_mind.TryGetMind(ent, out _, out _) && state.CurrentState == MobState.Alive)
                    observers.Add(ent);
            }

            // Aggregate LOS across all observers first — mutating CanMove inside the loop
            // made the result depend on GetNearbyHostiles order (last miss could unlock movement).
            var isObserved = false;
            foreach (var target in observers)
            {
                var direction = _transform.GetWorldPosition(entity) - _transform.GetWorldPosition(target);
                if (direction.LengthSquared() < 0.0001f)
                    continue;

                direction = System.Numerics.Vector2.Normalize(direction);

                var (_, worldRot) = _transform.GetWorldPositionRotation(target);
                var lookDeg = worldRot.Degrees;
                var dirDeg = direction.ToWorldAngle().Degrees;
                var difference = Math.Min(
                    Math.Abs(dirDeg - lookDeg),
                    Math.Abs(lookDeg - dirDeg));

                var notOccluded = _examine.InRangeUnOccluded(target, entity, comp.SightRange, null);

                if (difference < comp.SightAngle && notOccluded)
                {
                    isObserved = true;
                    break;
                }
            }

            comp.CanMove = !isObserved;
            _blocker.UpdateCanMove(entity);
        }
    }

    private void OnAttempt(EntityUid uid, StopOnLOSComponent comp, CancellableEntityEventArgs args)
    {
        if (!comp.CanMove)
            args.Cancel();
    }
}
