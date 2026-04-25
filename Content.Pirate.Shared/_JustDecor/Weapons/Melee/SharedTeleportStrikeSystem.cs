using System;
using System.Linq;
using System.Numerics;
using Content.Goobstation.Common.BlockTeleport;
using Content.Goobstation.Common.Weapons;
using Content.Pirate.Shared._JustDecor.Weapons.Melee;
using Content.Shared.ActionBlocker;
using Content.Shared.Interaction;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Shared.Interaction.Events;

namespace Content.Pirate.Shared._JustDecor.Weapons.Melee;

public sealed class SharedTeleportStrikeSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly RotateToFaceSystem _rotateToFace = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;

    /// <summary>
    /// Delay before performing the attack after teleporting.
    /// </summary>
    private const float AttackDelay = 0.1f;

    /// <summary>
    /// Additional delay added to return time after attack.
    /// </summary>
    private const float ExtraReturnDelay = 0.2f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TeleportStrikeComponent, GetLightAttackRangeEvent>(OnGetRange);
        SubscribeLocalEvent<TeleportStrikeComponent, LightAttackSpecialInteractionEvent>(OnSpecialAttack);
        SubscribeLocalEvent<TeleportStrikeLockComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
        SubscribeLocalEvent<TeleportStrikeLockComponent, ChangeDirectionAttemptEvent>(OnChangeDirection);
    }

    private void OnGetRange(Entity<TeleportStrikeComponent> ent, ref GetLightAttackRangeEvent args)
    {
        if (_net.IsServer)
            return;

        args.Range = Math.Max(args.Range, ent.Comp.MaxRange);
    }

    private void OnSpecialAttack(Entity<TeleportStrikeComponent> ent, ref LightAttackSpecialInteractionEvent args)
    {
        if (_net.IsClient)
            return;

        if (args.Target == null)
            return;

        var user = args.User;
        var target = args.Target.Value;

        if (HasComp<TeleportStrikeLockComponent>(user))
            return;

        if (!TryComp(ent, out MeleeWeaponComponent? melee) || melee.NextAttack > _timing.CurTime)
            return;

        var userXform = Transform(user);
        var targetXform = Transform(target);

        if (userXform.MapID != targetXform.MapID)
            return;

        var userPos = _xform.GetWorldPosition(userXform);
        var targetPos = _xform.GetWorldPosition(targetXform);

        var dir = targetPos - userPos;
        var distance = dir.Length();

        if (distance <= args.Range || distance > ent.Comp.MaxRange)
            return;

        var ev = new TeleportAttemptEvent(false);
        RaiseLocalEvent(user, ref ev);
        if (ev.Cancelled)
            return;

        var normalized = new Vector2(dir.X / distance, dir.Y / distance);
        var ray = new CollisionRay(
            userPos,
            normalized,
            (int) (CollisionGroup.Impassable | CollisionGroup.InteractImpassable));

        var result = _physics.IntersectRay(userXform.MapID, ray, distance, user).FirstOrNull();
        if (result != null && result.Value.HitEntity != target)
            return;

        var behindPos = targetPos + normalized * ent.Comp.BehindOffset;

        var originalCoords = userXform.Coordinates;
        var originalVelocity = Vector2.Zero;
        if (TryComp<PhysicsComponent>(user, out var physics))
        {
            originalVelocity = physics.LinearVelocity;
            _physics.SetLinearVelocity(user, Vector2.Zero, body: physics);
        }

        var lockComp = EnsureComp<TeleportStrikeLockComponent>(user);
        lockComp.ReturnCoordinates = originalCoords;
        lockComp.ReturnVelocity = originalVelocity;
        lockComp.Target = target;
        lockComp.Weapon = ent.Owner;
        lockComp.AttackTime = _timing.CurTime + TimeSpan.FromSeconds(AttackDelay);
        lockComp.ReturnTime = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.ReturnDelay + ExtraReturnDelay);
        Dirty(user, lockComp);

        _movementSpeed.RefreshMovementSpeedModifiers(user);

        if (ent.Comp.TeleportSound != null)
            _audio.PlayPredicted(ent.Comp.TeleportSound, user, user);

        // Teleport behind the target
        _xform.SetWorldPosition(user, behindPos);

        // Calculate and set rotation to face the target
        var dirToTarget = targetPos - behindPos;
        if (dirToTarget.LengthSquared() > 0.01f)
        {
            var angle = Angle.FromWorldVec(dirToTarget);
            _xform.SetWorldRotation(user, angle);
        }

        args.Cancel = true;
    }

    private void OnRefreshMovespeed(EntityUid uid, TeleportStrikeLockComponent comp, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(0f, 0f);
    }

    private void OnChangeDirection(EntityUid uid, TeleportStrikeLockComponent comp, ChangeDirectionAttemptEvent args)
    {
        args.Cancel();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<TeleportStrikeLockComponent>();
        while (query.MoveNext(out var uid, out var lockComp))
        {
            if (!Exists(uid))
                continue;

            // Perform attack when attack time is reached
            if (lockComp.AttackTime != TimeSpan.Zero && _timing.CurTime >= lockComp.AttackTime)
            {
                lockComp.AttackTime = TimeSpan.Zero;
                Dirty(uid, lockComp);

                if (TryComp<MeleeWeaponComponent>(lockComp.Weapon, out var melee) && Exists(lockComp.Target))
                {
                    _melee.AttemptLightAttack(uid, lockComp.Weapon, melee, lockComp.Target);

                    // Set cooldown after the attack
                    melee.NextAttack += TimeSpan.FromSeconds(0.2);
                    Dirty(lockComp.Weapon, melee);
                }
            }

            // Return to original position after return time
            if (_timing.CurTime < lockComp.ReturnTime)
                continue;

            _xform.SetCoordinates(uid, lockComp.ReturnCoordinates);

            if (TryComp<PhysicsComponent>(uid, out var physics))
                _physics.SetLinearVelocity(uid, lockComp.ReturnVelocity, body: physics);

            RemComp<TeleportStrikeLockComponent>(uid);
            _movementSpeed.RefreshMovementSpeedModifiers(uid);
        }
    }
}
