// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Players.PlayTimeTracking;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Events;
using Content.Shared.Clumsy;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Content.Shared.PowerCell.Components;
using Content.Shared.Tools.Components;
using Robust.Shared.Player;

namespace Content.Server._Pirate.Newbies;

/// <summary>
/// Prevents new players from rigging power cells or igniting fuel tanks with welders.
/// </summary>
public sealed class NoviceExplosiveProtectionSystem : EntitySystem
{
    private static readonly TimeSpan RequiredPlaytime = TimeSpan.FromHours(24);

    [Dependency] private readonly PlayTimeTrackingManager _playTime = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActorComponent, SelfBeforeInjectEvent>(OnBeforeInject, after: new[] { typeof(ClumsySystem) });
        SubscribeLocalEvent<WelderComponent, BeforeRangedInteractEvent>(OnBeforeWelderInteract);
    }

    private void OnBeforeInject(Entity<ActorComponent> ent, ref SelfBeforeInjectEvent args)
    {
        if (args.Cancelled || !HasComp<PowerCellComponent>(args.TargetGettingInjected) || !IsNovice(ent.Comp))
            return;

        _popup.PopupEntity(Loc.GetString("novice-protection-power-cell-injection"), ent, ent);
        args.Cancel();
    }

    private void OnBeforeWelderInteract(Entity<WelderComponent> ent, ref BeforeRangedInteractEvent args)
    {
        if (args.Handled || !args.CanReach || ent.Comp.TankSafe ||
            args.Target is not { } target ||
            !TryComp<ReagentTankComponent>(target, out var tank) || tank.TankType != ReagentTankType.Fuel ||
            !TryComp<ItemToggleComponent>(ent, out var toggle) || !toggle.Activated ||
            !TryComp<ActorComponent>(args.User, out var actor) || !IsNovice(actor))
            return;

        _popup.PopupEntity(Loc.GetString("novice-protection-fuel-tank-ignition"), args.User, args.User);
        args.Handled = true;
    }

    private bool IsNovice(ActorComponent actor)
    {
        var session = actor.PlayerSession;
        if (!_playTime.TryGetTrackerTimes(session, out _))
            return true;

        // Include the current round, so the restriction ends as soon as 24 hours are reached.
        _playTime.FlushTracker(session);
        return _playTime.GetOverallPlaytime(session) < RequiredPlaytime;
    }
}
