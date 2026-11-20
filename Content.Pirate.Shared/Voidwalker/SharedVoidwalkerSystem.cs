using Content.Pirate.Shared.Voidwalker.TemporarilyDisableCollision;
using Content.Shared.Gravity;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Pirate.Shared.Voidwalker;

public sealed class SharedVoidwalkerSystem : EntitySystem
{
    [Dependency] private readonly SharedStealthSystem _stealth = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();


        SubscribeLocalEvent<VoidwalkerComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMoveSpeed);
        SubscribeLocalEvent<VoidwalkerComponent, VoidwalkerSpacedStatusChangedEvent>(OnSpacedStatusChanged);

        SubscribeLocalEvent<VoidwalkerComponent, ShotAttemptedEvent>(OnShotAttempted); // someone should rly genericize these lol

        SubscribeLocalEvent<VoidwalkerComponent, CanWeightlessMoveEvent>(OnCanWeightlessMove);
        SubscribeLocalEvent<VoidwalkerComponent, StopThrowEvent>(OnStopThrow);
    }

    private void OnSpacedStatusChanged(Entity<VoidwalkerComponent> entity, ref VoidwalkerSpacedStatusChangedEvent args)
    {
        if (TerminatingOrDeleted(entity))
            return;

        if (args.Spaced
            && entity.Comp.UnsettleDoAfterId == null
            && _timing.CurTime >= entity.Comp.RevealedUntil)
        {
            EnsureComp<StealthComponent>(entity); // Okay, this is a weird way to do this, but stealth literally doesn't work if you enable/disable it so IDK :shrug:
           _stealth.SetThermalsImmune(entity, args.Spaced); // tell me if you find a better way to fix this - delph
        }
        else
            RemComp<StealthComponent>(entity);

        _movement.RefreshMovementSpeedModifiers(entity);
        Dirty(entity);
    }

    private void OnRefreshMoveSpeed(Entity<VoidwalkerComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var modifier = ent.Comp.IsInSpace ? 1f : ent.Comp.NonSpacedSpeedModifier;
        args.ModifySpeed(modifier, modifier);
        Dirty(ent);
    }

    private static void OnShotAttempted(Entity<VoidwalkerComponent> ent, ref ShotAttemptedEvent args) // no fun allowed or something
    {
        args.Cancel();
    }

    private static void OnCanWeightlessMove(Entity<VoidwalkerComponent> ent, ref CanWeightlessMoveEvent args)
    {
        args.CanMove = true;
    }

    private void OnStopThrow(Entity<VoidwalkerComponent> ent, ref StopThrowEvent args)
    {
        if (!_gravity.IsWeightless(ent.Owner)
            || !TryComp<PhysicsComponent>(ent, out var physics)
            || !TryComp<MovementSpeedModifierComponent>(ent, out var move))
            return;

        var velocity = physics.LinearVelocity;
        var maxSpeed = move.WeightlessSprintSpeed;
        // Low weightless friction lets throws coast too far.
        if (velocity.Length() <= maxSpeed)
            return;

        _physics.SetLinearVelocity(ent, velocity.Normalized() * maxSpeed, body: physics);
    }

}
