// SPDX-FileCopyrightText: Suspicion on Space Station contributors
// SPDX-License-Identifier: AGPL-3.0-only

using System.Linq;
using Content.Server.Access.Systems;
using Content.Server.Administration.Systems;
using Content.Server.Antag;
using Content.Server.Chat.Managers;
using Content.Server.Clothing.Systems;
using Content.Server.Communications;
using Content.Server.GameTicking;
using Content.Server._Pirate.Arenas;
using Content.Shared.Ghost;
using Content.Server.Implants;
using Content.Server.Mind;
using Content.Server.Pinpointer;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Robust.Shared.Map;
using Content.Server.Roles;
using Content.Server.RoundEnd;
using Content.Server.Station.Systems;
using Content.Server.Store.Systems;
using Content.Shared._Pirate.Suspicion;
using Content.Shared._Pirate.Suspicion.SuspicionGameRule;
using Content.Shared._Pirate.Suspicion.SuspicionGameRule.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Administration.Systems;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Hands;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Server.KillTracking;

namespace Content.Server._Pirate.Suspicion.SuspicionGameRule;

public sealed partial class SuspicionRuleSystem : ArenaRuleSystem<SuspicionRuleComponent>
{
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly RoleSystem _roleSystem = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawningSystem = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly AntagSelectionSystem _antagSelectionSystem = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;
    [Dependency] private readonly SubdermalImplantSystem _subdermalImplant = default!;
    [Dependency] private readonly AccessSystem _accessSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly KillTrackingSystem _killTracking = default!;
    [Dependency] private readonly StoreSystem _storeSystem = default!;
    [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly NpcFactionSystem _npcFactionSystem = default!;
    [Dependency] private readonly NavMapSystem _navMapSystem = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly IdCardSystem _idCardSystem = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookupSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ContainerSystem _containerSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    [Dependency] private readonly OutfitSystem _outfits = default!;
    [Dependency] private readonly IGameTiming _clock = default!;

    private TimeSpan _lastClockUpdate;

    private readonly SoundSpecifier _traitorStartSound = new SoundPathSpecifier("/Audio/Ambience/Antag/traitor_start.ogg");


    public override void Initialize()
    {
        base.Initialize();

        InitializeCVars();

        SubscribeLocalEvent<SuspicionPlayerComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
        SubscribeLocalEvent<SuspicionRoleComponent, GetBriefingEvent>(OnGetBriefing);
        SubscribeLocalEvent<SuspicionPlayerComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<SuspicionPlayerComponent, DidEquipHandEvent>(OnPlayerEquipsItem);
        SubscribeLocalEvent<SuspicionPlayerComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<CommunicationConsoleCallShuttleAttemptEvent>(OnShuttleCall);
        SubscribeLocalEvent<GhostHearingComponent, PlayerAttachedEvent>(OnGhost);
        SubscribeLocalEvent<SuspicionPlayerComponent, KillReportedEvent>(OnKillReported);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
    }


    protected override void AppendRoundEndText(EntityUid uid,
        SuspicionRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        var traitors = FindAllOfType(SuspicionRole.Traitor, false);
        var innocents = FindAllOfType(SuspicionRole.Innocent, false);
        var detectives = FindAllOfType(SuspicionRole.Detective, false);
        var wildcards = FindAllOfType(SuspicionRole.Wildcard, false);
        void Append(List<EntityUid> people, ref RoundEndTextAppendEvent args)
        {
            foreach (var person in people)
            {
                var name = FormattedMessage.EscapeText(MetaData(person).EntityName);
                var isDead = _mobState.IsDead(person);
                if (isDead)
                    args.AddLine(Loc.GetString("pirate-ttt-score-dead", ("name", name)));
                else
                    args.AddLine($"[bullet/] {name}");
            }
        }

        if (component.Winner is {} winner)
            args.AddLine(Loc.GetString(winner));

        args.AddLine(Loc.GetString("pirate-ttt-score-traitors", ("count", traitors.Count)));
        Append(traitors.Select(t => t.body).ToList(), ref args);
        args.AddLine(Loc.GetString("pirate-ttt-score-innocents", ("count", innocents.Count)));
        Append(innocents.Select(t => t.body).ToList(), ref args);
        args.AddLine(Loc.GetString("pirate-ttt-score-detectives", ("count", detectives.Count)));
        Append(detectives.Select(t => t.body).ToList(), ref args);
        args.AddLine(Loc.GetString("pirate-ttt-score-wildcards", ("count", wildcards.Count)));
        Append(wildcards.Select(t => t.body).ToList(), ref args);
    }


    protected override void Started(EntityUid uid, SuspicionRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        _lastClockUpdate = Timing.CurTime;
        StartClock(TimeSpan.FromMilliseconds(250), Tick);
        component.GameState = SuspicionGameState.Preparing;

        component.EndAt = Timing.CurTime + TimeSpan.FromSeconds(Math.Max(0, _preparingDuration));
        RaiseNetworkEvent(new SuspicionRulePreroundStarted(component.EndAt));
    }

    protected override void Ended(EntityUid uid, SuspicionRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);
        RaiseNetworkEvent(new SuspicionHudClosed());
    }

    private void OnPlayerAttached(EntityUid ent, SuspicionPlayerComponent component, PlayerAttachedEvent args)
    {
        if (ActiveRule is not { } active)
            return;

        var rule = active.Comp;
        RaiseNetworkEvent(new SuspicionRulePlayerSpawn
        {
            GameState = rule.GameState,
            EndTime = rule.EndAt,
        }, args.Player);
        if (rule.GameState == SuspicionGameState.Preparing || !TryGetOwnedRole(ent, args.Player, out var role))
            return;

        if (role.Role == SuspicionRole.Pending)
            AssignInnocent(ent, role);

        RaiseNetworkEvent(new SuspicionRuleUpdateRole(role.Role, role.SubRole), args.Player);
        SendAllyMarkers(args.Player);
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent args)
    {
        if (ActiveRule is not {} active || args.Old != GameRunLevel.InRound)
            return;

        StopClock();
        active.Comp.GameState = SuspicionGameState.PostRound;
        active.Comp.EndAt = TimeSpan.Zero;
        RaiseNetworkEvent(new SuspicionRuleTimerUpdate(active.Comp.EndAt));
        RaiseNetworkEvent(new SuspicionAllyRosterUpdated(NetEntity.Invalid, []));
    }

    private void OnPlayerEquipsItem(EntityUid uid, SuspicionPlayerComponent component, DidEquipHandEvent args)
    {
        if (ActiveRule?.Comp.GameState is SuspicionGameState.Preparing or SuspicionGameState.InProgress)
            AllowArenaGunUse(args.Equipped);
    }

    private void Tick()
    {
        if (ActiveRule is not { } active)
            return;

        var elapsed = Timing.CurTime - _lastClockUpdate;
        _lastClockUpdate = Timing.CurTime;
        var sus = active.Comp;
        if (sus.GameState == SuspicionGameState.Preparing)
        {
            var remaining = sus.EndAt - Timing.CurTime;
            if (remaining <= TimeSpan.Zero)
                StartRound(sus);
            else if (remaining.TotalSeconds <= 5 && sus.AnnouncedTimeLeft.Add(0))
                _chatManager.DispatchServerAnnouncement(Loc.GetString("pirate-ttt-prep-warning"));
            return;
        }

        if (sus.GameState != SuspicionGameState.InProgress)
            return;

        UpdateTimer(sus, elapsed);
        if (sus.GameState != SuspicionGameState.InProgress || _clock.CurTime < sus.NextPlayerCheck)
            return;

        sus.NextPlayerCheck = _clock.CurTime + TimeSpan.FromSeconds(1);
        UpdateSpaceWalkDamage();
        UpdateDeadRoundEndCondition(active);
    }
}
