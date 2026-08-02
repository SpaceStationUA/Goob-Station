using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Explosion.EntitySystems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Physics;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

using Content.Pirate.Shared.Yautja.Components;

namespace Content.Pirate.Shared.Yautja.Systems;

public sealed class SharedYautjaBracerSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStealthSystem _stealth = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<YautjaBracerComponent, ToggleYautjaClawsEvent>(OnToggleClaws);
        SubscribeLocalEvent<YautjaBracerComponent, ToggleYautjaCloakEvent>(OnToggleCloak);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaBracerSelfDestructEvent>(OnSelfDestruct);
        SubscribeLocalEvent<YautjaBracerComponent, GotUnequippedEvent>(OnBracerUnequipped);
        SubscribeLocalEvent<YautjaBracerComponent, ComponentShutdown>(OnBracerShutdown);
        SubscribeLocalEvent<YautjaBracerClawsComponent, ComponentShutdown>(OnClawsShutdown);
        SubscribeLocalEvent<YautjaBracerCloakTrackerComponent, MoveEvent>(OnCloakTrackerMove);
        SubscribeLocalEvent<YautjaCloakPackComponent, GotUnequippedEvent>(OnCloakPackUnequipped);
    }

    private void OnCloakPackUnequipped(Entity<YautjaCloakPackComponent> ent, ref GotUnequippedEvent args)
    {
        if (args.Slot != "back")
            return;

        DecloakUser(args.Equipee);
    }

    private void OnCloakTrackerMove(Entity<YautjaBracerCloakTrackerComponent> ent, ref MoveEvent args)
    {
        if (ent.Comp.Bracer is not { } bracerUid
            || !TryComp(bracerUid, out YautjaBracerComponent? bracer)
            || !bracer.Cloaked)
            return;

        if (!TryComp<StealthComponent>(ent, out var stealth))
            return;

        if (_stealth.GetVisibility(ent, stealth) > stealth.MinVisibility)
            _stealth.SetVisibility(ent, stealth.MinVisibility, stealth);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<YautjaBracerComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            var ent = (uid, comp);

            if (!comp.SelfDestructing || comp.SelfDestructAt == null || now < comp.SelfDestructAt)
                continue;

            switch (comp.SelfDestructPhase)
            {
                case YautjaBracerSelfDestructPhase.Arming:
                    BeginSelfDestructCountdown(ent);
                    break;
                case YautjaBracerSelfDestructPhase.Countdown:
                    Detonate(ent);
                    break;
            }
        }
    }

    private void OnToggleClaws(Entity<YautjaBracerComponent> ent, ref ToggleYautjaClawsEvent args)
    {
        if (args.Handled)
            return;

        var extended = HasExtendedClaws(ent.Comp);

        if (!extended)
        {
            if (!TryExtendClaws(ent, args.Performer))
                return;
        }
        else
        {
            RetractClaws(ent);
        }

        if (args.Action is { } act)
            _actions.SetToggled((act, act), !extended);

        args.Handled = true;
    }

    private void OnToggleCloak(Entity<YautjaBracerComponent> ent, ref ToggleYautjaCloakEvent args)
    {
        if (args.Handled)
            return;

        var user = args.Performer;
        var cloaked = ent.Comp.Cloaked;

        if (!cloaked)
        {
            if (!HasEquippedCloakPack(user))
            {
                _popup.PopupPredicted(Loc.GetString("yautja-bracer-cloak-need-pack"), user, user);
                args.Handled = true;
                return;
            }

            ActivateCloak(ent, user);
        }
        else
        {
            Decloak(ent);
        }

        if (args.Action is { } act)
            _actions.SetToggled((act, act), !cloaked);

        args.Handled = true;
    }

    private bool HasEquippedCloakPack(EntityUid user)
    {
        return _inventory.TryGetSlotEntity(user, "back", out var back)
               && HasComp<YautjaCloakPackComponent>(back);
    }

    private void DecloakUser(EntityUid user)
    {
        var query = EntityQueryEnumerator<YautjaBracerComponent>();
        while (query.MoveNext(out var uid, out var bracer))
        {
            if (!bracer.Cloaked || bracer.CloakUser != user)
                continue;

            Decloak((uid, bracer));
        }
    }

    private void ActivateCloak(Entity<YautjaBracerComponent> ent, EntityUid user)
    {
        RemComp<StealthOnMoveComponent>(user);

        var stealth = EnsureComp<StealthComponent>(user);
        _stealth.SetThermalsImmune(user, false, stealth);
        _stealth.SetVisibility(user, stealth.MinVisibility, stealth);
        EnsureComp<YautjaBracerCloakTrackerComponent>(user).Bracer = ent;

        ent.Comp.Cloaked = true;
        ent.Comp.CloakUser = user;
        Dirty(ent);

        if (_net.IsServer)
            Spawn(ent.Comp.CloakDisappearEffect, Transform(user).Coordinates);

        _audio.PlayPredicted(ent.Comp.CloakOnSound, user, user);
    }

    private void Decloak(Entity<YautjaBracerComponent> ent)
    {
        if (!ent.Comp.Cloaked)
            return;

        if (ent.Comp.CloakUser is { } target && !TerminatingOrDeleted(target))
        {
            RemComp<StealthOnMoveComponent>(target);
            RemComp<YautjaBracerCloakTrackerComponent>(target);
            RemComp<StealthComponent>(target);
            _audio.PlayPredicted(ent.Comp.CloakOffSound, target, target);

            foreach (var (actionUid, _) in _actions.GetActions(target))
            {
                if (!TryComp<InstantActionComponent>(actionUid, out var instant)
                    || instant.Event is not ToggleYautjaCloakEvent)
                {
                    continue;
                }

                _actions.SetToggled(actionUid, false);
            }
        }

        ent.Comp.Cloaked = false;
        ent.Comp.CloakUser = null;
        Dirty(ent);
    }

    private void OnSelfDestruct(Entity<YautjaBracerComponent> ent, ref YautjaBracerSelfDestructEvent args)
    {
        if (args.Handled || ent.Comp.SelfDestructing)
            return;

        var user = args.Performer;
        RetractClaws(ent);

        _audio.PlayPredicted(ent.Comp.SelfDestructDoAfterSound, user, user);
        _popup.PopupPredicted(Loc.GetString("yautja-bracer-self-destruct-started"), user, user);

        if (_net.IsServer)
        {
            var doAfterSound = _audio.ResolveSound(ent.Comp.SelfDestructDoAfterSound);
            ent.Comp.SelfDestructing = true;
            ent.Comp.SelfDestructPhase = YautjaBracerSelfDestructPhase.Arming;
            ent.Comp.SelfDestructUser = user;
            ent.Comp.SelfDestructAction = args.Action;
            ent.Comp.SelfDestructAt = _timing.CurTime + _audio.GetAudioLength(doAfterSound);
            Dirty(ent);
        }

        args.Handled = true;
    }

    private void BeginSelfDestructCountdown(Entity<YautjaBracerComponent> ent)
    {
        var user = ent.Comp.SelfDestructUser;
        if (user is not { } target || TerminatingOrDeleted(target))
        {
            CancelSelfDestruct(ent);
            return;
        }

        ent.Comp.SelfDestructPhase = YautjaBracerSelfDestructPhase.Countdown;
        ent.Comp.SelfDestructAt = _timing.CurTime + ent.Comp.SelfDestructCountdown;
        Dirty(ent);

        _audio.PlayPvs(ent.Comp.SelfDestructCountdownSound, target);
        _popup.PopupEntity(Loc.GetString("yautja-bracer-self-destruct-countdown"), target, target);

        if (ent.Comp.SelfDestructAction is { } action)
            _actions.SetCooldown(action, ent.Comp.SelfDestructCountdown);
    }

    private void OnBracerUnequipped(Entity<YautjaBracerComponent> ent, ref GotUnequippedEvent args)
    {
        Decloak(ent);
        CancelSelfDestruct(ent);
        RetractClaws(ent);
    }

    private void OnBracerShutdown(Entity<YautjaBracerComponent> ent, ref ComponentShutdown args)
    {
        Decloak(ent);
        CancelSelfDestruct(ent);
        RetractClaws(ent);
    }

    private void OnClawsShutdown(Entity<YautjaBracerClawsComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Bracer is not { } bracerUid
            || !TryComp(bracerUid, out YautjaBracerComponent? bracer)
            || bracer.ClawsEntity != ent)
        {
            return;
        }

        bracer.ClawsEntity = null;
        Dirty(bracerUid, bracer);
    }

    private void Detonate(Entity<YautjaBracerComponent> ent)
    {
        ent.Comp.SelfDestructing = false;
        ent.Comp.SelfDestructPhase = YautjaBracerSelfDestructPhase.None;
        ent.Comp.SelfDestructAt = null;

        var user = ent.Comp.SelfDestructUser;
        ent.Comp.SelfDestructUser = null;
        ent.Comp.SelfDestructAction = null;
        Dirty(ent);

        if (user is not { } target || TerminatingOrDeleted(target))
            return;

        RetractClaws(ent);

        var coords = Transform(target).Coordinates;
        var boom = Spawn(ent.Comp.SelfDestructExplosionPrototype, coords);
        _explosion.TriggerExplosive(boom);

        foreach (var item in _inventory.GetHandOrInventoryEntities(target))
            QueueDel(item);

        _body.GibBody(target, true);
    }

    private void CancelSelfDestruct(Entity<YautjaBracerComponent> ent)
    {
        if (!ent.Comp.SelfDestructing)
            return;

        ent.Comp.SelfDestructing = false;
        ent.Comp.SelfDestructPhase = YautjaBracerSelfDestructPhase.None;
        ent.Comp.SelfDestructAt = null;
        ent.Comp.SelfDestructUser = null;
        ent.Comp.SelfDestructAction = null;
        Dirty(ent);
    }

    private bool HasExtendedClaws(YautjaBracerComponent comp) =>
        comp.ClawsEntity is { } uid && !TerminatingOrDeleted(uid);

    private bool TryExtendClaws(Entity<YautjaBracerComponent> ent, EntityUid user)
    {
        if (HasExtendedClaws(ent.Comp))
            return true;

        if (!EnsureFreeHand(user))
        {
            _popup.PopupPredicted(Loc.GetString("yautja-bracer-claws-no-hands"), user, user);
            return false;
        }

        var claws = Spawn(ent.Comp.ClawsPrototype, Transform(user).Coordinates);
        EnsureComp<YautjaBracerClawsComponent>(claws).Bracer = ent;

        if (!_hands.TryPickupAnyHand(user, claws, checkActionBlocker: false))
        {
            _popup.PopupPredicted(Loc.GetString("yautja-bracer-claws-no-hands"), user, user);
            QueueDel(claws);
            return false;
        }

        ent.Comp.ClawsEntity = claws;
        Dirty(ent);

        _audio.PlayPredicted(ent.Comp.ClawsExtendSound, user, user);
        return true;
    }

    private bool EnsureFreeHand(EntityUid user)
    {
        if (!TryComp<HandsComponent>(user, out var hands))
            return false;

        if (_hands.CountFreeHands((user, hands)) > 0)
            return true;

        foreach (var hand in _hands.EnumerateHands(user))
        {
            if (_hands.HandIsEmpty((user, hands), hand))
                continue;

            var held = _hands.GetHeldItem((user, hands), hand);
            if (held != null && HasComp<UnremoveableComponent>(held))
                continue;

            if (_hands.TryDrop((user, hands), hand, checkActionBlocker: false))
                return true;
        }

        return _hands.CountFreeHands((user, hands)) > 0;
    }

    private void RetractClaws(Entity<YautjaBracerComponent> ent)
    {
        if (ent.Comp.ClawsEntity is not { } claws || TerminatingOrDeleted(claws))
        {
            ent.Comp.ClawsEntity = null;
            Dirty(ent);
            return;
        }

        QueueDel(claws);
        ent.Comp.ClawsEntity = null;
        Dirty(ent);
    }
}
