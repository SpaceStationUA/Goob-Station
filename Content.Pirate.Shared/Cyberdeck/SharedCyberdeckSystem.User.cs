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
    }

    private void OnProjectionVerbs(Entity<CyberdeckProjectionComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!HasComp<StationAiHeldComponent>(args.User)
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
                _audio.PlayLocal(ent.Comp.CounterHackSound, ent.Owner, ent.Owner);
                _audio.PlayLocal(ent.Comp.CounterHackSound, args.User, args.User);
            },
            Impact = LogImpact.High,
        });
    }

    private void OnCyberdeckAccessible(Entity<CyberdeckUserComponent> ent, ref AccessibleOverrideEvent args)
    {
        if (!ent.Comp.InProjection || args.User != ent.Owner)
            return;

        args.Accessible = _aiWhitelistQuery.HasComp(args.Target);
        args.Handled = true;
    }

    private void OnCyberdeckInRange(Entity<CyberdeckUserComponent> ent, ref InRangeOverrideEvent args)
    {
        if (!ent.Comp.InProjection || args.User != ent.Owner)
            return;

        args.InRange = _aiWhitelistQuery.HasComp(args.Target);
        args.Handled = true;
    }

    private void OnInteractionAttempt(Entity<CyberdeckUserComponent> ent, ref InteractionAttemptEvent args)
    {
        if (ent.Comp.InProjection
            && (args.Target is not { } target || !_aiWhitelistQuery.HasComp(target)))
            args.Cancelled = true;
    }

    private void OnUseAttempt(Entity<CyberdeckUserComponent> ent, ref UseAttemptEvent args)
    {
        if (ent.Comp.InProjection && !_aiWhitelistQuery.HasComp(args.Used))
            args.Cancel();
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
        if (component.InProjection)
            args.Cancelled = true;
    }
}
