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
    }

    protected override void Started(EntityUid uid, CascadeRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        comp.TimeUntilInitialAnnouncements = comp.DurationForInitialAnnouncements + Timing.CurTime;
        comp.TimeUntilEndRound = comp.DurationToRoundEnd + Timing.CurTime;

        SetAlertLevelDelta();

        var tile = new Tile(_tileDefManager[comp.CrystalMassPlating].TileId);
        if (comp.Origin is { } origin && Exists(origin))
            SpawnSupermatterCrystalMass(origin, comp, tile);

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

        if (comp.Stage < ResonanceCascadeStage.MiddleNTAnnouncement
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

        var active = QueryActiveRules();
        if (active.MoveNext(out _, out _, out var cascade, out _))
        {
            var tile = new Tile(_tileDefManager[cascade.CrystalMassPlating].TileId);
            SpawnSupermatterCrystalMass(ev.Core, cascade, tile);
        }
        else
        {
            var rule = GameTicker.AddGameRule(CascadeRule);
            Comp<CascadeRuleComponent>(rule).Origin = ev.Core;
            GameTicker.StartGameRule(rule);
        }
        QueueDel(ev.Core);
    }

    private void OnShuttleCallAttempt(ref CommunicationConsoleCallShuttleAttemptEvent ev)
    {
        var query = QueryActiveRules();
        if (!query.MoveNext(out _, out _, out _, out _))
            return;

        ev.Cancelled = true;
        ev.Reason = Loc.GetString("emergancy-shuttle-cascade-call-unavailable");
    }

    private void SpawnSupermatterCrystalMass(EntityUid supermatterUid, CascadeRuleComponent comp, Tile tile)
    {
        var xform = Transform(supermatterUid);
        var gridUid = xform.GridUid;

        if (!TryComp<MapGridComponent>(gridUid, out var supermatterMapGrid))
            return;

        _map.SetTile(gridUid.Value, supermatterMapGrid, xform.Coordinates, tile);
        Spawn(comp.CrystalBulbPrototype, xform.Coordinates);
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
