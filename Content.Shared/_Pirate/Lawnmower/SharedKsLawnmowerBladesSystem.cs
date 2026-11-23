// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Klovnstation14.

using System.Numerics;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Camera;
using Content.Shared.Damage.Systems;
using Content.Shared.Emag.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Zombies;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._Pirate.Lawnmower;

/// <summary>
///     Runs the mower's blades. The rider's client predicts its own mower; the server handles the rest.
/// </summary>
public abstract partial class SharedKsLawnmowerBladesSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageableSystem = default!;
    [Dependency] private EmagSystem _emagSystem = default!;
    [Dependency] private SharedBodySystem _bodySystem = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private INetManager _netManager = default!;
    [Dependency] private ISharedPlayerManager _playerManager = default!;
    [Dependency] private MobStateSystem _mobStateSystem = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private SharedBloodstreamSystem _bloodstreamSystem = default!;
    [Dependency] private SharedCameraRecoilSystem _cameraSystem = default!;
    [Dependency] private SharedPhysicsSystem _physicsSystem = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private SharedStunSystem _stunSystem = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!;
    [Dependency] private StandingStateSystem _standingSystem = default!;
    [Dependency] private ThrowingSystem _throwingSystem = default!;

    // Scratch set for the mower being updated.
    private readonly HashSet<EntityUid> _touching = new();

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<KsLawnmowerBladesComponent, MovementRelayTargetComponent, InputMoverComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var bladesComponent, out var relayComponent, out var moverComponent, out var physicsComponent))
        {
            // A client only predicts the mower its own player rides.
            if (_netManager.IsClient && relayComponent.Source != _playerManager.LocalEntity)
                continue;

            var entity = (uid, bladesComponent);
            UpdateTouching(entity);

            if (!moverComponent.HasDirectionalMovement || !moverComponent.CanMove || moverComponent.WishDir == Vector2.Zero)
                continue;

            var moving = physicsComponent.LinearVelocity.LengthSquared() >= bladesComponent.MinDrivingSpeed * bladesComponent.MinDrivingSpeed;
            if (moving)
                MowTile(entity, Transform(uid));

            // A standing target can stop the mower in the same tick that the blades detect contact.
            var crushed = _emagSystem.CheckFlag(uid, EmagType.Interaction) &&
                ProcessContacts(entity, relayComponent.Source, moverComponent.WishDir.Normalized());

            if (crushed)
                _audioSystem.PlayPredicted(bladesComponent.CrushSound, uid, relayComponent.Source);
            else if (moving && _gameTiming.CurTime >= bladesComponent.NextDriveSound)
                _audioSystem.PlayPredicted(bladesComponent.DriveSound, uid, relayComponent.Source);
            else
                continue;

            bladesComponent.NextDriveSound = _gameTiming.CurTime + bladesComponent.DriveSoundInterval;
            Dirty(uid, bladesComponent);
        }
    }

    /// <summary>
    ///     Gathers what the blades touch now, and re-arms a hit on every target that left them.
    /// </summary>
    private void UpdateTouching(Entity<KsLawnmowerBladesComponent> mower)
    {
        _touching.Clear();
        var contacts = _physicsSystem.GetContacts(mower.Owner);
        while (contacts.MoveNext(out var contact))
        {
            if (contact.IsTouching && contact.OurFixture(mower.Owner).Id == mower.Comp.BladeFixture)
                _touching.Add(contact.OtherEnt(mower.Owner));
        }

        if (mower.Comp.HitTargets.Count > 0 && mower.Comp.HitTargets.RemoveWhere(IsNotTouching) > 0)
            Dirty(mower);
    }

    private bool IsNotTouching(NetEntity targetNetEntity)
    {
        return !TryGetEntity(targetNetEntity, out var targetUid) || !_touching.Contains(targetUid.Value);
    }

    private bool ProcessContacts(Entity<KsLawnmowerBladesComponent> mower, EntityUid riderUid, Vector2 direction)
    {
        var crushed = false;
        var mowerPosition = _transformSystem.GetWorldPosition(mower.Owner);
        foreach (var victimUid in _touching)
        {
            if (victimUid == riderUid || TerminatingOrDeleted(victimUid) || EntityManager.IsQueuedForDeletion(victimUid) ||
                mower.Comp.HitTargets.Contains(GetNetEntity(victimUid)) ||
                !TryComp<MobStateComponent>(victimUid, out var mobStateComponent))
            {
                continue;
            }

            var offset = _transformSystem.GetWorldPosition(victimUid) - mowerPosition;
            if (Vector2.Dot(direction, offset) < 0f)
                continue;

            mower.Comp.HitTargets.Add(GetNetEntity(victimUid));
            Dirty(mower);
            var dead = _mobStateSystem.IsDead(victimUid, mobStateComponent);
            var humanoid = HasComp<HumanoidAppearanceComponent>(victimUid);
            var zombie = HasComp<ZombieComponent>(victimUid);
            var prone = _standingSystem.IsDown(victimUid);

            if ((dead && !humanoid) || (zombie && (prone || dead)))
            {
                HitWithBlades(mower, victimUid, riderUid, mower.Comp.GibPopup);
                // The server tells the victim's client; the rider's has no use for it.
                if (_netManager.IsServer)
                    _cameraSystem.KickCamera(victimUid, direction * mower.Comp.VictimCameraKick);

                // Does nothing on a client, so the body goes when the server's state arrives.
                if (_netManager.IsServer)
                    _bodySystem.GibBody(victimUid, gibOrgans: true);
                crushed = true;
            }
            else if (!dead && humanoid && prone)
            {
                HitWithBlades(mower, victimUid, riderUid, mower.Comp.CrushPopup);
                _stunSystem.TryKnockdown(victimUid, mower.Comp.CrushKnockdownTime);
                CrushVictim(mower, victimUid, riderUid);
                crushed = true;
            }
            else if (!dead && !prone)
            {
                _damageableSystem.TryChangeDamage(victimUid, mower.Comp.BumpDamage, ignoreResistances: true, origin: riderUid);
                _audioSystem.PlayPredicted(mower.Comp.BumpSound, victimUid, riderUid);
                var away = offset.LengthSquared() > 0.001f ? offset.Normalized() : direction;
                _throwingSystem.TryThrow(victimUid, away * mower.Comp.BumpThrowDistance, baseThrowSpeed: mower.Comp.BumpThrowSpeed, recoil: false, doSpin: false, playSound: false);
            }
        }

        // The rider's client kicks its own camera as it predicts the hit, so the server sends none.
        if (crushed && _netManager.IsClient && _gameTiming.IsFirstTimePredicted)
            _cameraSystem.KickCamera(riderUid, -direction * mower.Comp.RiderCameraKick);

        return crushed;
    }

    private void HitWithBlades(Entity<KsLawnmowerBladesComponent> mower, EntityUid victimUid, EntityUid riderUid, LocId message)
    {
        _popupSystem.PopupEntity(Loc.GetString(message, ("mower", mower.Owner), ("victim", victimUid)),
            mower.Owner, type: PopupType.LargeCaution);
        _audioSystem.PlayPredicted(mower.Comp.BladeSound, mower.Owner, riderUid);

        // Bleeding out spills a puddle, which is the server's to spawn.
        if (_netManager.IsServer && HasComp<BloodstreamComponent>(victimUid) && _bloodstreamSystem.TryBleedOut(victimUid, mower.Comp.BleedAmount))
            mower.Comp.BloodyTiles = mower.Comp.TracksTiles;
    }

    /// <summary>
    ///     Mows the tile under the mower. Server-only: tiles and decals aren't predicted.
    /// </summary>
    protected virtual void MowTile(Entity<KsLawnmowerBladesComponent> mower, TransformComponent transform)
    {
    }

    /// <summary>
    ///     Hurts a prone victim under the blades. Server-only: the damage cap reads server-side gib thresholds.
    /// </summary>
    protected virtual void CrushVictim(Entity<KsLawnmowerBladesComponent> mower, EntityUid victimUid, EntityUid riderUid)
    {
    }
}
