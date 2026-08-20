// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Defibrillator;
using Content.Server.DoAfter;
using Content.Server.Electrocution;
using Content.Server.Medical;
using Content.Server.Popups;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Hands;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Medical;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.PowerCell;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.Defibrillator;

/// <summary>
/// Handles belt-defibrillator shock paddles: using them on a target triggers the parent
/// belt's zap logic (charge, cooldown, sounds), and they snap back into the belt when
/// dropped or when the holder moves out of range.
/// </summary>
public sealed partial class DefibrillatorPaddlesSystem : EntitySystem
{
    [Dependency] private readonly DefibrillatorSystem _defibrillator = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly ElectrocutionSystem _electrocution = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DefibrillatorPaddlesComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DefibrillatorPaddlesComponent, EntGotInsertedIntoContainerMessage>(OnGotInsertedIntoContainer);
        SubscribeLocalEvent<DefibrillatorPaddlesComponent, EntGotRemovedFromContainerMessage>(OnGotRemovedFromContainer);
        SubscribeLocalEvent<DefibrillatorPaddlesComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<DefibrillatorPaddlesComponent, DroppedEvent>(OnDropped);
        SubscribeLocalEvent<DefibrillatorPaddlesComponent, GotUnequippedHandEvent>(OnUnequipped);
        SubscribeLocalEvent<DefibrillatorEmagComponent, DefibrillatorEmagChannelDoAfterEvent>(OnEmagChannelStep);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_accumulator < 1f)
        {
            _accumulator += frameTime;
            return;
        }

        _accumulator = 0;

        // Periodically check range: paddles held by someone too far from the belt snap back.
        var query = EntityQueryEnumerator<DefibrillatorPaddlesComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var paddles, out var xform))
        {
            if (paddles.Belt is not { } belt || !TryComp<TransformComponent>(belt, out var beltXform))
                continue;

            // Only snap back when actually held by a mob (i.e. we moved with the holder).
            if (!HasComp<MobStateComponent>(xform.ParentUid))
                continue;

            // Don't snap back while the paddles are still inside the belt's own slot.
            if (xform.ParentUid == belt)
                continue;

            var distance = (_transform.GetWorldPosition(xform) - _transform.GetWorldPosition(beltXform)).Length();
            if (distance <= paddles.SnapBackRange)
                continue;

            _popup.PopupEntity(Loc.GetString("defibrillator-paddles-snap-back"), uid);
            SnapBack(uid, paddles);
        }
    }

    private void OnMapInit(Entity<DefibrillatorPaddlesComponent> ent, ref MapInitEvent args)
    {
        // Paddles spawned inside a belt defib remember their belt.
        var parent = Transform(ent.Owner).ParentUid;
        if (HasComp<DefibrillatorComponent>(parent))
            ent.Comp.Belt = parent;
    }

    private void OnGotInsertedIntoContainer(Entity<DefibrillatorPaddlesComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (HasComp<DefibrillatorComponent>(args.Container.Owner))
            ent.Comp.Belt = args.Container.Owner;
    }

    private void OnGotRemovedFromContainer(Entity<DefibrillatorPaddlesComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        // Keep the original belt reference while the paddles are in hand and being used.
        if (HasComp<DefibrillatorComponent>(args.Container.Owner))
            ent.Comp.Belt = args.Container.Owner;
    }

    private void OnAfterInteract(Entity<DefibrillatorPaddlesComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target)
            return;

        // Only usable on living targets, like the main defibrillator.
        if (!HasComp<MobStateComponent>(target))
            return;

        if (ent.Comp.Belt is not { } belt)
        {
            _popup.PopupEntity(Loc.GetString("defibrillator-paddles-no-belt"), ent.Owner, args.User);
            args.Handled = true;
            return;
        }

        // Emagged belt: paddles can be used offensively on living targets. First zap needs a
        // short prep, then shocks chain every ~0.2s, draining the battery until it runs dry
        // or the user moves away / drops the paddles.
        if (TryComp<DefibrillatorEmagComponent>(belt, out var emag) && emag.SafetyDisabled &&
            TryComp<MobStateComponent>(target, out var mobState) && _mobState.IsAlive(target, mobState))
        {
            args.Handled = StartEmagChannel(ent.Owner, belt, emag, target, args.User);
            return;
        }

        // Reuse the belt's defibrillator logic: charge check, cooldown, do-after, sounds.
        args.Handled = _defibrillator.TryStartZap(belt, target, args.User);
    }

    /// <summary>
    /// Starts the emagged offensive channel: a short prep do-after, then the first shock
    /// knocks the target down and follow-up shocks are chained until the battery runs dry
    /// or the channel is interrupted.
    /// </summary>
    private bool StartEmagChannel(EntityUid paddles, EntityUid belt, DefibrillatorEmagComponent emag, EntityUid target, EntityUid user)
    {
        // Must be wielding the paddles in both hands, like SS13.
        if (!TryComp<WieldableComponent>(paddles, out var wieldable) || !wieldable.Wielded)
        {
            _popup.PopupEntity(Loc.GetString("defibrillator-emag-need-wield"), paddles, user);
            return true;
        }

        // The belt needs enough charge for at least one shock (shows a popup if not).
        if (!_powerCell.HasCharge(belt, emag.ChannelChargeCost, user))
            return true;

        _popup.PopupEntity(Loc.GetString("defibrillator-emag-channel-prep", ("target", target)), paddles, user);

        return _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, emag.PrepDuration,
            new DefibrillatorEmagChannelDoAfterEvent(), belt, target, paddles)
        {
            NeedHand = true,
            BreakOnMove = true,
            MultiplyDelay = false, // Goobstation
        });
    }

    /// <summary>
    /// One step of the emagged offensive channel: drains charge, shocks the target and
    /// chains the next shock after <see cref="DefibrillatorEmagComponent.ChannelInterval"/>.
    /// </summary>
    private void OnEmagChannelStep(Entity<DefibrillatorEmagComponent> belt, ref DefibrillatorEmagChannelDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;

        if (args.Target is not { } target || args.Used is not { } paddles)
            return;

        // The paddles that started this channel must still belong to this belt.
        if (!TryComp<DefibrillatorPaddlesComponent>(paddles, out var paddlesComp) || paddlesComp.Belt != belt.Owner)
            return;

        // Only while the safety is off and the target is still alive.
        if (!belt.Comp.SafetyDisabled)
            return;

        if (!TryComp<MobStateComponent>(target, out var mobState) || !_mobState.IsAlive(target, mobState))
            return;

        // Drain charge for this shock; stops the loop if the battery runs out.
        if (!_powerCell.TryUseCharge(belt.Owner, belt.Comp.ChannelChargeCost, args.User))
            return;

        _audio.PlayPvs(belt.Comp.ZapSound, belt.Owner);
        _electrocution.TryDoElectrocution(target, null, belt.Comp.ChannelDamage, belt.Comp.WritheDuration, true,
            ignoreInsulation: true);
        _popup.PopupEntity(Loc.GetString("defibrillator-emag-zap", ("defib", belt.Owner), ("target", target)),
            belt.Owner, args.User);

        // Chain the next shock; the loop ends when the user moves, drops the paddles, the
        // target moves or dies, or the battery runs dry.
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, belt.Comp.ChannelInterval,
            new DefibrillatorEmagChannelDoAfterEvent(), belt.Owner, target, paddles)
        {
            NeedHand = true,
            BreakOnMove = true,
            MultiplyDelay = false, // Goobstation
        });
    }

    private void OnDropped(Entity<DefibrillatorPaddlesComponent> ent, ref DroppedEvent args)
    {
        SnapBack(ent.Owner, ent.Comp);
    }

    private void OnUnequipped(Entity<DefibrillatorPaddlesComponent> ent, ref GotUnequippedHandEvent args)
    {
        SnapBack(ent.Owner, ent.Comp);
    }

    private void SnapBack(EntityUid uid, DefibrillatorPaddlesComponent paddles)
    {
        if (TerminatingOrDeleted(uid))
            return;

        if (paddles.Belt is not { } belt || TerminatingOrDeleted(belt))
            return;

        if (TryComp<MetaDataComponent>(uid, out var meta) &&
            (meta.Flags & MetaDataFlags.InContainer) != 0 &&
            _containers.TryGetContainingContainer((uid, Transform(uid), meta), out var oldContainer))
        {
            _containers.Remove((uid, Transform(uid), meta), oldContainer, reparent: false);
        }

        // Try to insert back into the belt's paddles slot. If the belt has no free
        // paddles slot (shouldn't happen), just leave them where they are.
        if (TryComp<ItemSlotsComponent>(belt, out var slots))
        {
            foreach (var slot in slots.Slots.Values)
            {
                if (slot.HasItem)
                    continue;

                if (_itemSlots.CanInsert(belt, uid, user: null, slot))
                {
                    _itemSlots.TryInsert(belt, slot, uid, user: null);
                    return;
                }
            }
        }
    }
}
