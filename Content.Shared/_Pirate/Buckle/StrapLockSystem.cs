// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.EntityEffects;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Throwing;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared._Pirate.Buckle;

/// <summary>
/// Handles holding and locking a mob to a crucifix. Range checks are driven only by movement events.
/// </summary>
public sealed class StrapLockSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly SharedEntityEffectsSystem _effects = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedVirtualItemSystem _virtualItems = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StrapLockComponent, StrapAttemptEvent>(OnStrapAttempt);
        SubscribeLocalEvent<StrapLockComponent, UnstrapAttemptEvent>(OnUnstrapAttempt);
        SubscribeLocalEvent<StrapLockComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<StrapLockComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<StrapLockComponent, MoveEvent>(OnStrapMoved);
        SubscribeLocalEvent<StrapLockComponent, ComponentShutdown>(OnStrapShutdown);

        SubscribeLocalEvent<StrapLockHeldComponent, ComponentShutdown>(OnHeldShutdown);

        SubscribeLocalEvent<StrapLockHoldingComponent, VirtualItemDeletedEvent>(OnVirtualItemDeleted);
        SubscribeLocalEvent<StrapLockHoldingComponent, MoveEvent>(OnHolderMoved);
        SubscribeLocalEvent<StrapLockHoldingComponent, ComponentShutdown>(OnHoldingShutdown);

        SubscribeLocalEvent<StrapLockedComponent, InteractionAttemptEvent>(OnLockedInteractionAttempt);
        SubscribeLocalEvent<StrapLockedComponent, AttackAttemptEvent>(OnLockedAttackAttempt);
        SubscribeLocalEvent<StrapLockedComponent, ThrowAttemptEvent>(OnLockedThrowAttempt);
        SubscribeLocalEvent<StrapLockedComponent, PullAttemptEvent>(OnLockedPullAttempt);
        SubscribeLocalEvent<StrapLockedComponent, DownAttemptEvent>(OnLockedDownAttempt);
        SubscribeLocalEvent<StrapLockedComponent, ThrowPushbackAttemptEvent>(OnLockedPushbackAttempt);
    }

    private void OnStrapAttempt(Entity<StrapLockComponent> ent, ref StrapAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (args.User is not { } user)
        {
            args.Cancelled = true;
            return;
        }

        if (user == args.Buckle.Owner)
        {
            args.Cancelled = true;
            if (args.Popup)
                _popup.PopupEntity(Loc.GetString("strap-lock-self", ("strap", ent.Owner)), ent, user);
            return;
        }

        if (_hands.CountFreeHands(user) >= ent.Comp.RequiredHands)
            return;

        args.Cancelled = true;
        if (args.Popup)
        {
            _popup.PopupEntity(Loc.GetString("strap-lock-need-hands",
                ("hands", ent.Comp.RequiredHands),
                ("strap", ent.Owner)), ent, user);
        }
    }

    private void OnUnstrapAttempt(Entity<StrapLockComponent> ent, ref UnstrapAttemptEvent args)
    {
        if (args.Cancelled || !ent.Comp.Locked)
            return;

        args.Cancelled = true;
        if (!args.Popup || args.User is not { } user)
            return;

        var buckled = Identity.Entity(args.Buckle, EntityManager);
        var key = user == args.Buckle.Owner ? "you" : "others";
        _popup.PopupEntity(Loc.GetString($"strap-lock-unstrap-locked-{key}",
            ("buckled", buckled),
            ("strap", ent.Owner)), ent, user);
    }

    private void OnStrapped(Entity<StrapLockComponent> ent, ref StrappedEvent args)
    {
        var target = args.Buckle.Owner;
        if (args.User is not { } user)
        {
            _buckle.Unbuckle((target, args.Buckle.Comp), null);
            return;
        }

        ClearVirtualItems(ent);
        for (var i = 0; i < ent.Comp.RequiredHands; i++)
        {
            if (_virtualItems.TrySpawnVirtualItemInHand(target, user, out var virtualItem))
            {
                ent.Comp.VirtualItems.Add(virtualItem.Value);
                continue;
            }

            _buckle.Unbuckle((target, args.Buckle.Comp), null);
            ClearVirtualItems(ent);
            return;
        }

        Dirty(ent);
        StartHolding(ent, user, target);
    }

    private void OnUnstrapped(Entity<StrapLockComponent> ent, ref UnstrappedEvent args)
    {
        UnlockStrap(ent);
        ClearVirtualItems(ent);
        RemComp<StrapLockedComponent>(args.Buckle);

        if (!TryComp<StrapLockHeldComponent>(args.Buckle, out var held))
            return;

        if (args.User == held.Holder)
            held.Unsafe = false;

        RemoveHeld(args.Buckle);
    }

    private void OnStrapMoved(Entity<StrapLockComponent> ent, ref MoveEvent args)
    {
        if (!TryComp<StrapComponent>(ent, out var strap))
            return;

        foreach (var target in new List<EntityUid>(strap.BuckledEntities))
        {
            if (!TryComp<StrapLockHeldComponent>(target, out var held) ||
                !TryComp<StrapLockHoldingComponent>(held.Holder, out var holding) ||
                holding.Strap != ent.Owner)
            {
                continue;
            }

            CheckHolding((held.Holder, holding));
        }
    }

    private void OnHolderMoved(Entity<StrapLockHoldingComponent> ent, ref MoveEvent args)
    {
        CheckHolding(ent);
    }

    private void OnStrapShutdown(Entity<StrapLockComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<StrapComponent>(ent, out var strap))
            return;

        foreach (var target in new List<EntityUid>(strap.BuckledEntities))
        {
            RemCompDeferred<StrapLockedComponent>(target);
            if (!TryComp<StrapLockHeldComponent>(target, out var held))
                continue;

            RemoveHolding(held.Holder);
            RemoveHeld(target);
        }
    }

    private void OnHeldShutdown(Entity<StrapLockHeldComponent> ent, ref ComponentShutdown args)
    {
        if (_net.IsServer && !TerminatingOrDeleted(ent.Comp.Holder))
            RemoveHolding(ent.Comp.Holder);
    }

    private void OnVirtualItemDeleted(Entity<StrapLockHoldingComponent> ent, ref VirtualItemDeletedEvent args)
    {
        if (_net.IsServer && args.BlockingEntity == ent.Comp.Buckled)
            StopHolding(ent);
    }

    private void OnHoldingShutdown(Entity<StrapLockHoldingComponent> ent, ref ComponentShutdown args)
    {
        if (_net.IsServer && ent.Comp.Buckled.IsValid())
            StopHolding(ent);
    }

    private static void OnLockedInteractionAttempt(Entity<StrapLockedComponent> ent, ref InteractionAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private static void OnLockedAttackAttempt(Entity<StrapLockedComponent> ent, ref AttackAttemptEvent args)
    {
        args.Cancel();
    }

    private static void OnLockedThrowAttempt(Entity<StrapLockedComponent> ent, ref ThrowAttemptEvent args)
    {
        args.Cancel();
    }

    private static void OnLockedPullAttempt(Entity<StrapLockedComponent> ent, ref PullAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private static void OnLockedDownAttempt(Entity<StrapLockedComponent> ent, ref DownAttemptEvent args)
    {
        args.Cancel();
    }

    private static void OnLockedPushbackAttempt(Entity<StrapLockedComponent> ent, ref ThrowPushbackAttemptEvent args)
    {
        args.Cancel();
    }

    private void CheckHolding(Entity<StrapLockHoldingComponent> ent)
    {
        var target = ent.Comp.Buckled;
        if (TerminatingOrDeleted(target))
        {
            ent.Comp.Buckled = EntityUid.Invalid;
            Dirty(ent);
            RemCompDeferred(ent.Owner, ent.Comp);
            return;
        }

        if (TerminatingOrDeleted(ent.Comp.Strap))
        {
            StopHolding(ent);
            return;
        }

        if (!_buckle.IsBuckled(target))
        {
            RemoveHeld(target);
            ent.Comp.Buckled = EntityUid.Invalid;
            Dirty(ent);
            RemCompDeferred(ent.Owner, ent.Comp);
            return;
        }

        var holderCoordinates = _transform.GetMapCoordinates(ent.Owner);
        var strapCoordinates = _transform.GetMapCoordinates(ent.Comp.Strap);
        if (!holderCoordinates.InRange(strapCoordinates, ent.Comp.Range))
            StopHolding(ent);
    }

    private void StartHolding(Entity<StrapLockComponent> ent, EntityUid user, EntityUid target)
    {
        var dropEvent = new DropHandItemsEvent();
        RaiseLocalEvent(target, ref dropEvent);
        EnsureComp<StrapLockedComponent>(target);

        var userIdentity = Identity.Entity(user, EntityManager);
        var targetIdentity = Identity.Entity(target, EntityManager);
        _popup.PopupPredicted(
            Loc.GetString("strap-lock-raising-you", ("buckled", targetIdentity), ("strap", ent.Owner)),
            Loc.GetString("strap-lock-raising-others",
                ("buckled", targetIdentity),
                ("strap", ent.Owner),
                ("user", userIdentity)),
            target,
            user);

        var holding = EnsureComp<StrapLockHoldingComponent>(user);
        holding.Strap = ent;
        holding.Buckled = target;
        holding.DropEffect = ent.Comp.DropEffect;
        Dirty(user, holding);

        var held = EnsureComp<StrapLockHeldComponent>(target);
        held.Holder = user;
        held.Unsafe = true;
        Dirty(target, held);
    }

    private void StopHolding(Entity<StrapLockHoldingComponent> ent)
    {
        // StrapLockHeld shutdown owns holder cleanup; this can itself run from Holding shutdown.
        var target = ent.Comp.Buckled;
        if (!target.IsValid())
            return;

        var strap = ent.Comp.Strap;
        var dropEffect = ent.Comp.DropEffect;
        ent.Comp.Buckled = EntityUid.Invalid;
        if (!TerminatingOrDeleted(ent.Owner))
            Dirty(ent);

        ClearVirtualItems(strap);

        if (TerminatingOrDeleted(target))
            return;

        if (CompOrNull<StrapLockComponent>(strap)?.Locked == true)
        {
            RemoveHeld(target);
            return;
        }

        _buckle.Unbuckle((target, null), null);

        if (CompOrNull<StrapLockHeldComponent>(target)?.Unsafe == true)
        {
            if (!TerminatingOrDeleted(ent.Owner))
            {
                var userIdentity = Identity.Entity(ent.Owner, EntityManager);
                var targetIdentity = Identity.Entity(target, EntityManager);
                _popup.PopupPredicted(
                    Loc.GetString("strap-lock-dropped-you", ("buckled", targetIdentity)),
                    Loc.GetString("strap-lock-dropped-others",
                        ("buckled", targetIdentity),
                        ("user", userIdentity)),
                    target,
                    _players.LocalEntity);
            }

            _effects.TryApplyEffect(target, dropEffect);
        }

        RemCompDeferred<StrapLockedComponent>(target);
        RemoveHeld(target);
    }

    private void ClearVirtualItems(EntityUid uid)
    {
        if (!TryComp<StrapLockComponent>(uid, out var component))
            return;

        ClearVirtualItems((uid, component));
    }

    private void ClearVirtualItems(Entity<StrapLockComponent> ent)
    {
        foreach (var item in ent.Comp.VirtualItems)
        {
            PredictedQueueDel(item);
        }

        ent.Comp.VirtualItems.Clear();
        if (!TerminatingOrDeleted(ent.Owner))
            Dirty(ent);
    }

    public void UnlockStrap(Entity<StrapLockComponent> ent)
    {
        if (!ent.Comp.Locked)
            return;

        ent.Comp.Locked = false;
        Dirty(ent);
    }

    public void LockStrap(Entity<StrapLockComponent> ent)
    {
        if (ent.Comp.Locked)
            return;

        ent.Comp.Locked = true;
        Dirty(ent);
        ClearVirtualItems(ent);

        if (!TryComp<StrapComponent>(ent, out var strap))
            return;

        foreach (var target in strap.BuckledEntities)
        {
            if (!TryComp<StrapLockHeldComponent>(target, out var held))
                continue;

            if (TryComp<StrapLockHoldingComponent>(held.Holder, out var holding))
            {
                holding.Buckled = EntityUid.Invalid;
                if (!TerminatingOrDeleted(held.Holder))
                    Dirty(held.Holder, holding);
                RemoveHolding(held.Holder);
            }

            RemoveHeld(target);
        }
    }

    private void RemoveHeld(EntityUid target)
    {
        if (!TryComp<StrapLockHeldComponent>(target, out var held) ||
            held.LifeStage >= ComponentLifeStage.Stopping)
        {
            return;
        }

        RemCompDeferred(target, held);
    }

    private void RemoveHolding(EntityUid holder)
    {
        if (!TryComp<StrapLockHoldingComponent>(holder, out var holding) ||
            holding.LifeStage >= ComponentLifeStage.Stopping)
        {
            return;
        }

        RemCompDeferred(holder, holding);
    }
}
