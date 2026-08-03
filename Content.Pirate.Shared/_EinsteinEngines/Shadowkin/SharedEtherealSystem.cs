using Content.Shared.Physics;
using Robust.Shared.Physics;
using System.Linq;
using Robust.Shared.Physics.Systems;
using Content.Shared.Interaction.Events;
using Robust.Shared.Timing;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.CombatMode.Pacification;
using Content.Shared._Pirate.CCVars;
using Content.Shared._DV.Psionics.Events;
using Content.Shared.Hands;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Robust.Shared.Configuration;
using Content.Shared.Tag;


namespace Content.Shared.Shadowkin;

public abstract class SharedEtherealSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EtherealComponent, MapInitEvent>(OnStartup);
        SubscribeLocalEvent<EtherealComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<EtherealComponent, InteractionAttemptEvent>(OnInteractionAttempt);
        SubscribeLocalEvent<EtherealComponent, UseAttemptEvent>(OnUseAttempt);
        SubscribeLocalEvent<EtherealComponent, BeforeThrowEvent>(OnBeforeThrow);
        SubscribeLocalEvent<EtherealComponent, ThrowAttemptEvent>(OnThrowAttempt);
        SubscribeLocalEvent<EtherealComponent, PickupAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<EtherealComponent, PsionicPowerUseAttemptEvent>(OnAttemptPowerUse);
        SubscribeLocalEvent<EtherealComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<EtherealComponent, ShotAttemptedEvent>(OnShootAttempt);
        SubscribeLocalEvent<EtherealComponent, PsionicMindBrokenEvent>(OnMindbreak);
        SubscribeLocalEvent<EtherealComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    public virtual void OnStartup(EntityUid uid, EtherealComponent component, MapInitEvent args)
    {
        if (!TryComp<FixturesComponent>(uid, out var fixtures))
            return;

        var fixture = fixtures.Fixtures.First();

        component.OldMobMask = fixture.Value.CollisionMask;
        component.OldMobLayer = fixture.Value.CollisionLayer;

        if (_cfg.GetCVar(PirateVars.EtherealPassThrough))
        {
            _physics.SetCollisionMask(uid, fixture.Key, fixture.Value, (int) CollisionGroup.GhostImpassable, fixtures);
            _physics.SetCollisionLayer(uid, fixture.Key, fixture.Value, 0, fixtures);

            if (_tag.RemoveTag(uid, "DoorBumpOpener"))
                component.HasDoorBumpTag = true;

            return;
        }

        _physics.SetCollisionMask(uid, fixture.Key, fixture.Value, (int) CollisionGroup.FlyingMobMask, fixtures);
        _physics.SetCollisionLayer(uid, fixture.Key, fixture.Value, (int) CollisionGroup.FlyingMobLayer, fixtures);
    }

    public virtual void OnShutdown(EntityUid uid, EtherealComponent component, ComponentShutdown args)
    {
        // DarkSwap: undo the pacification we applied whenever the shadow realm ends
        // for any reason (toggle off, mindbreak, death, etc.). This must not be gated
        // behind the fixtures check below - the ethereal state may end without fixtures.
        if (component.RemovePacifiedOnEnd)
            RemComp<PacifiedComponent>(uid);

        if (!TryComp<FixturesComponent>(uid, out var fixtures))
            return;

        var fixture = fixtures.Fixtures.First();

        _physics.SetCollisionMask(uid, fixture.Key, fixture.Value, component.OldMobMask, fixtures);
        _physics.SetCollisionLayer(uid, fixture.Key, fixture.Value, component.OldMobLayer, fixtures);

        if (_cfg.GetCVar(PirateVars.EtherealPassThrough))
            if (component.HasDoorBumpTag)
                _tag.AddTag(uid, "DoorBumpOpener");
    }

    private void OnMindbreak(EntityUid uid, EtherealComponent component, ref PsionicMindBrokenEvent args)
    {
        SpawnAtPosition("ShadowkinShadow", Transform(uid).Coordinates);
        SpawnAtPosition("EffectFlashShadowkinDarkSwapOff", Transform(uid).Coordinates);
        RemComp(uid, component);
    }

    private void OnMobStateChanged(EntityUid uid, EtherealComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Critical
            || args.NewMobState == MobState.Dead)
        {
            SpawnAtPosition("ShadowkinShadow", Transform(uid).Coordinates);
            SpawnAtPosition("EffectFlashShadowkinDarkSwapOff", Transform(uid).Coordinates);
            RemComp(uid, component);
        }
    }

    private void OnShootAttempt(Entity<EtherealComponent> ent, ref ShotAttemptedEvent args)
    {
        args.Cancel();
    }

    private void OnAttackAttempt(EntityUid uid, EtherealComponent component, AttackAttemptEvent args)
    {
        if (HasComp<EtherealComponent>(args.Target))
            return;

        args.Cancel();
    }

    private void OnBeforeThrow(Entity<EtherealComponent> ent, ref BeforeThrowEvent args)
    {
        var thrownItem = args.ItemUid;

        // Raise an AttemptPacifiedThrow event and rely on other systems to check
        // whether the candidate item is OK to throw:
        var ev = new AttemptPacifiedThrowEvent(thrownItem, ent);
        RaiseLocalEvent(thrownItem, ref ev);
        if (!ev.Cancelled)
            return;

        args.Cancelled = true;
    }

    private void OnInteractionAttempt(EntityUid uid, EtherealComponent component, InteractionAttemptEvent args)
    {
        if (!HasComp<TransformComponent>(args.Target)
            || HasComp<EtherealComponent>(args.Target))
            return;
        args.Cancelled = true;
        if (_gameTiming.InPrediction)
            return;

        _popup.PopupEntity(Loc.GetString("ethereal-pickup-fail"), args.Target.Value, uid);
    }

    private void OnUseAttempt(EntityUid uid, EtherealComponent component, UseAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnThrowAttempt(EntityUid uid, EtherealComponent component, ThrowAttemptEvent args)
    {
        // Block throwing at the action blocker level (CanThrow), so the player never even initiates a throw.
        args.Cancel();
    }

    private void OnPickupAttempt(EntityUid uid, EtherealComponent component, PickupAttemptEvent args)
    {
        // Block picking up items while intangible (CanPickup).
        args.Cancel();
    }

    private void OnAttemptPowerUse(EntityUid uid, EtherealComponent component, ref PsionicPowerUseAttemptEvent args)
    {
        args.CanUsePower = false;
    }
}
