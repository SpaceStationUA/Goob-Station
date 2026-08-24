using System.Numerics;
using Content.Shared._DV.Psionics.Components.PsionicPowers;
using Content.Shared._DV.Psionics.Events;
using Content.Shared._DV.Psionics.Events.PowerActionEvents;
using Content.Shared.Coordinates;
using Content.Shared.Mobs.Systems;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;

namespace Content.Shared._DV.Psionics.Systems.PsionicPowers;

/// <summary>
/// This system enables a psionic being to emit a telekinetic pulse that pushes everyone around them away.
/// </summary>
public sealed class TelekineticPulsePowerSystem : BasePsionicPowerSystem<TelekineticPulsePowerComponent, TelekineticPulsePowerActionEvent>
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    protected override void OnPowerUsed(Entity<TelekineticPulsePowerComponent> psionic, ref TelekineticPulsePowerActionEvent args)
    {
        if (psionic.Comp.AbilitySound != null)
            _audio.PlayPredicted(psionic.Comp.AbilitySound, psionic, psionic);

        PushEntitiesAround(psionic.Owner, psionic.Comp.Radius, psionic.Comp.PushStrength);

        SpawnAttachedTo(psionic.Comp.Effect, psionic.Owner.ToCoordinates());
        args.Handled = true;
        AfterPowerUsed(psionic, args.Performer);
    }

    /// <summary>
    /// Push all entities around the source away from it.
    /// </summary>
    private void PushEntitiesAround(EntityUid source, float radius, float pushStrength)
    {
        var sourcePos = Transform(source).Coordinates.Position;

        foreach (var entity in _lookup.GetEntitiesInRange(source, radius))
        {
            // Don't push yourself.
            if (entity == source)
                continue;

            // Don't push dead/dying entities.
            if (_mobState.IsDead(entity))
                continue;

            // Calculate direction away from source.
            var entityPos = Transform(entity).Coordinates.Position;
            var direction = entityPos - sourcePos;

            // If entities are at the exact same position, push in a random direction.
            if (direction.LengthSquared() < 0.001f)
            {
                direction = new Vector2(
                    _random.NextFloat(-1f, 1f),
                    _random.NextFloat(-1f, 1f)
                );
                if (direction.LengthSquared() < 0.001f)
                    direction = Vector2.UnitX;
            }

            direction = direction.Normalized();

            // Apply the push force.
            if (TryComp<PhysicsComponent>(entity, out var physics))
            {
                _physics.SetLinearVelocity(entity, direction * pushStrength, body: physics);
            }
        }
    }
}
