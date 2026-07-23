// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Roudenn <romabond091@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Pirate.Common.Access;
using Content.Pirate.Common.Cyberdeck.Components;
using Content.Shared.Access.Components;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Utility;

namespace Content.Pirate.Shared.Cyberdeck;

public abstract partial class SharedCyberdeckSystem
{
    private void InitializeHacking()
    {
        SubscribeLocalEvent<CyberdeckUserComponent, CyberdeckHackActionEvent>(OnStartHacking);
        SubscribeLocalEvent<CyberdeckHackableComponent, CyberdeckHackDoAfterEvent>(OnHacked);
        SubscribeLocalEvent<AccessReaderComponent, CyberdeckHackDeviceEvent>(OnAccessHacked);

        SubscribeLocalEvent<CyberdeckHackableComponent, StationAiLightEvent>(OnAiHacking,
            before: new[] { typeof(SharedStationAiSystem) });
        SubscribeLocalEvent<CyberdeckHackableComponent, StationAiBoltEvent>(OnAiHacking,
            before: new[] { typeof(SharedStationAiSystem) });
        SubscribeLocalEvent<CyberdeckHackableComponent, StationAiEmergencyAccessEvent>(OnAiHacking,
            before: new[] { typeof(SharedStationAiSystem) });
        SubscribeLocalEvent<CyberdeckHackableComponent, StationAiElectrifiedEvent>(OnAiHacking,
            before: new[] { typeof(SharedStationAiSystem) });

        SubscribeLocalEvent<CyberdeckSiliconTargetComponent, BeforeCyberdeckHackEvent>(BeforeSiliconHacked);
        SubscribeLocalEvent<CyberdeckSiliconTargetComponent, AfterCyberdeckHackEvent>(AfterSiliconHacked);
    }

    private void OnStartHacking(Entity<CyberdeckUserComponent> ent, ref CyberdeckHackActionEvent args)
    {
        var (uid, component) = ent;

        if (args.Handled || args.Target == uid)
            return;

        args.Handled = true;
        EntityUid? target = null;

        // Containers take priority so clicking a borg hacks its power cell.
        if (_containerQuery.TryComp(args.Target, out var containerComp))
        {
            foreach (var container in _container.GetAllContainers(args.Target, containerComp))
            {
                var contained = container.ContainedEntities.FirstOrNull(_hackableQuery.HasComp);
                if (contained == null)
                    continue;

                target = contained.Value;
                break;
            }
        }

        if (target == null && _handsQuery.TryComp(args.Target, out var handsComp))
        {
            target = _hands.EnumerateHeld((args.Target, handsComp))
                .FirstOrNull(_hackableQuery.HasComp);
        }

        if (_hackableQuery.HasComp(args.Target))
            target = args.Target;

        if (target is not { } hackTarget || !_hackableQuery.TryComp(hackTarget, out var hackable))
            return;

        if (_hands.GetActiveItem(uid) != null)
        {
            Popup.PopupClient(Loc.GetString("cyberdeck-needs-free-hand"), uid, uid);
            return;
        }

        if (component.ProviderEntity is { } provider
            && !CheckCharges(uid, provider, hackable.Cost, hackTarget))
            return;

        var before = new BeforeCyberdeckHackEvent(args.Target, hackTarget, TimeSpan.Zero, false);
        RaiseLocalEvent(hackTarget, ref before);
        if (args.Target != hackTarget)
            RaiseLocalEvent(args.Target, ref before);

        var doAfter = new DoAfterArgs(
            EntityManager,
            uid,
            hackable.HackingTime + before.PenaltyTime,
            new CyberdeckHackDoAfterEvent(),
            hackTarget,
            args.Target,
            component.ProviderEntity,
            uid)
        {
            BlockDuplicate = true,
            BreakOnDamage = true,
            BreakOnWeightlessMove = false,
            DistanceThreshold = 20f,
            Broadcast = false,
            Hidden = true,
            RequireCanInteract = false,
            ColorOverride = Color.Aquamarine,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        var message = Loc.GetString("cyberdeck-start-hacking",
            ("target", Identity.Entity(hackTarget, EntityManager, uid)));
        Popup.PopupClient(message, uid, uid);
        _audio.PlayLocal(component.UserHackingSound, uid, uid);

        var after = new AfterCyberdeckHackEvent(args.Target, hackTarget, false);
        RaiseLocalEvent(hackTarget, ref after);
        if (args.Target != hackTarget)
            RaiseLocalEvent(args.Target, ref after);
    }

    private void OnHacked(Entity<CyberdeckHackableComponent> ent, ref CyberdeckHackDoAfterEvent args)
    {
        if (args.Cancelled
            || args.Handled
            || ent.Owner != args.Args.EventTarget
            || !TryHackDevice(args.User, ent.Owner))
            return;

        args.Handled = true;

        var hack = new CyberdeckHackDeviceEvent(args.User);
        RaiseLocalEvent(ent.Owner, ref hack);

        if (hack.Refund)
        {
            if (_cyberdeckUserQuery.TryComp(args.User, out var user))
                RefundCharges((args.User, user), ent.Comp.Cost);
            return;
        }

        if (ent.Comp.AfterHackingEffect != null && _net.IsServer)
            SpawnAtPosition(ent.Comp.AfterHackingEffect, Transform(ent).Coordinates);
    }

    private void OnAccessHacked(Entity<AccessReaderComponent> ent, ref CyberdeckHackDeviceEvent args)
    {
        var ignore = EnsureComp<IgnoreAccessComponent>(ent);
        if (ignore.Ignore.Add(args.User))
            Dirty(ent.Owner, ignore);
    }

    private void OnAiHacking<T>(Entity<CyberdeckHackableComponent> target, ref T args)
        where T : BaseStationAiAction
    {
        if (!_cyberdeckUserQuery.TryComp(args.User, out var user) || !user.InProjection)
            return;

        args.Cancelled = !TryHackDevice(args.User, target);
    }

    private static void BeforeSiliconHacked(
        Entity<CyberdeckSiliconTargetComponent> ent,
        ref BeforeCyberdeckHackEvent args)
    {
        if (args.Handled)
            return;

        args.PenaltyTime += ent.Comp.PenaltyTime;
        args.Handled = true;
    }

    private void AfterSiliconHacked(
        Entity<CyberdeckSiliconTargetComponent> ent,
        ref AfterCyberdeckHackEvent args)
    {
        if (args.Handled)
            return;

        _audio.PlayGlobal(ent.Comp.VictimHackedSound, ent.Owner);
        Popup.PopupEntity(
            Loc.GetString("cyberdeck-player-get-hacked"),
            ent.Owner,
            ent.Owner,
            PopupType.LargeCaution);
        args.Handled = true;
    }
}
