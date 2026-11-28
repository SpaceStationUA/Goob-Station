// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using Content.Shared._Pirate.CaptureTheFlag;
using Content.Shared.Examine;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.CaptureTheFlag;

public sealed partial class CaptureTheFlagRuleSystem
{
    private const float RelayRadius = 2.5f;
    private const int MaxReserveCharges = 2;
    private static readonly TimeSpan RelayCheckInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RelayCaptureTime = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan RelayChargeTime = TimeSpan.FromSeconds(25);
    private static readonly Color NeutralRelayColor = Color.FromHex("#FFDB8B");
    private static readonly Color RedRelayColor = Color.FromHex("#FF5266");
    private static readonly Color BlueRelayColor = Color.FromHex("#62BAFF");
    private static readonly EntProtoId[] AmmoPacket = ["MagazinePistol"];
    private static readonly EntProtoId[] MedicalPacket = ["Brutepack", "Ointment"];

    [Dependency] private readonly SharedPointLightSystem _relayLight = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private void InitializeRelay()
    {
        SubscribeLocalEvent<CaptureTheFlagRelayComponent, ExaminedEvent>(OnRelayExamine);
        SubscribeLocalEvent<CaptureTheFlagSupplyComponent, ExaminedEvent>(OnTeamSupplyExamine);
        SubscribeLocalEvent<CaptureTheFlagSupplyComponent, GetVerbsEvent<Verb>>(OnReserveVerbs);
    }

    private bool BindRelay(EntityUid uid, CaptureTheFlagRuleComponent rule)
    {
        Entity<CaptureTheFlagRelayComponent>? found = null;
        var relays = AllEntityQuery<CaptureTheFlagRelayComponent, TransformComponent>();
        while (relays.MoveNext(out var relayId, out var relay, out var xform))
        {
            if (xform.GridUid != rule.Grid)
                continue;

            if (found != null || relay.Rule.IsValid())
                return false;

            found = (relayId, relay);
        }

        if (found is not { } entry)
            return false;

        rule.Relay = entry.Owner;
        entry.Comp.Rule = uid;
        return true;
    }

    private void ReleaseRelay(EntityUid uid, CaptureTheFlagRuleComponent rule)
    {
        var relayId = rule.Relay;
        rule.Relay = EntityUid.Invalid;
        rule.RelayState = CaptureTheFlagRelayState.Neutral;
        if (!TryComp<CaptureTheFlagRelayComponent>(relayId, out var relay) || relay.Rule != uid)
            return;

        relay.Rule = EntityUid.Invalid;
        relay.Team = null;
        relay.ClaimingTeam = null;
        relay.CaptureAt = default;
        relay.LastCheck = default;
        relay.ChargeProgress = TimeSpan.Zero;
        relay.Producing = false;
        _relayLight.SetColor(relayId, NeutralRelayColor);
    }

    private void UpdateRelay(EntityUid uid, CaptureTheFlagRuleComponent rule, int red, int blue)
    {
        if (!TryComp<CaptureTheFlagRelayComponent>(rule.Relay, out var relay) || TerminatingOrDeleted(rule.Relay))
            return;

        var now = Timing.CurTime;
        var elapsed = now - relay.LastCheck;
        if (elapsed < RelayCheckInterval)
            return;

        relay.LastCheck = now;
        var relayPosition = Transform(rule.Relay).Coordinates;
        CaptureTheFlagTeam? nearbyTeam = null;
        var contested = false;
        foreach (var player in rule.Players.Values)
        {
            if (!TryGetParticipant(player.Body, uid, rule, out _) ||
                !_transform.InRange(Transform(player.Body).Coordinates, relayPosition, RelayRadius))
                continue;

            if (nearbyTeam is { } nearby && nearby != player.Team)
            {
                contested = true;
                break;
            }

            nearbyTeam = player.Team;
        }

        if (contested || nearbyTeam is not { } challenger || challenger == relay.Team)
        {
            relay.ClaimingTeam = null;
        }
        else if (relay.ClaimingTeam != challenger)
        {
            relay.ClaimingTeam = challenger;
            relay.CaptureAt = now + RelayCaptureTime;
        }
        else if (now >= relay.CaptureAt)
        {
            relay.Team = challenger;
            relay.ClaimingTeam = null;
            relay.ChargeProgress = TimeSpan.Zero;
            relay.Producing = false;
            _relayLight.SetColor(rule.Relay, challenger == CaptureTheFlagTeam.Red ? RedRelayColor : BlueRelayColor);
            ChatManager.DispatchServerAnnouncement(Loc.GetString("ctf-relay-captured", ("team", GetTeamName(challenger))));
        }

        var center = (contested, relay.ClaimingTeam, relay.Team) switch
        {
            (true, _, _) => CaptureTheFlagRelayState.Contested,
            (_, CaptureTheFlagTeam.Red, _) => CaptureTheFlagRelayState.TakingRed,
            (_, CaptureTheFlagTeam.Blue, _) => CaptureTheFlagRelayState.TakingBlue,
            (_, _, CaptureTheFlagTeam.Red) => CaptureTheFlagRelayState.Red,
            (_, _, CaptureTheFlagTeam.Blue) => CaptureTheFlagRelayState.Blue,
            _ => CaptureTheFlagRelayState.Neutral,
        };
        if (rule.RelayState != center)
        {
            rule.RelayState = center;
            SendScoreboard(rule);
        }

        if (red == 0 || blue == 0 || contested || relay.Team is not { } owner ||
            (nearbyTeam is { } occupier && occupier != owner) || !TryGetHome(rule, owner, out var home) ||
            home.ReserveCharges >= MaxReserveCharges)
        {
            relay.Producing = false;
            return;
        }

        if (relay.Producing)
            relay.ChargeProgress += elapsed;
        relay.Producing = true;

        var previousCharges = home.ReserveCharges;
        while (relay.ChargeProgress >= RelayChargeTime && home.ReserveCharges < MaxReserveCharges)
        {
            home.ReserveCharges++;
            relay.ChargeProgress -= RelayChargeTime;
        }

        if (previousCharges != home.ReserveCharges)
            SendScoreboard(rule);

        if (home.ReserveCharges == MaxReserveCharges)
        {
            relay.ChargeProgress = TimeSpan.Zero;
            relay.Producing = false;
        }
    }

    private bool TryGetHome(CaptureTheFlagRuleComponent rule, CaptureTheFlagTeam team,
        [NotNullWhen(true)] out CaptureTheFlagBaseComponent? home)
    {
        home = null;
        return rule.Homes.TryGetValue(team, out var homeId) && TryComp(homeId, out home);
    }

    private void OnRelayExamine(EntityUid uid, CaptureTheFlagRelayComponent relay, ExaminedEvent args)
    {
        if (!TryGetMatch(relay.Rule, out var rule) || rule.Relay != uid)
            return;

        args.PushMarkup(relay.Team is { } owner
            ? Loc.GetString("ctf-relay-owned", ("team", GetTeamName(owner)))
            : Loc.GetString("ctf-relay-neutral"));
        if (rule.RelayState == CaptureTheFlagRelayState.Contested)
            args.PushMarkup(Loc.GetString("ctf-center-contested"));
        else if (relay.ClaimingTeam is {} challenger)
            args.PushMarkup(Loc.GetString("ctf-relay-progress", ("team", GetTeamName(challenger)),
                ("seconds", Math.Max(0, (int) Math.Ceiling((relay.CaptureAt - Timing.CurTime).TotalSeconds)))));

        TryGetHome(rule, CaptureTheFlagTeam.Red, out var red);
        TryGetHome(rule, CaptureTheFlagTeam.Blue, out var blue);
        args.PushMarkup(Loc.GetString("ctf-relay-reserves", ("red", red?.ReserveCharges ?? 0), ("blue", blue?.ReserveCharges ?? 0)));
        args.PushMarkup(Loc.GetString("ctf-relay-guide"));
    }

    private void OnTeamSupplyExamine(EntityUid uid, CaptureTheFlagSupplyComponent supply, ExaminedEvent args)
    {
        if (TryGetMatch(supply.Rule, out var rule) && TryGetHome(rule, supply.Team, out var home) && home.Supply == uid)
            args.PushMarkup(Loc.GetString("ctf-supply-reserves", ("charges", home.ReserveCharges), ("max", MaxReserveCharges)));
    }

    private void OnReserveVerbs(EntityUid uid, CaptureTheFlagSupplyComponent supply, GetVerbsEvent<Verb> args)
    {
        var stand = (uid, supply);
        var user = args.User;
        if (!args.CanAccess || !args.CanInteract || !TryGetReserve(stand, user, out _))
            return;

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("ctf-reserve-ammo"),
            Act = () => SpendReserve(stand, user, AmmoPacket),
        });
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("ctf-reserve-medical"),
            Act = () => SpendReserve(stand, user, MedicalPacket),
        });
    }

    private bool TryGetReserve(Entity<CaptureTheFlagSupplyComponent> supply, EntityUid user,
        [NotNullWhen(true)] out CaptureTheFlagBaseComponent? home)
    {
        home = null;
        return TryUseTeamSupply(supply, user, out var rule, out _) && TryGetHome(rule, supply.Comp.Team, out home) &&
            home.Supply == supply.Owner && home.ReserveCharges > 0;
    }

    private void SpendReserve(Entity<CaptureTheFlagSupplyComponent> supply, EntityUid user, EntProtoId[] packet)
    {
        if (!TryGetReserve(supply, user, out var home))
            return;

        home.ReserveCharges--;
        SendScoreboard(Comp<CaptureTheFlagRuleComponent>(supply.Comp.Rule));
        var position = Transform(user).Coordinates;
        foreach (var item in packet)
            _hands.PickupOrDrop(user, SpawnAttachedTo(item, position), animate: false);
        if (TryComp<ActorComponent>(user, out var actor))
            ChatManager.DispatchServerMessage(actor.PlayerSession, Loc.GetString("ctf-reserve-spent", ("charges", home.ReserveCharges)));
    }
}
