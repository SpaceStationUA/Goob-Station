// SPDX-FileCopyrightText: 2025 Tyranex <bobthezombie4@gmail.com>
// SPDX-License-Identifier: MIT

using Content.Goobstation.Maths.FixedPoint;
using Content.Server.Administration.Logs;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Store.Systems;
using Content.Shared._Pirate.MalfAI;
using Content.Shared.APC;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Store.Components;
using Content.Shared.Verbs;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.MalfAI;

/// <summary>Drains an APC for CPU and restores its original breaker state after the outage.</summary>
public sealed class MalfAiApcSiphonSystem : EntitySystem
{
    [Dependency] private readonly StoreSystem _store = default!;
    [Dependency] private readonly ApcSystem _apc = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ApcComponent, ApcSiphonCpuMessage>(OnSiphonMessage);
        SubscribeLocalEvent<ApcComponent, ApcStartSiphonEvent>(OnStartSiphon);
        SubscribeLocalEvent<ApcComponent, GetVerbsEvent<Verb>>(OnVerbs);
        SubscribeLocalEvent<MalfAiApcSiphonedComponent, ApcToggleMainBreakerAttemptEvent>(OnBreakerAttempt);
        SubscribeLocalEvent<MalfAiApcSiphonedComponent, InteractHandEvent>(OnInteract);
    }

    private void OnSiphonMessage(EntityUid uid, ApcComponent comp, ApcSiphonCpuMessage args)
        => TrySiphon(uid, args.Actor);

    private void OnStartSiphon(EntityUid uid, ApcComponent comp, ref ApcStartSiphonEvent args)
        => TrySiphon(uid, args.SiphonedBy);

    private void OnVerbs(EntityUid uid, ApcComponent comp, GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !HasComp<MalfAiMarkerComponent>(args.User) ||
            !HasComp<StationAiHeldComponent>(args.User) || HasComp<MalfAiApcSiphonedComponent>(uid))
            return;

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("malfai-apc-siphon-verb", ("amount", _cfg.GetCVar(CCVars.MalfAiSiphonCpuAmount))),
            Act = () => TrySiphon(uid, args.User),
        });
    }

    public bool TrySiphon(EntityUid uid, EntityUid user)
    {
        if (!HasComp<MalfAiMarkerComponent>(user) || !HasComp<StationAiHeldComponent>(user) ||
            HasComp<MalfAiApcSiphonedComponent>(uid) ||
            !TryComp<ApcComponent>(uid, out var apc) ||
            !TryComp<PowerNetworkBatteryComponent>(uid, out var battery) ||
            !TryComp<StoreComponent>(user, out var store) ||
            !_interaction.InRangeUnobstructed(user, uid))
            return false;

        var amount = _cfg.GetCVar(CCVars.MalfAiSiphonCpuAmount);
        if (!_store.TryAddCurrency(new() { { "CPU", FixedPoint2.New(amount) } }, user, store))
            return false;

        var siphoned = EnsureComp<MalfAiApcSiphonedComponent>(uid);
        siphoned.OriginalBreakerState = apc.MainBreakerEnabled;
        Dirty(uid, siphoned);
        var emagged = EnsureComp<EmaggedComponent>(uid);
        var wasEmagged = (emagged.EmagType & EmagType.Interaction) != 0;
        emagged.EmagType |= EmagType.Interaction;
        Dirty(uid, emagged);
        apc.MainBreakerEnabled = false;
        battery.CanDischarge = false;
        _apc.UpdateApcState(uid, apc, battery);
        _apc.UpdateUIState(uid, apc, battery);

        Timer.Spawn(TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.MalfAiSiphonDurationSeconds)), () =>
        {
            if (!Exists(uid) || !TryComp<MalfAiApcSiphonedComponent>(uid, out var current) || current != siphoned ||
                !TryComp<ApcComponent>(uid, out var restoredApc) ||
                !TryComp<PowerNetworkBatteryComponent>(uid, out var restoredBattery))
                return;

            restoredApc.MainBreakerEnabled = current.OriginalBreakerState;
            restoredBattery.CanDischarge = current.OriginalBreakerState;
            RemComp<MalfAiApcSiphonedComponent>(uid);
            // Pirate: do not erase an emag applied before the siphon or other emag flags.
            if (!wasEmagged && TryComp<EmaggedComponent>(uid, out var restoredEmag))
            {
                restoredEmag.EmagType &= ~EmagType.Interaction;
                if (restoredEmag.EmagType == EmagType.None)
                    RemComp<EmaggedComponent>(uid);
                else
                    Dirty(uid, restoredEmag);
            }
            _apc.UpdateApcState(uid, restoredApc, restoredBattery);
            _apc.UpdateUIState(uid, restoredApc, restoredBattery);
            _popup.PopupEntity(Loc.GetString("malfai-apc-restore"), uid);
        });

        _adminLogger.Add(LogType.Action, LogImpact.High,
            $"Malf AI {ToPrettyString(user)} siphoned APC {ToPrettyString(uid)} for {amount} CPU");
        return true;
    }

    private void OnBreakerAttempt(EntityUid uid, MalfAiApcSiphonedComponent comp, ref ApcToggleMainBreakerAttemptEvent args)
        => args.Cancelled = true;

    private void OnInteract(EntityUid uid, MalfAiApcSiphonedComponent comp, InteractHandEvent args)
    {
        _popup.PopupCursor(Loc.GetString("malfai-apc-unresponsive"), args.User, PopupType.Medium);
        args.Handled = true;
    }
}
