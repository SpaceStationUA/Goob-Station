// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Server.RelayedDeathrattle;
using Content.Goobstation.Shared.CrewMonitoring;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Whitelist;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Robust.Shared.Prototypes;
using Content.Shared.IdentityManagement;
using Content.Server.Popups;
using Content.Shared.Popups;

namespace Content.Goobstation.Server.CrewMonitoring;

public sealed partial class CrewMonitorScanningSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    [Dependency] private SharedSubdermalImplantSystem _implantSystem = default!;
    [Dependency] private PopupSystem _popup = default!;

    [Dependency] private IPrototypeManager _prototypes = default!;

    private static readonly EntProtoId CommandTrackerImplant = "CommandTrackingImplant";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CrewMonitorScanningComponent, AfterInteractEvent>(OnScanAttempt);
        SubscribeLocalEvent<CrewMonitorScanningComponent, CrewMonitorScanningDoAfterEvent>(OnScanComplete);
    }

    private void OnScanAttempt(EntityUid uid, CrewMonitorScanningComponent comp, AfterInteractEvent args)
    {
        if (args.Target == null || !args.CanReach || !HasComp<HumanoidAppearanceComponent>(args.Target))
            return;

        var userName = Identity.Entity(args.User, EntityManager);
        _popup.PopupEntity(Loc.GetString("injector-component-injecting-user"), args.Target.Value, args.User);
        if (args.User != args.Target.Value)
            _popup.PopupEntity(Loc.GetString("implanter-component-implanting-target", ("user", userName)), args.User, args.Target.Value, PopupType.LargeCaution);

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, comp.DoAfterTime, new CrewMonitorScanningDoAfterEvent(), uid, args.Target, uid) { NeedHand = true, BreakOnMove = true });
    }

    private void OnScanComplete(EntityUid uid, CrewMonitorScanningComponent comp, CrewMonitorScanningDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;
        var name = Identity.Name(target, EntityManager, args.User);
        var implantName = _prototypes.Index(CommandTrackerImplant).Name;
        if (TryComp<ImplantedComponent>(target, out var implanted) &&
            implanted.ImplantContainer.ContainedEntities.Any(id => MetaData(id).EntityPrototype?.ID == CommandTrackerImplant.Id))
        {
            _popup.PopupEntity(Loc.GetString("implanter-component-implant-already", ("implant", implantName), ("target", name)), target, args.User);
            return;
        }

        if (_whitelist.IsWhitelistFail(comp.Whitelist, target) || !TryInstallTracker(target))
        {
            _popup.PopupEntity(Loc.GetString("implanter-component-implant-failed", ("implant", implantName), ("target", name)), target, args.User);
            return;
        }

        if (comp.ApplyDeathrattle)
            EnsureComp<RelayedDeathrattleComponent>(target).Target = uid;

        _popup.PopupEntity(Loc.GetString("crew-monitoring-command-tracker-installed", ("target", name)), target, args.User);
    }

    private bool TryInstallTracker(EntityUid target)
    {
        if (_implantSystem.AddImplant(target, CommandTrackerImplant) is not { } tracker)
            return false;

        if (Comp<SubdermalImplantComponent>(tracker).ImplantedEntity == target)
            return true;

        QueueDel(tracker);
        return false;
    }
}
