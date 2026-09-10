using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._White.Actions;
using Content.Shared._White.Other;
using Content.Shared._White.Xenomorphs.Acid.Components;
using Content.Shared.Chemistry;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._White.Xenomorphs.Acid;

public abstract class SharedXenomorphAcidSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PlasmaCostActionSystem _plasmaCost = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenomorphAcidComponent, AcidActionEvent>(OnXenomorphAcidActionEvent);
        SubscribeLocalEvent<AcidCorrodingComponent, GettingPickedUpAttemptEvent>(OnCorrodingPickupAttempt);
        SubscribeLocalEvent<AcidCorrodingComponent, ComponentShutdown>(OnCorrodingShutdown);
        SubscribeLocalEvent<AcidCorrodingComponent, ReactionEntityEvent>(OnAcidReaction);
    }

    private void OnCorrodingPickupAttempt(EntityUid uid, AcidCorrodingComponent component, GettingPickedUpAttemptEvent args)
    {
        args.Cancel();
        if (args.ShowPopup)
            _popup.PopupEntity(Loc.GetString("xenomorphs-acid-cant-pickup"), uid, args.User, type: PopupType.SmallCaution);
    }

    private void OnCorrodingShutdown(EntityUid uid, AcidCorrodingComponent component, ComponentShutdown args)
    {
        if (_net.IsClient)
            return;

        if (!TerminatingOrDeleted(component.Acid))
            QueueDel(component.Acid);
    }

    private void OnAcidReaction(EntityUid uid, AcidCorrodingComponent component, ref ReactionEntityEvent args)
    {
        // Fire extinguisher (Water) / space cleaner spray hit via vapor Touch/Eyes.
        if (args.Method is not (ReactionMethod.Touch or ReactionMethod.Eyes))
            return;

        if (args.Reagent.ID is not ("Water" or "SpaceCleaner"))
            return;

        CleanAcid(uid, component);
    }

    /// <summary>
    /// Removes acid overlay and stops corrosion without destroying the target.
    /// </summary>
    protected void CleanAcid(EntityUid uid, AcidCorrodingComponent component)
    {
        if (_net.IsClient)
            return;

        if (!TerminatingOrDeleted(component.Acid))
            QueueDel(component.Acid);

        RemComp<AcidCorrodingComponent>(uid);
        _popup.PopupEntity(Loc.GetString("xenomorphs-acid-cleaned"), uid);
    }

    private void OnXenomorphAcidActionEvent(EntityUid uid, XenomorphAcidComponent component, AcidActionEvent args)
    {
        if (args.Handled)
            return;

        TryComp<PlasmaCostActionComponent>(args.Action, out var plasmaCost);
        var plasmaCostValue = plasmaCost?.PlasmaCost ?? FixedPoint2.Zero;

        if (plasmaCostValue > FixedPoint2.Zero && !_plasmaCost.HasEnoughPlasma(uid, plasmaCostValue))
        {
            _popup.PopupEntity(Loc.GetString("xenomorphs-acid-not-enough-plasma"), uid, uid, type: PopupType.SmallCaution);
            return;
        }

        if (!CanCorrode(args.Target, uid, popup: true))
            return;

        if (plasmaCostValue > FixedPoint2.Zero)
            _plasmaCost.DeductPlasma(uid, plasmaCostValue);

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("xenomorphs-acid-apply", ("target", args.Target)), uid, uid, type: PopupType.Small);

        if (_net.IsClient)
            return;

        var isStructure = IsStructureTarget(args.Target);
        var lifetime = isStructure ? component.StructureAcidLifeTime : component.AcidLifeTime;

        var acid = SpawnAttachedTo(component.AcidId, args.Target.ToCoordinates());
        var acidCorroding = new AcidCorrodingComponent
        {
            Acid = acid,
            AcidExpiresAt = Timing.CurTime + lifetime,
            NextDamageAt = Timing.CurTime + TimeSpan.FromSeconds(1),
            DamagePerSecond = component.DamagePerSecond,
            AshPrototype = component.AshPrototype,
            // Items / corpses melt to ash. Structures take DoT until destroyed (RMC-style).
            DissolveToAsh = !isStructure,
        };
        AddComp(args.Target, acidCorroding);
    }

    protected bool IsStructureTarget(EntityUid target)
    {
        return HasComp<StructureComponent>(target) || Transform(target).Anchored;
    }

    protected bool CanCorrode(EntityUid target, EntityUid user, bool popup)
    {
        if (HasComp<AcidCorrodingComponent>(target))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("xenomorphs-acid-already-corroding", ("target", target)), user, user, type: PopupType.SmallCaution);
            return false;
        }

        if (TryComp<MobStateComponent>(target, out var mobState))
        {
            if (mobState.CurrentState != MobState.Dead)
            {
                if (popup)
                    _popup.PopupEntity(Loc.GetString("xenomorphs-acid-alive"), user, user, type: PopupType.SmallCaution);
                return false;
            }

            return true;
        }

        // RMC-style: walls, doors, windows, machines, and loose items.
        if (IsStructureTarget(target))
        {
            if (!HasComp<DamageableComponent>(target))
            {
                if (popup)
                    _popup.PopupEntity(Loc.GetString("xenomorphs-acid-not-corrodible", ("target", target)), user, user, type: PopupType.SmallCaution);
                return false;
            }

            return true;
        }

        if (!HasComp<ItemComponent>(target))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("xenomorphs-acid-not-corrodible", ("target", target)), user, user, type: PopupType.SmallCaution);
            return false;
        }

        return true;
    }
}
