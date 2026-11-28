// SPDX-FileCopyrightText: Suspicion on Space Station contributors
// SPDX-License-Identifier: AGPL-3.0-only

using System.Linq;
using Content.Server._Pirate.Suspicion.GridMarker;
using Content.Server.Communications;
using Content.Shared._Pirate.Suspicion.RadarOverlay;
using Content.Shared._Pirate.Suspicion;
using Content.Shared._Pirate.Suspicion.SuspicionGameRule;
using Content.Shared._Pirate.Suspicion.SuspicionGameRule.Components;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Player;
using Content.Shared.Ghost;
using Content.Shared.Hands.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Overlays;
using Content.Shared.Popups;
using Content.Server.KillTracking;

namespace Content.Server._Pirate.Suspicion.SuspicionGameRule;

public sealed partial class SuspicionRuleSystem
{
    private void OnGhost(EntityUid ghost, GhostHearingComponent component, PlayerAttachedEvent args)
    {
        if (ActiveRule == null || !HasComp<GhostComponent>(ghost))
            return;

        EnsureComp<ShowCriminalRecordIconsComponent>(ghost);
        SendAllyMarkers(args.Player);
    }

    private void OnMobStateChanged(EntityUid uid, SuspicionPlayerComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState is MobState.Critical or MobState.Dead && _playerManager.TryGetSessionByEntity(uid, out var session))
            RaiseNetworkEvent(new OnRadarOverlayToggledEvent(false), session);

        if (args.NewMobState == MobState.Critical)
        {
            var damageSpec = new DamageSpecifier(_prototypeManager.Index<DamageGroupPrototype>("Genetic"), 1000);
            _damageableSystem.TryChangeDamage(args.Target, damageSpec, origin: args.Origin);
            Log.Debug("Player is critical, applying genetic damage.");
            return;
        }

        if (args.NewMobState != MobState.Dead) // Someone died.
            return;

        DropAllItemsOnEntity(args.Target);

    }

    private bool UpdateDeadRoundEndCondition(Entity<SuspicionRuleComponent> sus)
    {
        var allTraitors = FindAllOfType(SuspicionRole.Traitor);
        var allInnocents = FindAllOfType(SuspicionRole.Innocent);
        var allDetectives = FindAllOfType(SuspicionRole.Detective);
        var allWildcards = FindAllOfType(SuspicionRole.Wildcard);

        if (allInnocents.Count == 0 && allDetectives.Count == 0 && allWildcards.Count == 0)
        {
            sus.Comp.Winner = "pirate-ttt-win-traitors";
            _chatManager.DispatchServerAnnouncement(Loc.GetString(sus.Comp.Winner));
            StopClock();
            sus.Comp.GameState = SuspicionGameState.PostRound;
            _roundEndSystem.EndRound(TimeSpan.FromSeconds(sus.Comp.PostRoundDuration));
            return true;
        }

        if (allTraitors.Count == 0)
        {
            sus.Comp.Winner = "pirate-ttt-win-innocents";
            _chatManager.DispatchServerAnnouncement(Loc.GetString(sus.Comp.Winner));
            StopClock();
            sus.Comp.GameState = SuspicionGameState.PostRound;
            _roundEndSystem.EndRound(TimeSpan.FromSeconds(sus.Comp.PostRoundDuration));
            return true;
        }

        return false;
    }

    private void OnKillReported(Entity<SuspicionPlayerComponent> victim, ref KillReportedEvent ev)
    {
        if (ActiveRule is not { } active || active.Comp.GameState != SuspicionGameState.InProgress)
            return;

        var sus = active.Comp;
        foreach (var traitor in FindAllOfType(SuspicionRole.Traitor))
            AddTcToPlayer(traitor.body, sus.AmountAddedPerKill);

        sus.EndAt += TimeSpan.FromSeconds(_timeAddedPerKill);
        RaiseNetworkEvent(new SuspicionRuleTimerUpdate(sus.EndAt));

        if (ev.Suicide || ev.Primary is not KillPlayerSource player ||
            !TrySusRole(victim, out var role) || role.SubRole != SuspicionSubRole.Jester ||
            !TrySusRole(player.PlayerId, out var attacker) ||
            attacker.Role is not (SuspicionRole.Innocent or SuspicionRole.Detective))
            return;

        sus.Winner = "pirate-ttt-win-jester";
        _chatManager.DispatchServerAnnouncement(Loc.GetString(sus.Winner));
        StopClock();
        sus.GameState = SuspicionGameState.PostRound;
        _roundEndSystem.EndRound(TimeSpan.FromSeconds(sus.PostRoundDuration));
    }

    private void OnShuttleCall(ref CommunicationConsoleCallShuttleAttemptEvent ev)
    {
        if (ActiveRule != null)
            ev.Cancelled = true;
    }

    private void OnExamine(EntityUid uid, SuspicionPlayerComponent component, ref ExaminedEvent args)
    {
        if (!TryComp<MobStateComponent>(args.Examined, out var mobState))
            return;

        if (!_mobState.IsDead(args.Examined, mobState))
            return; // Not a dead body... *yet*.

        var isInRange = args.IsInDetailsRange || component.Revealed;
        // Always show the role if it was already announced in chat.

        if (!isInRange)
        {
            args.PushText(Loc.GetString("pirate-ttt-nearer"), -10);
            return;
        }

        var mind = _mindSystem.GetMind(args.Examined);
        var examinerMind = _mindSystem.GetMind(args.Examiner);

        if (mind == null)
            return;

        if (!_roleSystem.MindHasRole<SuspicionRoleComponent>(mind.Value, out var role))
            return;

        if (role.Value.Comp2.Role == SuspicionRole.Pending)
            return;

        if (examinerMind.HasValue) // I apologize for this being so nested. Can't use early returns with the other stuff below.
        {
            // If the examined is a traitor and the examinor is a detective, we give the detective any TC the traitor had.
            if (role.Value.Comp2.Role == SuspicionRole.Traitor)
            {
                if (_roleSystem.MindHasRole<SuspicionRoleComponent>(examinerMind.Value, out var examinerRole))
                {
                    if (examinerRole.Value.Comp2.Role == SuspicionRole.Detective)
                    {
                        var implantT = GetUplinkImplant(args.Examined);
                        var implantD = GetUplinkImplant(args.Examiner);
                        if (implantT.HasValue && implantD.HasValue)
                        {
                            var tc = implantT.Value.Comp.Balance.Values.Sum(x => x.Int());
                            AddTcToPlayer(args.Examiner, tc);
                            implantT.Value.Comp.Balance.Clear();

                            if (tc >= 0)
                            {
                                if (_playerManager.TryGetSessionByEntity(args.Examiner, out var session))
                                {
                                    var msgFound = Loc.GetString("suspicion-found-tc", ("tc", tc));
                                    _chatManager.ChatMessageToOne(
                                        ChatChannel.Server,
                                        msgFound,
                                        msgFound,
                                        EntityUid.Invalid,
                                        false,
                                        client: session.Channel,
                                        recordReplay: true
                                    );
                                }
                            }
                        }
                    }
                }
            }
        }

        var victimRole = Loc.GetString("pirate-ttt-role-" + (role.Value.Comp2.SubRole?.ToString() ?? role.Value.Comp2.Role.ToString()).ToLowerInvariant());

        args.PushMarkup(Loc.GetString(
                "suspicion-examination",
                ("ent", args.Examined),
                ("col", role.Value.Comp2.Role.GetRoleColor()),
                ("role", victimRole)),
            -10);

        if (!HasComp<HandsComponent>(args.Examiner))
            return;

        if (HasComp<GhostComponent>(args.Examiner))
            return; // Check for admin ghosts

        // Reveal the role in chat
        if (component.Revealed)
            return;

        if (role.Value.Comp2.Role == SuspicionRole.Traitor)
        {
            var allDetectives = FindAllOfType(SuspicionRole.Detective);
            foreach (var det in allDetectives) // Once a Traitor is found, all detectives get 1 TC.
            {
                AddTcToPlayer(det.body, 1, false);
            }
        }

        component.Revealed = true;
        var trans = Comp<TransformComponent>(args.Examined);
        var loc = _transformSystem.GetMapCoordinates(trans);

        var msg = Loc.GetString("suspicion-examination-chat",
            ("finder", args.Examiner),
            ("found", args.Examined),
            ("where", _navMapSystem.GetNearestBeaconString(loc)),
            ("col", role.Value.Comp2.Role.GetRoleColor()),
            ("role", victimRole));
        SendAnnouncement(
            msg
        );
    }
    private void UpdateSpaceWalkDamage()
    {
        var query = EntityQueryEnumerator<SuspicionPlayerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.SpacewalkThreshold <= 0)
                continue;

            var coordinates = _transformSystem.GetMapCoordinates(uid);

            var entities = _entityLookupSystem.GetEntitiesInRange<SuspicionGridMarkerComponent>(coordinates,
                comp.SpacewalkThreshold,
                LookupFlags.Sundries);

            if (entities.Count > 0)
                continue;

            if (comp.LastTookSpacewalkDamage + TimeSpan.FromSeconds(1) > _clock.CurTime)
                continue;

            var damage = new DamageSpecifier(_prototypeManager.Index<DamageGroupPrototype>("Toxin"), 5);
            _damageableSystem.TryChangeDamage(uid, damage);
            comp.LastTookSpacewalkDamage = _clock.CurTime;
            _popupSystem.PopupEntity(Loc.GetString("pirate-ttt-outside"),
                uid,
                uid,
                PopupType.LargeCaution);
        }
    }

    private static readonly int[] TimeWarnings = [240, 180, 120, 60, 30, 10, 5];

    private void UpdateTimer(SuspicionRuleComponent sus, TimeSpan elapsed)
    {
        var remaining = sus.EndAt - Timing.CurTime;
        var previous = (remaining + elapsed).TotalSeconds;

        foreach (var seconds in TimeWarnings)
        {
            if (previous <= seconds || remaining.TotalSeconds > seconds)
                continue;
            _chatManager.DispatchServerAnnouncement(Loc.GetString("pirate-ttt-time-left", ("time", remaining.ToString(@"m\:ss"))));
            break;
        }

        if (remaining > TimeSpan.Zero)
            return;

        sus.Winner = "pirate-ttt-win-innocents";
        _chatManager.DispatchServerAnnouncement(Loc.GetString(sus.Winner));
        StopClock();
        sus.GameState = SuspicionGameState.PostRound;
        _roundEndSystem.EndRound(TimeSpan.FromSeconds(sus.PostRoundDuration));
    }
}
