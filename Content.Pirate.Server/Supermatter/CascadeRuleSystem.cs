// SPDX-FileCopyrightText: Contributors to Impstation
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Supermatter.Components;
using Content.Server.AlertLevel;
using Content.Server.Chat.Systems;
using Content.Goobstation.Server.Supermatter.Systems;
using Content.Shared._Pirate.Supermatter;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Content.Server.Communications;
using Content.Server.GameTicking.Rules;
using Content.Server.RoundEnd;
using Content.Server.Shuttles.Systems;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;


namespace Content.Pirate.Server.Supermatter;

/// <summary>
///     Manages <see cref="CascadeRuleComponent"/>
/// </summary>
public sealed class CascadeRuleSystem : GameRuleSystem<CascadeRuleComponent>
{
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefManager = default!;
    [Dependency] private readonly AlertLevelSystem _alertLevelSystem = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SupermatterSystem _supermatter = default!;
    private static readonly EntProtoId CascadeRule = "SupermatterCascadeDelamRule";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SupermatterCascadeEvent>(OnDelamination);
        SubscribeLocalEvent<CascadeCoreComponent, InteractUsingEvent>(OnInteract);
        SubscribeLocalEvent<CascadeCoreComponent, DestabilizeCrystalDoAfterEvent>(OnDestabilize);
        SubscribeLocalEvent<CommunicationConsoleCallShuttleAttemptEvent>(OnShuttleCallAttempt);
        SubscribeLocalEvent<RoundEndRequestAttemptEvent>(OnEvacuationAttempt);
        SubscribeLocalEvent<EmergencyShuttleDepartureAttemptEvent>(OnDepartureAttempt);
    }

    protected override void Started(EntityUid uid, CascadeRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        var initialWindow = TimeSpan.FromTicks(Math.Min(comp.DurationForInitialAnnouncements.Ticks, comp.DurationToRoundEnd.Ticks / 2));
        comp.TimeUntilInitialAnnouncements = Timing.CurTime + initialWindow;
        comp.TimeUntilEndRound = comp.DurationToRoundEnd + Timing.CurTime;

        SetAlertLevelDelta();

        var tile = new Tile(_tileDefManager[comp.CrystalMassPlating].TileId);
        foreach (var origin in comp.PendingOrigins)
            SpawnSupermatterCrystalMass(origin, comp, tile);
        comp.PendingOrigins.Clear();

        SpawnRandomCrystalMass(comp, tile);
    }

    protected override void ActiveTick(EntityUid uid, CascadeRuleComponent comp, GameRuleComponent gameRule, float frameTime)
    {
        if (comp.Stage < ResonanceCascadeStage.BeginningNTAnnouncement
            && comp.TimeUntilInitialAnnouncements - Timing.CurTime < comp.DurationForInitialAnnouncements / 2)
        {
            Announce("resonance-cascade-announcement-begin");

            comp.Stage = ResonanceCascadeStage.BeginningNTAnnouncement;
        }

        if (comp.Stage < ResonanceCascadeStage.BeginningEvacCancel
            && comp.TimeUntilInitialAnnouncements < Timing.CurTime)
        {
            if (_roundEndSystem.IsRoundEndRequested())
            {
                _roundEndSystem.CancelRoundEndCountdown(uid, true);
                _chat.DispatchGlobalAnnouncement(Loc.GetString("emergancy-shuttle-cascade-enroute"),
                    Loc.GetString("emergancy-shuttle-announcement-sender"), colorOverride: Color.Yellow);
            }

            comp.Stage = ResonanceCascadeStage.BeginningEvacCancel;
        }

        if (comp.Stage == ResonanceCascadeStage.BeginningEvacCancel
            && comp.TimeUntilEndRound - Timing.CurTime < comp.DurationToRoundEnd / 2)
        {
            Announce("resonance-cascade-announcement-middle");

            comp.Stage = ResonanceCascadeStage.MiddleNTAnnouncement;
        }

        if (comp.Stage < ResonanceCascadeStage.End
            && comp.TimeUntilEndRound < Timing.CurTime)
        {
            _roundEndSystem.EndRound();

            var count = comp.MinMaxSinglarity.Next(_robustRandom);
            for (var i = 0; i < count; i++)
                if (TryFindRandomTile(out _, out _, out _, out var coords))
                    Spawn(comp.SingularityPrototype, coords);

            comp.Stage = ResonanceCascadeStage.End;
        }
    }

    private void Announce(string key)
    {
        _chat.DispatchGlobalAnnouncement(Loc.GetString(key), Loc.GetString("resonance-cascade-announcement-sender"),
            colorOverride: Color.Cyan);
    }

    private void OnInteract(Entity<CascadeCoreComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.Destabilized || !HasComp<SupermatterDestabilizerComponent>(args.Used) ||
            !TryComp<SupermatterComponent>(ent, out var sm))
            return;

        args.Handled = true;
        if (_supermatter.GetIntegrity(sm) < 80f)
        {
            _popup.PopupEntity(Loc.GetString("supermatter-destabalize-integrity-low"), ent, args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString("supermatter-destabalize-start"), ent, args.User);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(5),
            new DestabilizeCrystalDoAfterEvent(), ent, ent, args.Used)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            BreakOnWeightlessMove = false,
            NeedHand = true,
        });
    }

    private void OnDestabilize(Entity<CascadeCoreComponent> ent, ref DestabilizeCrystalDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.Destabilized || args.Used is not { } used ||
            !HasComp<SupermatterDestabilizerComponent>(used) || !TryComp<SupermatterComponent>(ent, out var sm) ||
            _supermatter.GetIntegrity(sm) < 80f)
            return;

        args.Handled = true;
        ent.Comp.Destabilized = true;
        QueueDel(used);
        Announce("supermatter-announcement-cascade-destabalize");
        _popup.PopupEntity(Loc.GetString("supermatter-destabalize-end"), ent, args.User);
    }

    private void OnDelamination(SupermatterCascadeEvent ev)
    {
        if (TerminatingOrDeleted(ev.Core))
            return;

        EntityUid? rule = null;
        var existing = EntityQueryEnumerator<CascadeRuleComponent>();
        while (existing.MoveNext(out var uid, out _))
        {
            if (HasComp<EndedGameRuleComponent>(uid))
                continue;
            rule = uid;
            break;
        }

        var newRule = rule == null;
        var ruleUid = rule ?? GameTicker.AddGameRule(CascadeRule);
        var cascade = Comp<CascadeRuleComponent>(ruleUid);
        var xform = Transform(ev.Core);
        if (xform.GridUid is { } gridUid && TryComp<MapGridComponent>(gridUid, out var grid))
        {
            var tile = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates);
            var origin = _map.GridTileToLocal(gridUid, grid, tile);
            if (HasComp<ActiveGameRuleComponent>(ruleUid))
                SpawnSupermatterCrystalMass(origin, cascade, new Tile(_tileDefManager[cascade.CrystalMassPlating].TileId));
            else
                cascade.PendingOrigins.Add(origin);
        }
        if (newRule)
            GameTicker.StartGameRule(ruleUid);
        QueueDel(ev.Core);
    }

    private void OnEvacuationAttempt(ref RoundEndRequestAttemptEvent ev)
    {
        var query = QueryActiveRules();
        if (query.MoveNext(out _, out _, out _, out _))
            ev.Block("emergancy-shuttle-cascade-call-unavailable");
    }

    private void OnDepartureAttempt(ref EmergencyShuttleDepartureAttemptEvent ev)
    {
        var query = QueryActiveRules();
        if (query.MoveNext(out _, out _, out _, out _))
            ev.Cancelled = true;
    }

    private void OnShuttleCallAttempt(ref CommunicationConsoleCallShuttleAttemptEvent ev)
    {
        var query = QueryActiveRules();
        if (!query.MoveNext(out _, out _, out _, out _))
            return;

        ev.Cancelled = true;
        ev.Reason = Loc.GetString("emergancy-shuttle-cascade-call-unavailable");
    }

    private void SpawnSupermatterCrystalMass(EntityCoordinates origin, CascadeRuleComponent comp, Tile tile)
    {
        if (!TryComp<MapGridComponent>(origin.EntityId, out var grid))
            return;

        _map.SetTile(origin.EntityId, grid, origin, tile);
        Spawn(comp.CrystalBulbPrototype, origin);
    }

    private void SpawnRandomCrystalMass(CascadeRuleComponent comp, Tile tile)
    {
        var count = comp.MinMaxCrystalMassSpawn.Next(_robustRandom);
        for (var i = 0; i < count; i++)
        {
            if (TryFindRandomTile(out _, out _, out var targetGrid, out var coords))
            {
                if (!TryComp<MapGridComponent>(targetGrid, out var mapGrid))
                    break;

                _map.SetTile(targetGrid, mapGrid, coords, tile);
                Spawn(comp.CrystalBulbPrototype, coords);
            }
        }
    }

    private void SetAlertLevelDelta()
    {
        if (!TryGetRandomStation(out var station))
            return;
        if (_alertLevelSystem.GetLevel(station.Value) == "delta") // Don't delta if already delta
            return;
        _alertLevelSystem.SetLevel(station.Value, "delta", true, true, true);
    }
}
