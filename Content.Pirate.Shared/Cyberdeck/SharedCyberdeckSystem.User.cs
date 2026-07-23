// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Roudenn <romabond091@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Pirate.Common.Cyberdeck.Components;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Hands;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Popups;
using Content.Shared.Pulling.Events;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Throwing;
using Content.Shared.Verbs;

namespace Content.Pirate.Shared.Cyberdeck;

public abstract partial class SharedCyberdeckSystem
{
    private void InitializeUser()
    {
        SubscribeLocalEvent<CyberdeckUserComponent, ComponentStartup>(OnUserStartup);
        SubscribeLocalEvent<CyberdeckUserComponent, ComponentShutdown>(OnUserShutdown);
        SubscribeLocalEvent<CyberdeckUserComponent, AccessibleOverrideEvent>(OnCyberdeckAccessible,
            after: new[] { typeof(SharedStationAiSystem) });
        SubscribeLocalEvent<CyberdeckUserComponent, InRangeOverrideEvent>(OnCyberdeckInRange,
            after: new[] { typeof(SharedStationAiSystem) });
        SubscribeLocalEvent<CyberdeckProjectionComponent, GetVerbsEvent<AlternativeVerb>>(OnProjectionVerbs);
        SubscribeLocalEvent<CyberdeckAiUiProxyComponent, BoundUserInterfaceCheckRangeEvent>(OnProxyBuiCheck);
        SubscribeLocalEvent<CyberdeckAiUiProxyComponent, BoundUserInterfaceMessageAttempt>(OnProxyMessageAttempt);
        SubscribeLocalEvent<CyberdeckAiUiProxyComponent, GetStationAiRadialEvent>(OnProxyGetRadial);
        SubscribeLocalEvent<CyberdeckAiUiProxyComponent, StationAiRadialMessage>(OnProxyRadialMessage);

        SubscribeLocalEvent<CyberdeckUserComponent, InteractionAttemptEvent>(OnInteractionAttempt);
        SubscribeLocalEvent<CyberdeckUserComponent, UseAttemptEvent>(OnUseAttempt);
        SubscribeLocalEvent<CyberdeckUserComponent, PickupAttemptEvent>(OnProjectedAttempt);
        SubscribeLocalEvent<CyberdeckUserComponent, DropAttemptEvent>(OnProjectedAttempt);
        SubscribeLocalEvent<CyberdeckUserComponent, ThrowAttemptEvent>(OnProjectedAttempt);
        SubscribeLocalEvent<CyberdeckUserComponent, AttackAttemptEvent>(OnProjectedAttempt);
        SubscribeLocalEvent<CyberdeckUserComponent, IsEquippingAttemptEvent>(OnProjectedAttempt);
        SubscribeLocalEvent<CyberdeckUserComponent, IsUnequippingAttemptEvent>(OnProjectedAttempt);
        SubscribeLocalEvent<CyberdeckUserComponent, StartPullAttemptEvent>(OnProjectedAttempt);
        SubscribeLocalEvent<CyberdeckUserComponent, PullAttemptEvent>(OnPullAttempt);
    }

    private void OnUserStartup(Entity<CyberdeckUserComponent> ent, ref ComponentStartup args)
    {
        var (uid, component) = ent;
        _actions.AddAction(uid, ref component.HackAction, component.HackActionId);
        _actions.AddAction(uid, ref component.VisionAction, component.VisionActionId);

        if (!TryComp(uid, out BodyComponent? body)
            || !_body.TryGetBodyOrganEntityComps<CyberdeckSourceComponent>((uid, body), out var organs)
            || organs.Count == 0)
            return;

        component.ProviderEntity = organs[0].Owner;
        UpdateProviderChargeState(organs[0].Owner);
        UpdateAlert(ent);
        Dirty(ent);
    }

    private void OnUserShutdown(Entity<CyberdeckUserComponent> ent, ref ComponentShutdown args)
    {
        UpdateAlert(ent, true);
        DetachFromProjection(ent);

        _actions.RemoveAction(ent.Owner, ent.Comp.HackAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.VisionAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.ReturnAction);
        PredictedQueueDel(ent.Comp.ProjectionEntity);
        PredictedQueueDel(ent.Comp.AiUiProxyEntity);
    }

    private void OnProjectionVerbs(Entity<CyberdeckProjectionComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess
            || !HasComp<StationAiHeldComponent>(args.User)
            || ent.Comp.RemoteEntity is not { } remote
            || !_cyberdeckUserQuery.TryComp(remote, out var user)
            || !user.InProjection)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cyberdeck-station-ai-smite-verb"),
            Act = () =>
            {
                if (!_cyberdeckUserQuery.TryComp(remote, out var currentUser) || !currentUser.InProjection)
                    return;

                DetachFromProjection((remote, currentUser));
                _damage.TryChangeDamage(
                    remote,
                    new DamageSpecifier
                    {
                        DamageDict = new Dictionary<string, FixedPoint2> { ["Shock"] = 10 },
                    },
                    targetPart: TargetBodyPart.Head);
                _stun.KnockdownOrStun(remote, TimeSpan.FromSeconds(5), true);

                Popup.PopupClient(
                    Loc.GetString("cyberdeck-player-get-hacked"),
                    remote,
                    remote,
                    PopupType.LargeCaution);
                _audio.PlayGlobal(ent.Comp.CounterHackSound, remote);
            },
            Impact = LogImpact.High,
        });
    }

    private void OnCyberdeckAccessible(Entity<CyberdeckUserComponent> ent, ref AccessibleOverrideEvent args)
    {
        if (!ent.Comp.InProjection || args.User != ent.Owner)
            return;

        args.Accessible = IsAiControlEnabled(args.Target);
        args.Handled = true;
    }

    private void OnCyberdeckInRange(Entity<CyberdeckUserComponent> ent, ref InRangeOverrideEvent args)
    {
        if (!ent.Comp.InProjection || args.User != ent.Owner)
            return;

        args.InRange = IsAiControlEnabled(args.Target);
        args.Handled = true;
    }

    private void OnInteractionAttempt(Entity<CyberdeckUserComponent> ent, ref InteractionAttemptEvent args)
    {
        if (ent.Comp.InProjection
            && (args.Target is not { } target
                || target != ent.Comp.AiUiProxyEntity && !IsAiControlEnabled(target)))
            args.Cancelled = true;
    }

    private void OnProxyBuiCheck(
        Entity<CyberdeckAiUiProxyComponent> ent,
        ref BoundUserInterfaceCheckRangeEvent args)
    {
        args.Result = BoundUserInterfaceRangeResult.Fail;

        if (args.UiKey.Equals(AiUi.Key)
            && TryGetProxyTarget(ent, args.Actor.Owner, out _))
            args.Result = BoundUserInterfaceRangeResult.Pass;
    }

    private void OnProxyMessageAttempt(
        Entity<CyberdeckAiUiProxyComponent> ent,
        ref BoundUserInterfaceMessageAttempt args)
    {
        if (!args.UiKey.Equals(AiUi.Key)
            || !TryGetProxyTarget(ent, args.Actor, out _))
            args.Cancel();
    }

    private void OnProxyGetRadial(
        Entity<CyberdeckAiUiProxyComponent> ent,
        ref GetStationAiRadialEvent args)
    {
        if (ent.Comp.RemoteEntity is not { } remote
            || !TryGetProxyTarget(ent, remote, out var target))
            return;

        RaiseLocalEvent(target, ref args);
    }

    private void OnProxyRadialMessage(
        Entity<CyberdeckAiUiProxyComponent> ent,
        ref StationAiRadialMessage args)
    {
        if (!TryGetProxyTarget(ent, args.Actor, out var target))
            return;

        args.Event.User = args.Actor;
        RaiseLocalEvent(target, (object) args.Event);
    }

    private bool TryGetProxyTarget(
        Entity<CyberdeckAiUiProxyComponent> proxy,
        EntityUid actor,
        out EntityUid target)
    {
        target = default;

        if (proxy.Comp.RemoteEntity != actor
            || !_cyberdeckUserQuery.TryComp(actor, out var user)
            || !user.InProjection
            || user.AiUiProxyEntity != proxy.Owner
            || user.ProjectionEntity is not { } projection
            || TerminatingOrDeleted(projection)
            || !TryComp(projection, out CyberdeckProjectionComponent? projectionComponent)
            || projectionComponent.RemoteEntity != actor
            || proxy.Comp.TargetEntity is not { } targetEntity
            || TerminatingOrDeleted(targetEntity)
            || !_aiWhitelistQuery.TryComp(targetEntity, out var whitelist)
            || !whitelist.Enabled
            || !_hackableQuery.HasComp(targetEntity)
            || !_ui.HasUi(targetEntity, AiUi.Key))
            return false;

        var projectionGrid = Transform(projection).GridUid;
        if (projectionGrid == null || Transform(targetEntity).GridUid != projectionGrid)
            return false;

        target = targetEntity;
        return true;
    }

    private void OnUseAttempt(Entity<CyberdeckUserComponent> ent, ref UseAttemptEvent args)
    {
        if (ent.Comp.InProjection && !IsAiControlEnabled(args.Used))
            args.Cancel();
    }

    private bool IsAiControlEnabled(EntityUid target)
    {
        return _aiWhitelistQuery.TryComp(target, out var whitelist) && whitelist.Enabled;
    }

    private static void OnProjectedAttempt(
        EntityUid uid,
        CyberdeckUserComponent component,
        CancellableEntityEventArgs args)
    {
        if (component.InProjection)
            args.Cancel();
    }

    private static void OnPullAttempt(
        EntityUid uid,
        CyberdeckUserComponent component,
        PullAttemptEvent args)
    {
        if (component.InProjection && args.PullerUid == uid)
            args.Cancelled = true;
    }
}
