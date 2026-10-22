// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Events;
using Content.Shared.Clumsy;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Content.Shared.PowerCell.Components;
using Content.Shared.Tools.Components;
using Content.Shared.Weapons.Melee.Events;
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
        SubscribeLocalEvent<WelderComponent, MeleeHitEvent>(OnWelderMeleeHit);
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
            !IsFuelTank(target) || !IsRestrictedWelderUser(ent.Owner, args.User))
            return;

        _popup.PopupEntity(Loc.GetString("novice-protection-fuel-tank-ignition"), args.User, args.User);
        args.Handled = true;
    }

    private void OnWelderMeleeHit(Entity<WelderComponent> ent, ref MeleeHitEvent args)
    {
        // TankSafe only protects refuelling: lit welders still deal heat damage in melee.
        if (args.Handled || !args.IsHit || !args.HitEntities.Any(IsFuelTank) ||
            !IsRestrictedWelderUser(ent.Owner, args.User))
            return;

        _popup.PopupEntity(Loc.GetString("novice-protection-fuel-tank-ignition"), args.User, args.User);
        args.Handled = true;
    }

    private bool IsFuelTank(EntityUid target)
    {
        return TryComp<ReagentTankComponent>(target, out var tank) && tank.TankType == ReagentTankType.Fuel;
    }

    private bool IsRestrictedWelderUser(EntityUid welder, EntityUid user)
    {
        return TryComp<ItemToggleComponent>(welder, out var toggle) && toggle.Activated &&
            TryComp<ActorComponent>(user, out var actor) && IsNovice(actor);
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
