using Content.Shared.ActionBlocker;
using Content.Shared.Alert;
using Content.Shared.Atmos.Rotting;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Movement.Events;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Pirate.Shared.TerrorSpider.EntitySystems;

public sealed class WrapSystem : EntitySystem
{
    private const string SharpComponentName = "Sharp";

    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WrapActionEvent>(OnWrapAttempt);
        SubscribeLocalEvent<HumanoidAppearanceComponent, WrapDoAfterEvent>(OnWrap);
        SubscribeLocalEvent<WrappedComponent, UnWrapAlertEvent>(OnAlertUnwrap);
        SubscribeLocalEvent<WrapEntityHolderComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<WrapEntityHolderComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<WrapEntityHolderComponent, UnwrapDoAfterEvent>(OnUnwrap);
        SubscribeLocalEvent<WrapEntityHolderComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WrappedComponent, IsRottingEvent>(OnRotting);
        SubscribeLocalEvent<WrappedComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
    }

    private static void OnRotting(Entity<WrappedComponent> ent, ref IsRottingEvent args)
    {
        args.Handled = true;
    }

    private static void OnUpdateCanMove(Entity<WrappedComponent> ent, ref UpdateCanMoveEvent args)
    {
        args.Cancel();
    }

    private void OnInteractUsing(Entity<WrapEntityHolderComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasSharp(args.Used))
            return;

        args.Handled = true;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.UnWrapItemTime, new UnwrapDoAfterEvent(), args.Target, args.Target, args.Used)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            BreakOnHandChange = true,
            BreakOnDropItem = true,
        });
    }

    private void OnInteractHand(Entity<WrapEntityHolderComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.UnWrapHandTime, new UnwrapDoAfterEvent(), args.Target, args.Target)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            BreakOnHandChange = true,
            BreakOnDropItem = true,
        });
    }

    private void OnAlertUnwrap(Entity<WrappedComponent> ent, ref UnWrapAlertEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (ent.Comp.Holder == null
            || Deleted(ent.Comp.Holder)
            || !TryComp<WrapEntityHolderComponent>(ent.Comp.Holder.Value, out var holder))
        {
            RemComp<WrappedComponent>(ent.Owner);
            _alerts.ClearAlert(ent.Owner, args.AlertId);
            return;
        }

        var activeItem = _hands.GetActiveItem(ent.Owner);
        var time = activeItem == null || !HasSharp(activeItem.Value)
            ? holder.UnWrapHandTime
            : holder.UnWrapItemTime;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, ent.Owner, time, new UnwrapDoAfterEvent(), ent.Comp.Holder.Value, ent.Comp.Holder.Value)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            BreakOnHandChange = true,
            BreakOnDropItem = true,
        });
    }

    private void OnStartup(Entity<WrapEntityHolderComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.Container = _container.EnsureContainer<Container>(ent.Owner, ent.Comp.ContainerId);
    }

    private void OnUnwrap(Entity<WrapEntityHolderComponent> ent, ref UnwrapDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;

        if (ent.Comp.Hold is not { } held || Deleted(held))
            return;

        if (_container.TryGetContainingContainer(held, out var container))
            _container.Remove(held, container, true, true);

        RemComp<WrappedComponent>(held);
        _blocker.UpdateCanMove(held);
        _alerts.ClearAlert(held, ent.Comp.WrappedAlert);
        ent.Comp.Hold = null;
        Dirty(ent);
        PredictedQueueDel(ent.Owner);
    }

    private void OnWrap(Entity<HumanoidAppearanceComponent> ent, ref WrapDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || !_timing.IsFirstTimePredicted || HasComp<WrappedComponent>(ent.Owner))
            return;

        var wrapped = EnsureComp<WrappedComponent>(ent.Owner);
        _blocker.UpdateCanMove(ent.Owner);

        var holder = PredictedSpawnAttachedTo(args.WrapContainerId, Transform(ent.Owner).Coordinates);
        wrapped.Holder = holder;
        Dirty(ent.Owner, wrapped);

        if (_net.IsServer && TryComp<WrapEntityHolderComponent>(holder, out var holderComp))
        {
            if (holderComp.Container == null || !_container.Insert(ent.Owner, holderComp.Container))
            {
                RemComp<WrappedComponent>(ent.Owner);
                _blocker.UpdateCanMove(ent.Owner);
                PredictedQueueDel(holder);
                return;
            }

            _alerts.ShowAlert(ent.Owner, holderComp.WrappedAlert);
            holderComp.Hold = ent.Owner;
            Dirty(holder, holderComp);
        }

        args.Handled = true;
    }

    private void OnWrapAttempt(WrapActionEvent args)
    {
        if (args.Handled || HasComp<WrappedComponent>(args.Target))
            return;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.Performer, args.WrapTime, new WrapDoAfterEvent(args.WrapContainerId), args.Target, args.Target)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = false,
        });

        args.Handled = true;
    }

    private bool HasSharp(EntityUid uid)
    {
        // Pirate/Starlight: local sharpness is a component, not a TagPrototype.
        return EntityManager.ComponentFactory.TryGetRegistration(SharpComponentName, out var registration) &&
               EntityManager.HasComponent(uid, registration.Type);
    }
}
