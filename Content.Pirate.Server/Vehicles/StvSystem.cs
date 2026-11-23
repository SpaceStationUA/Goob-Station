// SPDX-License-Identifier: AGPL-3.0-or-later
// Far Horizons STV, adapted to Pirate's vehicle controls and item slots.

using System.Numerics;
using Content.Goobstation.Shared.Vehicles;
using Content.Pirate.Shared.Vehicles;
using Content.Shared.Actions;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.CCVar;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Content.Shared.FixedPoint;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Lock;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Pirate.Server.Vehicles;

public sealed class StvSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private UnpoweredFlashlightSystem _flashlight = default!;
    [Dependency] private LockSystem _lock = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private SharedVehicleSystem _vehicles = default!;

    private static readonly EntProtoId IgnitionAction = "ActionVehicleIgnition";
    private static readonly EntProtoId HeadlightAction = "ActionStvHeadlight";
    private static readonly EntProtoId SirenAction = "ActionVehicleToggleSecuritySiren";
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    private static readonly SoundSpecifier Startup = new SoundPathSpecifier("/Audio/Effects/Vehicle/vehiclestartup.ogg",
        AudioParams.Default.WithVolume(-3));
    private static readonly SoundSpecifier Impact = new SoundCollectionSpecifier("MetalThud");
    private static readonly TimeSpan FuelInterval = TimeSpan.FromSeconds(2);

    public override void Initialize()
    {
        SubscribeLocalEvent<StvComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<StvComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<StvComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<StvComponent, EntInsertedIntoContainerMessage>(OnInsert);
        SubscribeLocalEvent<StvComponent, EntRemovedFromContainerMessage>(OnRemove);
        SubscribeLocalEvent<StvComponent, StvControlActionEvent>(OnControl);
        SubscribeLocalEvent<StvComponent, BreakageEventArgs>(OnBroken);
        SubscribeLocalEvent<StvComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<StvComponent, StartCollideEvent>(OnCollide);
    }

    private void OnInit(Entity<StvComponent> ent, ref MapInitEvent args) => ApplyParts(ent);

    private void OnStrapped(Entity<StvComponent> ent, ref StrappedEvent args)
    {
        var rider = args.Buckle.Owner;
        _actions.AddAction(rider, ref ent.Comp.IgnitionAction, IgnitionAction, ent);
        UpdateLightsActions(ent, rider);
    }

    private void OnUnstrapped(Entity<StvComponent> ent, ref UnstrappedEvent args)
    {
        _actions.RemoveAction(ent.Comp.IgnitionAction);
        _actions.RemoveAction(ent.Comp.HeadlightAction);
        _actions.RemoveAction(ent.Comp.SirenAction);
        if (!TerminatingOrDeleted(ent))
            StopEngine(ent);
    }

    private void OnInsert(Entity<StvComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID is "engine" or "tires" or "armor")
            ApplyParts(ent);
        if (args.Container.ID is "headlight" or "lightbar" && Comp<VehicleComponent>(ent).Driver is { } rider)
            UpdateLightsActions(ent, rider);
    }

    private void OnRemove(Entity<StvComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (TerminatingOrDeleted(ent) || args.Container.ID is not ("key_slot" or "engine" or "tires" or "armor" or "headlight" or "lightbar"))
            return;

        switch (args.Container.ID)
        {
            case "key_slot":
            case "engine":
                StopEngine(ent);
                break;
            case "headlight":
                _flashlight.SetLight(args.Entity, false, quiet: true);
                break;
            case "lightbar":
                _toggle.TryDeactivate(args.Entity, showPopup: false);
                break;
        }
        if (args.Container.ID is "engine" or "tires" or "armor")
            ApplyParts(ent);
        if (args.Container.ID is "headlight" or "lightbar" && Comp<VehicleComponent>(ent).Driver is { } rider)
            UpdateLightsActions(ent, rider);
    }

    private void UpdateLightsActions(Entity<StvComponent> ent, EntityUid rider)
    {
        if (_slots.GetItemOrNull(ent, "headlight") != null)
            _actions.AddAction(rider, ref ent.Comp.HeadlightAction, HeadlightAction, ent);
        else
            _actions.RemoveAction(ent.Comp.HeadlightAction);
        if (_slots.GetItemOrNull(ent, "lightbar") != null)
            _actions.AddAction(rider, ref ent.Comp.SirenAction, SirenAction, ent);
        else
            _actions.RemoveAction(ent.Comp.SirenAction);
    }

    private void ApplyParts(Entity<StvComponent> ent)
    {
        TryComp<MovementSpeedModifierComponent>(_slots.GetItemOrNull(ent, "tires"), out var tires);
        TryComp<MovementSpeedModifierComponent>(_slots.GetItemOrNull(ent, "engine"), out var engine);
        var acceleration = tires?.BaseAcceleration ?? 0.5f;
        var friction = (tires?.BaseFriction ?? 20f) / Math.Max(0.01f, _cfg.GetCVar(CCVars.TileFrictionModifier));
        _movement.ChangeBaseSpeed(ent, engine?.BaseWalkSpeed ?? 0, engine?.BaseSprintSpeed ?? 0, acceleration);
        _movement.ChangeBaseFriction(ent, friction, friction, acceleration);
        _movement.RefreshFrictionModifiers(ent);
        TryComp<DamageableComponent>(_slots.GetItemOrNull(ent, "armor"), out var armor);
        _damage.SetDamageModifierSetId(ent.Owner, armor?.DamageModifierSetId);
    }

    private void OnControl(Entity<StvComponent> ent, ref StvControlActionEvent args)
    {
        var vehicle = Comp<VehicleComponent>(ent);
        if (args.Handled || vehicle.Driver != args.Performer)
            return;

        args.Handled = true;
        if (args.Control == StvControl.Ignition)
        {
            if (vehicle.EngineRunning || ent.Comp.IgnitionAt != null)
                StopEngine(ent);
            else if (!CanStart(ent, vehicle))
                _popup.PopupEntity(Loc.GetString("stv-ignition-failed"), ent, args.Performer);
            else
            {
                _audio.PlayPvs(Startup, ent);
                ent.Comp.IgnitionAt = _timing.CurTime + TimeSpan.FromSeconds(2);
            }
            return;
        }

        if (!vehicle.EngineRunning)
        {
            _popup.PopupEntity(Loc.GetString("stv-ignition-off"), ent, args.Performer);
            return;
        }

        if (args.Control == StvControl.Headlight && _slots.GetItemOrNull(ent, "headlight") is { } light)
            _flashlight.TryToggleLight(light, args.Performer);
        if (args.Control == StvControl.Siren && _slots.GetItemOrNull(ent, "lightbar") is { } siren)
            _toggle.Toggle(siren, args.Performer);
    }

    private bool CanStart(Entity<StvComponent> ent, VehicleComponent vehicle)
    {
        return !vehicle.IsBroken && vehicle.Driver != null && _slots.GetItemOrNull(ent, vehicle.KeySlot) != null
            && HasComp<StvEngineComponent>(_slots.GetItemOrNull(ent, "engine"))
            && _solutions.TryGetSolution(ent.Owner, "fueltank", out _, out var fuel)
            && fuel.GetTotalPrototypeQuantity("WeldingFuel") > 0;
    }

    private void StopEngine(Entity<StvComponent> ent)
    {
        ent.Comp.IgnitionAt = null;
        _vehicles.SetIgnition((ent, Comp<VehicleComponent>(ent)), false);
        _actions.SetToggled(ent.Comp.IgnitionAction, false);
        if (_slots.GetItemOrNull(ent, "headlight") is { } light)
            _flashlight.SetLight(light, false, quiet: true);
        if (_slots.GetItemOrNull(ent, "lightbar") is { } siren)
            _toggle.TryDeactivate(siren, showPopup: false);
        if (TryComp<LockComponent>(ent, out var storageLock) && !storageLock.Locked)
            _lock.Lock(ent, null, storageLock);
    }

    public override void Update(float frameTime)
    {
        var vehicles = EntityQueryEnumerator<StvComponent, VehicleComponent>();
        while (vehicles.MoveNext(out var uid, out var stv, out var vehicle))
        {
            if (stv.IgnitionAt is { } start && _timing.CurTime >= start)
            {
                stv.IgnitionAt = null;
                if (!CanStart((uid, stv), vehicle))
                    continue;
                _vehicles.SetIgnition((uid, vehicle), true);
                _actions.SetToggled(stv.IgnitionAction, true);
                _lock.Unlock(uid, null);
                stv.NextFuelDraw = _timing.CurTime;
            }
            if (!vehicle.EngineRunning || _timing.CurTime < stv.NextFuelDraw)
                continue;

            if (!TryComp<StvEngineComponent>(_slots.GetItemOrNull(uid, "engine"), out var engine)
                || !_solutions.TryGetSolution(uid, "fueltank", out var tank, out var fuel)
                || fuel.GetTotalPrototypeQuantity("WeldingFuel") <= 0)
            {
                StopEngine((uid, stv));
                continue;
            }
            _solutions.RemoveReagent(tank.Value, "WeldingFuel", FixedPoint2.New(engine.FuelPerSecond * FuelInterval.TotalSeconds));
            stv.NextFuelDraw += FuelInterval;
        }
    }

    private void OnBroken(Entity<StvComponent> ent, ref BreakageEventArgs args)
    {
        StopEngine(ent);
        _appearance.SetData(ent, StvVisuals.Broken, true);
    }

    private void OnDamageChanged(Entity<StvComponent> ent, ref DamageChangedEvent args)
    {
        if (args.Damageable.TotalDamage == 0)
            _appearance.SetData(ent, StvVisuals.Broken, false);
    }

    private void OnCollide(Entity<StvComponent> ent, ref StartCollideEvent args)
    {
        if (!args.OurFixture.Hard || Comp<VehicleComponent>(ent).Driver is not { } rider || args.OtherEntity == rider)
            return;

        var movement = Comp<MovementSpeedModifierComponent>(ent);
        var threshold = Math.Max(8f, Math.Min(movement.BaseWalkSpeed, movement.BaseSprintSpeed) + 1f);
        var velocity = args.OurBody.LinearVelocity;
        var speed = Math.Abs(Vector2.Dot(velocity - args.OtherBody.LinearVelocity, args.WorldNormal));
        if (speed < threshold)
            return;

        if (args.OtherFixture.Hard)
        {
            _audio.PlayPvs(Impact, ent);
            _buckle.Unbuckle(rider, null);
            _stun.TryKnockdown(rider, TimeSpan.FromSeconds(3));
            _throwing.TryThrow(rider, velocity, speed, playSound: false);
        }
        else if (HasComp<DamageableComponent>(args.OtherEntity))
        {
            var damage = new DamageSpecifier(ProtoMan.Index(Blunt), FixedPoint2.New(Math.Clamp(10f + 5f * speed / threshold, 10f, 20f)));
            _damage.TryChangeDamage(args.OtherEntity, damage, origin: rider);
        }
    }
}
