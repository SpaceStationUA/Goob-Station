// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.CaptureTheFlag;

public sealed partial class CaptureTheFlagRuleSystem
{
    private static readonly EntProtoId RedFlag = "CaptureTheFlagRedFlag";
    private static readonly EntProtoId BlueFlag = "CaptureTheFlagBlueFlag";
    private static readonly Vector2 FlagOffset = new(0f, 0.3f);

    [Dependency] private readonly SharedMapSystem _maps = default!;

    private void InitializeObjectives()
    {
        SubscribeLocalEvent<CaptureTheFlagFlagComponent, GettingPickedUpAttemptEvent>(OnFlagPickupAttempt);
        SubscribeLocalEvent<CaptureTheFlagFlagComponent, ContainerGettingInsertedAttemptEvent>(OnFlagContainerInsert);
        SubscribeLocalEvent<CaptureTheFlagFlagComponent, GotEquippedHandEvent>(OnFlagEquipped);
        SubscribeLocalEvent<CaptureTheFlagFlagComponent, GotUnequippedHandEvent>(OnFlagUnequipped);
        SubscribeLocalEvent<CaptureTheFlagFlagComponent, ThrowItemAttemptEvent>(OnFlagThrowAttempt);
        SubscribeLocalEvent<CaptureTheFlagFlagComponent, InteractHandEvent>(OnFlagInteract);
        SubscribeLocalEvent<CaptureTheFlagFlagComponent, InteractUsingEvent>(OnFlagUsing);
        SubscribeLocalEvent<CaptureTheFlagBaseComponent, InteractUsingEvent>(OnBaseUsing);
        SubscribeLocalEvent<CaptureTheFlagBaseComponent, ExaminedEvent>(OnBaseExamine);
        SubscribeLocalEvent<CaptureTheFlagSupplyComponent, InteractHandEvent>(OnSupplyInteract);
        SubscribeLocalEvent<CaptureTheFlagSupplyComponent, InteractUsingEvent>(OnSupplyUsing);
    }

    private bool TryGetFlagMatch(EntityUid uid, CaptureTheFlagFlagComponent flag,
        [NotNullWhen(true)] out CaptureTheFlagBaseComponent? home,
        [NotNullWhen(true)] out CaptureTheFlagRuleComponent? rule)
    {
        rule = null;
        return TryComp(flag.Home, out home) && home.Flag == uid && TryGetMatch(home.Rule, out rule);
    }

    private void OnFlagPickupAttempt(EntityUid uid, CaptureTheFlagFlagComponent flag, GettingPickedUpAttemptEvent args)
    {
        if (!CanCarryFlag(uid, flag, args.User))
            args.Cancel();
    }

    private void OnFlagContainerInsert(EntityUid uid, CaptureTheFlagFlagComponent flag, ContainerGettingInsertedAttemptEvent args)
    {
        var owner = args.Container.Owner;
        if (!_hands.TryGetHand(owner, args.Container.ID, out _) ||
            !CanCarryFlag(uid, flag, owner))
            args.Cancel();
    }

    private bool CanCarryFlag(EntityUid uid, CaptureTheFlagFlagComponent flag, EntityUid user)
    {
        return TryGetFlagMatch(uid, flag, out var home, out var rule) &&
            TryGetParticipant(user, home.Rule, rule, out var player) && player.Team != home.Team &&
            (flag.Carrier == null || flag.Carrier == user) && CanUse(user, uid);
    }

    private void OnFlagEquipped(EntityUid uid, CaptureTheFlagFlagComponent flag, GotEquippedHandEvent args)
    {
        if (!TryGetFlagMatch(uid, flag, out var home, out _))
            return;

        if (flag.AtHome)
            ChatManager.DispatchServerAnnouncement(Loc.GetString("ctf-flag-stolen", ("team", GetTeamName(home.Team))));

        flag.AtHome = false;
        flag.Carrier = args.User;
        flag.ReturnAt = null;
    }

    private void OnFlagUnequipped(EntityUid uid, CaptureTheFlagFlagComponent flag, GotUnequippedHandEvent args)
    {
        if (flag.Carrier != args.User || !TryGetFlagMatch(uid, flag, out _, out var rule))
            return;

        flag.Carrier = null;
        flag.ReturnAt = Timing.CurTime + rule.DroppedFlagDuration;
    }

    private void OnFlagThrowAttempt(EntityUid uid, CaptureTheFlagFlagComponent flag, ref ThrowItemAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnFlagInteract(EntityUid uid, CaptureTheFlagFlagComponent flag, InteractHandEvent args)
    {
        if (args.Handled || flag.AtHome || flag.Carrier != null ||
            !TryGetFlagMatch(uid, flag, out var home, out var rule) ||
            !TryGetParticipant(args.User, home.Rule, rule, out var player) || player.Team != home.Team ||
            !CanUse(args.User, uid))
            return;

        args.Handled = true;
        ReturnFlag((flag.Home, home));
        ChatManager.DispatchServerAnnouncement(Loc.GetString("ctf-flag-recovered", ("team", GetTeamName(home.Team))));
    }

    private void OnFlagUsing(EntityUid uid, CaptureTheFlagFlagComponent flag, InteractUsingEvent args)
    {
        if (args.Handled || !flag.AtHome || !TryGetFlagMatch(uid, flag, out var home, out _))
            return;

        TryCapture((flag.Home, home), args);
    }

    private void OnBaseUsing(EntityUid uid, CaptureTheFlagBaseComponent home, InteractUsingEvent args)
    {
        if (!args.Handled)
            TryCapture((uid, home), args);
    }

    private void TryCapture(Entity<CaptureTheFlagBaseComponent> home, InteractUsingEvent args)
    {
        if (!TryGetMatch(home.Comp.Rule, out var rule) ||
            !TryGetParticipant(args.User, home.Comp.Rule, rule, out var player) || player.Team != home.Comp.Team ||
            !TryComp<CaptureTheFlagFlagComponent>(args.Used, out var enemyFlag) ||
            enemyFlag.Carrier != args.User || !_hands.IsHolding(args.User, args.Used) ||
            !TryGetFlagMatch(args.Used, enemyFlag, out var enemyHome, out _) ||
            enemyHome.Rule != home.Comp.Rule || enemyHome.Team == home.Comp.Team ||
            !CanUse(args.User, home))
            return;

        args.Handled = true;
        if (!TryComp<CaptureTheFlagFlagComponent>(home.Comp.Flag, out var ownFlag) || !ownFlag.AtHome ||
            TerminatingOrDeleted(home.Comp.Flag) || Transform(home.Comp.Flag).Coordinates != FlagPosition(home))
        {
            _popups.PopupEntity(Loc.GetString("ctf-capture-home-missing"), home, args.User);
            return;
        }

        var (red, blue) = CountPlaying(rule);
        if (home.Comp.Team == CaptureTheFlagTeam.Red ? blue == 0 : red == 0)
        {
            _popups.PopupEntity(Loc.GetString("ctf-capture-no-opponent"), home, args.User);
            return;
        }

        ReturnFlag((enemyFlag.Home, enemyHome));
        home.Comp.Score++;
        ChatManager.DispatchServerAnnouncement(Loc.GetString("ctf-flag-captured",
            ("team", GetTeamName(home.Comp.Team)), ("score", GetScore(rule))));

        var total = home.Comp.Score + enemyHome.Score;
        var tier = total >= 3 ? 3 : 2;
        if (tier > rule.Tier)
        {
            rule.Tier = tier;
            ChatManager.DispatchServerAnnouncement(Loc.GetString("ctf-equipment-unlocked", ("tier", tier)));
        }

        SendScoreboard(rule);
        if (home.Comp.Score >= rule.ScoreToWin)
            Finish(rule);
    }

    private void OnBaseExamine(EntityUid uid, CaptureTheFlagBaseComponent home, ExaminedEvent args)
    {
        if (!TryGetMatch(home.Rule, out var rule) || rule.EndAt is not { } endAt)
            return;

        args.PushMarkup(GetScore(rule));
        args.PushMarkup(Loc.GetString("ctf-examine-time",
            ("seconds", (int) Math.Max(0, Math.Ceiling((endAt - Timing.CurTime).TotalSeconds)))));

        var state = "ctf-flag-status-away";
        if (TryComp<CaptureTheFlagFlagComponent>(home.Flag, out var flag))
        {
            if (flag.AtHome)
                state = "ctf-flag-status-home";
            else if (flag.Carrier == null)
                state = "ctf-flag-status-dropped";
        }

        args.PushMarkup(Loc.GetString(state));
    }

    private void UpdateFlag(EntityUid uid, CaptureTheFlagRuleComponent rule, Entity<CaptureTheFlagBaseComponent> home)
    {
        var flagId = home.Comp.Flag;
        if (!TryComp<CaptureTheFlagFlagComponent>(flagId, out var flag) || TerminatingOrDeleted(flagId))
        {
            AutoReturnFlag(home);
            return;
        }

        var xform = Transform(flagId);
        if (xform.GridUid != rule.Grid || !TryComp<MapGridComponent>(rule.Grid, out var grid) ||
            !_maps.TryGetTileRef(rule.Grid, grid, xform.Coordinates, out var tile) || tile.Tile.IsEmpty ||
            (flag.AtHome && xform.Coordinates != FlagPosition(home)) ||
            (flag.ReturnAt is { } returnAt && Timing.CurTime >= returnAt))
        {
            AutoReturnFlag(home);
            return;
        }

        if (flag.Carrier is { } carrier && !TryGetParticipant(carrier, uid, rule, out _))
            DropFlag(rule, carrier);
    }

    private bool IsCarryingFlag(CaptureTheFlagRuleComponent rule, EntityUid user)
    {
        foreach (var home in rule.Homes.Values)
        {
            if (TryComp<CaptureTheFlagBaseComponent>(home, out var objective) && _hands.IsHolding(user, objective.Flag))
                return true;
        }

        return false;
    }

    private void DropFlag(CaptureTheFlagRuleComponent rule, EntityUid user)
    {
        foreach (var home in rule.Homes.Values)
        {
            if (TryComp<CaptureTheFlagBaseComponent>(home, out var objective) && _hands.IsHolding(user, objective.Flag) &&
                !_hands.TryDrop(user, objective.Flag, checkActionBlocker: false))
                AutoReturnFlag((home, objective));
        }
    }

    private void AutoReturnFlag(Entity<CaptureTheFlagBaseComponent> home)
    {
        ReturnFlag(home);
        ChatManager.DispatchServerAnnouncement(Loc.GetString("ctf-flag-returned", ("team", GetTeamName(home.Comp.Team))));
    }

    private EntityCoordinates FlagPosition(EntityUid home)
    {
        var coordinates = Transform(home).Coordinates;
        return new EntityCoordinates(coordinates.EntityId, coordinates.Position + FlagOffset);
    }

    private void ReturnFlag(Entity<CaptureTheFlagBaseComponent> home)
    {
        if (home.Comp.Flag.IsValid())
            QueueDel(home.Comp.Flag);

        var flag = SpawnAttachedTo(home.Comp.Team == CaptureTheFlagTeam.Red ? RedFlag : BlueFlag, FlagPosition(home));
        home.Comp.Flag = flag;
        Comp<CaptureTheFlagFlagComponent>(flag).Home = home;
    }

    private void OnSupplyInteract(EntityUid uid, CaptureTheFlagSupplyComponent supply, InteractHandEvent args)
    {
        if (!args.Handled)
            args.Handled = TryClaimSupply((uid, supply), args.User);
    }

    private void OnSupplyUsing(EntityUid uid, CaptureTheFlagSupplyComponent supply, InteractUsingEvent args)
    {
        if (!args.Handled)
            args.Handled = TryClaimSupply((uid, supply), args.User);
    }

    private bool TryClaimSupply(Entity<CaptureTheFlagSupplyComponent> supply, EntityUid user)
    {
        if (!TryUseTeamSupply(supply, user, out var rule, out var player))
            return false;

        if (rule.Tier == 1)
        {
            _popups.PopupEntity(Loc.GetString("ctf-equipment-locked"), supply, user);
            return true;
        }

        if (player.ClaimedTier >= rule.Tier)
        {
            _popups.PopupEntity(Loc.GetString("ctf-equipment-claimed"), supply, user);
            return true;
        }

        player.ClaimedTier = rule.Tier;
        var equipment = rule.Tier == 2 ? rule.TierTwoGear : rule.TierThreeGear;
        foreach (var item in equipment)
        {
            var issued = SpawnAttachedTo(item, Transform(user).Coordinates);
            AllowArenaGunUse(issued);
            _hands.PickupOrDrop(user, issued, animate: false);
        }

        _popups.PopupEntity(Loc.GetString("ctf-equipment-issued", ("tier", rule.Tier)), supply, user);
        return true;
    }

    private bool TryUseTeamSupply(Entity<CaptureTheFlagSupplyComponent> supply, EntityUid user,
        [NotNullWhen(true)] out CaptureTheFlagRuleComponent? rule, [NotNullWhen(true)] out CaptureTheFlagPlayer? player)
    {
        player = null;
        return TryGetMatch(supply.Comp.Rule, out rule) && TryGetParticipant(user, supply.Comp.Rule, rule, out player) &&
            player.Team == supply.Comp.Team && CanUse(user, supply);
    }
}
