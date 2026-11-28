// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Weapons.MeleeDash;
using Content.Shared.Emoting;
using Content.Shared.Gravity; // Pirate: Omu rapier gravity restriction
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Popups; // Pirate: Omu rapier gravity restriction
using Content.Shared.Standing;
using Content.Shared.Throwing;
using Content.Shared.Timing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;

namespace Content.Goobstation.Shared.Weapons.MeleeDash;

public sealed partial class MeleeDashSystem : EntitySystem
{
    [Dependency] private UseDelaySystem _useDelay = default!;
    [Dependency] private Emoting.SharedAnimatedEmotesSystem _emotes = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedGravitySystem _gravity = default!; // Pirate
    [Dependency] private SharedPopupSystem _popup = default!; // Pirate

    private const int DashCollisionLayer = (int) CollisionGroup.MidImpassable;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DashingComponent, LandEvent>((Entity<DashingComponent> ent, ref LandEvent _) => EndDash(ent));
        SubscribeLocalEvent<DashingComponent, StopThrowEvent>((Entity<DashingComponent> ent, ref StopThrowEvent _) => EndDash(ent));
        SubscribeLocalEvent<DashingComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<DashingComponent, StartCollideEvent>(OnCollide);
        SubscribeAllEvent<MeleeDashEvent>(OnDash);
    }

    private void OnCollide(Entity<DashingComponent> ent, ref StartCollideEvent args)
    {
        var (uid, comp) = ent;

        if (!TryComp(uid, out ActorComponent? actor))
            return;

        if (!TryComp(comp.Weapon, out MeleeWeaponComponent? melee))
            return;

        var hit = GetNetEntity(args.OtherEntity); // Pirate
        if (comp.HitEntities.Contains(hit))
            return;

        if (!HasComp<MobStateComponent>(args.OtherEntity))
            return;

        if (!_hands.IsHolding(ent.Owner, ent.Comp.Weapon))
            return;

        comp.HitEntities.Add(hit);
        Dirty(ent);

        var ev = new LightAttackEvent(hit,
            GetNetEntity(comp.Weapon.Value),
            GetNetCoordinates(Transform(args.OtherEntity).Coordinates));
        _melee.DoLightAttack(uid, ev, comp.Weapon.Value, melee, actor.PlayerSession);
    }

    private void OnPreventCollide(Entity<DashingComponent> ent, ref PreventCollideEvent args)
    {
        // Ignore only contacts that would disappear if this fixture ignored MidImpassable.
        if ((args.OurFixture.CollisionMask & args.OtherFixture.CollisionLayer & ~DashCollisionLayer) == 0 &&
            (args.OtherFixture.CollisionMask & args.OurFixture.CollisionLayer) == 0)
            args.Cancelled = true;
    }

    private void EndDash(Entity<DashingComponent> ent)
    {
        RemComp<DashingComponent>(ent);
        _physics.RegenerateContacts(ent.Owner);
    }

    private void OnDash(MeleeDashEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity == null)
            return;

        var user = args.SenderSession.AttachedEntity.Value;

        if (_standing.IsDown(user))
            return;

        if (_container.IsEntityInContainer(user))
            return;

        var weapon = GetEntity(msg.Weapon);

        if (!TryComp(weapon, out MeleeDashComponent? dash) ||
            !TryComp(weapon, out UseDelayComponent? delay) || _useDelay.IsDelayed((weapon, delay)))
            return;

        // Pirate: preserve Omu's gravity restriction on the warden rapier.
        if (!dash.CanDashWhileWeightless && _gravity.IsWeightless(user))
        {
            _popup.PopupClient(Loc.GetString("no-dash-while-weightless"), user, user, PopupType.Medium);
            return;
        }

        var length = msg.Direction.Length();
        if (!float.IsFinite(length) || length <= 0f)
            return;

        var dir = msg.Direction / length * MathF.Min(length, dash.MaxDashLength);

        if (!_hands.IsHolding(user, weapon) ||
            !TryComp(user, out PhysicsComponent? body) ||
            (body.BodyType & (BodyType.Dynamic | BodyType.KinematicController)) == 0)
            return;

        _throwing.TryThrow(user, dir, dash.DashForce, null, 0f, null, false, false, false, false, false);
        if (!HasComp<ThrownItemComponent>(user))
            return;

        _useDelay.TryResetDelay((weapon, delay));

        var dashing = EnsureComp<DashingComponent>(user);

        dashing.Weapon = weapon;
        dashing.HitEntities.Clear();
        Dirty(user, dashing);
        _physics.RegenerateContacts((user, body));

        _audio.PlayPredicted(dash.DashSound, user, user);

        if (dash.EmoteOnDash != null)
            _emotes.SetEmote(user, dash.EmoteOnDash);
    }
}
