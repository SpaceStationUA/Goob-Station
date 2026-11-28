// SPDX-FileCopyrightText: 2025 Moffstation
// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Server._Pirate.GunGame.Components;
using Content.Server.GameTicking;
using Content.Server._Pirate.Arenas;
using Content.Server.KillTracking;
using Content.Server.Mind;
using Content.Server.Power.Components;
using Content.Server.RoundEnd;
using Content.Shared.Administration.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Power.Components;
using Content.Shared.PowerCell.Components;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Server.Station.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Pirate.GunGame;

/// <summary>
/// The system for the gungame gamemode rule.
/// This is mostly a copy of <see cref="Content.Server.GameTicking.Rules.DeathMatchRuleSystem"/> with some
/// key changes for gun game.
/// </summary>
public sealed partial class GunGameRuleSystem : ArenaRuleSystem<GunGameRuleComponent>
{
    [Dependency] private Content.Server.Chat.Managers.IChatManager _chat = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private readonly KillTrackingSystem _kills = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private StationSpawningSystem _stationSpawning = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private RejuvenateSystem _rejuvenate = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
        SubscribeLocalEvent<RoundStartAttemptEvent>(OnRoundStartAttempt);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<GunGameTrackerComponent, KillReportedEvent>(OnKillReported);
    }

    private void OnRoundStartAttempt(RoundStartAttemptEvent args)
    {
        if (GameTicker.CurrentPreset?.ID != "GunGame" || args.Cancelled || args.Forced || args.Players.Length >= 2)
            return;

        ChatManager.SendAdminAnnouncement(Loc.GetString("pirate-gun-game-too-few"));
        args.CancelWithoutFallback();
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent args)
    {
        if (ActiveRule == null)
            return;

        if (args.New == GameRunLevel.InRound)
        {
            _player.PlayerStatusChanged += OnSessionStatusChanged;
            MatchToken.Register(() => _player.PlayerStatusChanged -= OnSessionStatusChanged);
        }
        else
            StopClock();
    }

    private void OnBeforeSpawn(PlayerBeforeSpawnEvent ev)
    {
        if (ev.Handled || ActiveRule is not {} active)
            return;
        if (active.Comp.Victor != null || GameTicker.RunLevel == GameRunLevel.PostRound)
        {
            GameTicker.JoinAsObserver(ev.Player);
            ev.Handled = true;
            return;
        }
        if (active.Comp.PlayerInfo.TryGetValue(ev.Player.UserId, out var enrolled))
        {
            if (enrolled.PendingRespawn != null)
            {
                ev.Handled = true;
                return;
            }

            if (_mind.TryGetMind(ev.Player.UserId, out _, out var owned) && owned.OwnedEntity is {} body &&
                TryComp<GunGameTrackerComponent>(body, out var tracker) && !tracker.DeathRecorded)
            {
                if (owned.VisitingEntity != null)
                    _mind.UnVisit(ev.Player);
                if (ev.Player.AttachedEntity != body)
                    _player.SetAttachedEntity(ev.Player, body);
                ev.Handled = true;
                return;
            }
        }

        ev.Handled = SpawnFighter(active, ev.Player, ev.Profile, ev.Station);
    }

    private bool SpawnFighter(Entity<GunGameRuleComponent> active, ICommonSession session,
        HumanoidCharacterProfile profile, EntityUid station)
    {
        var (uid, gunGame) = active;

        if (_mind.TryGetMind(session.UserId, out var mindId, out var mind))
        {
            if (mind.VisitingEntity?.Valid == true)
                _mind.UnVisit(mindId.Value, mind);
        }
        else
        {
            mindId = _mind.CreateMind(session.UserId, profile.Name);
        }

        var spawn = TryComp<AvoidantSpawningComponent>(uid, out var avoidant) ? SelectSpawn((uid, avoidant), station) : null;
        var mobMaybe = spawn is {} position
            ? _stationSpawning.SpawnPlayerMob(position, null, profile, station, ckey: session.Name)
            : _stationSpawning.SpawnPlayerCharacterOnStation(station, null, profile, ckey: session.Name);
        if (mobMaybe is not {} mob)
            return false;

        _mind.TransferTo(mindId.Value, mob, ghostCheckOverride: true);
        _kills.SetKillState(mob, MobState.Dead);
        EnsureComp<GunGameTrackerComponent>(mob).Player = session.UserId;

        if (!gunGame.PlayerInfo.TryGetValue(session.UserId, out var player))
        {
            player = new GunGamePlayerTrackingInfo(session.UserId, new Queue<ProtoId<StartingGearPrototype>>(gunGame.RewardSpawnsQueue));
            gunGame.PlayerInfo.Add(session.UserId, player);
        }
        var previous = player.PendingRespawn;
        player.PendingRespawn = null;
        player.Profile = profile;
        RefreshPlayerLoadout(player, gunGame);

        if (previous is {} old && !TerminatingOrDeleted(old.Corpse))
            QueueDel(old.Corpse);
        return true;
    }

    /// <summary>
    /// Activates when a kill is reported. ev.Entity corresponds to the person who was killed.
    /// </summary>
    private void OnKillReported(Entity<GunGameTrackerComponent> victim, ref KillReportedEvent ev)
    {
        if (ActiveRule is not {} active)
            return;
        var gunGame = active.Comp;

        // Don't want other players picking up somebody's gun
        if (gunGame.Victor != null || victim.Comp.DeathRecorded || GameTicker.RunLevel == GameRunLevel.PostRound)
            return;

        victim.Comp.DeathRecorded = true;
        DeleteCurrentLoadout(victim, gunGame);

        QueueRespawn(victim, active);

        // Make sure the thing that killed them was a player, and that it was a player
        //  other than their own foolish self. Can't suicide your way to victory.
        if (ev.Suicide || ev.Primary is not KillPlayerSource killer)
            return;

        if (!gunGame.PlayerInfo.TryGetValue(killer.PlayerId, out var killerInfo))
            return;
        // Only allow the player to receive their next weapon after they get enough kills.
        if (++killerInfo.Kills < gunGame.KillsPerWeapon)
            return;

        killerInfo.Kills = 0;
        ProgressPlayerReward(killerInfo, gunGame);
        if (gunGame.Victor != null || !_player.TryGetSessionById(killer.PlayerId, out var session) ||
            session.AttachedEntity is not {} killerUid ||
            !TryComp<GunGameTrackerComponent>(killerUid, out var killerBody) || killerBody.DeathRecorded ||
            killerBody.Player != killer.PlayerId)
            return;

        _rejuvenate.PerformRejuvenate(killerUid);
        RefreshPlayerLoadout(killerInfo, gunGame);
    }

    /// <summary>
    /// Appends round end text at the end of the round.
    /// </summary>
    protected override void AppendRoundEndText(EntityUid uid, GunGameRuleComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        if (component.Victor is { } victor && _player.TryGetPlayerData(victor, out var data))
        {
            args.AddLine(Loc.GetString("gun-game-scoreboard-winner", ("player", data.UserName)));
            args.AddLine("");
        }

        args.AddLine(Loc.GetString("gun-game-scoreboard-header"));
        args.AddLine(GetScoreboard((uid, component)).ToMarkup());
    }

    /// <summary>
    /// Deletes a GunGameRewardTrackerComponent's gear.
    /// </summary>
    /// <remarks>
    /// Unequips all the spawned entities on the player before deleting them.
    /// Also removes nearby shell casings to clean things up.
    /// todo: once WeakEntityReference is implemented, ensure it's utilized here.
    /// </remarks>
    private void DeleteCurrentLoadout(Entity<GunGameTrackerComponent> playerEntity, GunGameRuleComponent rule)
    {
        // drop whatever is in their hands
        _hands.TryDrop(playerEntity.Owner, null, false, false);

        // delete all their rewards
        foreach (var entity in playerEntity.Comp.CurrentRewards)
        {
            if (_inventory.TryGetContainingSlot(entity, out var slot))
                _inventory.TryUnequip(playerEntity, playerEntity, slot.Name, true, true, true);

            QueueDel(entity);
        }
        playerEntity.Comp.CurrentRewards.Clear();

        // delete nearby shell casings
        foreach (var entity in _lookup.GetEntitiesInRange<CartridgeAmmoComponent>(
                     new EntityCoordinates(playerEntity.Owner, 0.0f, 0.0f),
                     rule.CasingDeletionRange))
        {
            if (entity.Comp.Spent && _random.Prob(rule.CasingDeletionProb))
                QueueDel(entity.Owner);
        }
    }

    /// <summary>
    /// Progresses the specified user's reward queue.
    /// If their queue is empty then they've won so we trigger the round end.
    /// </summary>
    /// <param name="playerInfo">The player's current round info</param>
    /// <param name="rule">The GunGameRuleComponent for this gamerule</param>
    private void ProgressPlayerReward(GunGamePlayerTrackingInfo playerInfo, GunGameRuleComponent rule)
    {
        if (playerInfo.RewardQueue.Count > 1 // We want the last weapon to remain in the queue so we can peek it later.
            && playerInfo.RewardQueue.TryDequeue(out _))
            return;

        rule.Victor = playerInfo.UserId;
        StopClock();
        _roundEnd.EndRound(rule.RestartDelay);
    }

    /// <summary>
    /// Spawns the player's current weapon in their queue, intended to be used on spawn and when they upgrade weapons.
    /// </summary>
    /// <param name="playerInfo">The player's current round info</param>
    /// <param name="rule">The GunGameRuleComponent for this gamerule</param>
    private void RefreshPlayerLoadout(GunGamePlayerTrackingInfo playerInfo, GunGameRuleComponent rule)
    {
        if (!_mind.TryGetMind(playerInfo.UserId, out _, out var mind))
            return;

        if (mind.OwnedEntity is not { } playerEntity)
            return;

        var breathingTank = CompOrNull<InternalsComponent>(playerEntity)?.GasTankEntity;
        var currentGear = EnsureComp<GunGameTrackerComponent>(playerEntity);
        DeleteCurrentLoadout((playerEntity, currentGear), rule);

        if (playerInfo.RewardQueue.Count == 0) // If we somehow try to spawn somebody's weapon after they win
            return;

        // Peek the player's queue, and spawn their items
        var id = playerInfo.RewardQueue.Peek();
        SpawnPlayerLoadout((playerEntity, currentGear), id, rule, keepBreathMask: breathingTank != null);
        EquipArenaBreathingGear(playerEntity, breathingTank);
        if (_player.TryGetSessionById(playerInfo.UserId, out var session))
        {
            _chat.DispatchServerMessage(session, Loc.GetString("pirate-gun-game-stage",
                ("level", rule.RewardSpawnsQueue.Count - playerInfo.RewardQueue.Count + 1),
                ("total", rule.RewardSpawnsQueue.Count), ("weapon", GetStageWeaponName(id))));
        }
    }

    private string GetStageWeaponName(ProtoId<StartingGearPrototype> loadout)
    {
        var gear = _proto.Index(loadout);
        if (gear.Inhand.Count > 0)
            return _proto.Index(gear.Inhand[0]).Name;

        // The quartermaster fights with brass knuckles equipped in the glove slot.
        return gear.Equipment.TryGetValue("gloves", out var gloves)
            ? _proto.Index(gloves).Name
            : Loc.GetString("gun-game-scoreboard-list-empty");
    }

    /// <summary>
    /// Spawns all the items specified in the player's current loadout. Dropping anything in a slot being filled.
    /// This is used instead of one of the pre-existing equipping methods since we need to keep track of what is being
    /// spawned.
    /// </summary>
    private void SpawnPlayerLoadout(Entity<GunGameTrackerComponent> target,
        ProtoId<StartingGearPrototype> gear,
        GunGameRuleComponent gameRule, bool keepBreathMask)
    {
        if (!_proto.TryIndex(gear, out var startingGear))
            return;

        foreach (var (slotName, itemProto) in startingGear.Equipment)
        {
            // Keep a connected breathing mask through upgrades for species that need a tank.
            if (slotName == "mask" && keepBreathMask)
                continue;

            _inventory.TryUnequip(target, slotName, true, true, false);
            var itemEntity = Spawn(itemProto);
            target.Comp.CurrentRewards.Add(itemEntity);
            AllowArenaGunUse(itemEntity);
            UpgradeEnergyWeapon(itemEntity, gameRule);
            _inventory.TryEquip(target.Owner, itemEntity, slotName, silent: true, force: true);
        }

        foreach (var itemProto in startingGear.Inhand)
        {
            var itemEntity = Spawn(itemProto, Transform(target).Coordinates);
            target.Comp.CurrentRewards.Add(itemEntity);
            AllowArenaGunUse(itemEntity);
            UpgradeEnergyWeapon(itemEntity, gameRule);
            // A stage reward can arrive while the shooter is recovering from critical damage.
            _hands.TryForcePickupAnyHand(target.Owner, itemEntity, checkActionBlocker: false);
        }
    }

    /// <summary>
    /// Formats the scoreboard for the end of round screen.
    /// </summary>
    /// <returns>
    /// The formatted message to add to the round-end screen.
    /// Returns an empty message if the component doesn't exist.
    /// </returns>
    private FormattedMessage GetScoreboard(Entity<GunGameRuleComponent?> entity)
    {
        var msg = new FormattedMessage();

        if (!Resolve(entity, ref entity.Comp))
            return msg;

        foreach (var (idx, playerInfo) in entity.Comp.PlayerInfo.Values.OrderBy(p => p.RewardQueue.Count)
                     .ThenByDescending(p => p.Kills).Select((value, index) => (index, value)))
        {
            if (!_player.TryGetPlayerData(playerInfo.UserId, out var data))
                continue;

            var weaponName = playerInfo.RewardQueue.TryPeek(out var loadout)
                ? GetStageWeaponName(loadout)
                : Loc.GetString("gun-game-scoreboard-list-empty");

            msg.AddMarkupOrThrow(Loc.GetString("gun-game-scoreboard-list-entry",
                ("place", idx + 1),
                ("name", data.UserName),
                ("weaponsLeft", playerInfo.RewardQueue.Count - 1),
                ("weapon", weaponName)));
            msg.PushNewline();
        }

        return msg;
    }

    /// <summary>
    /// Upgrades an energy weapon to have a rechargeable battery.
    /// This does account for whether the weapon contains a battery itself,
    /// at which point it grabs that battery and upgrades it instead.
    /// Also, this will set the recharge rate to only increase up to
    /// <see cref="GunGameRuleComponent.DefaultEnergyWeaponRechargeRate"/>.
    /// If the weapon already has a recharge rate higher than this, it uses that instead.
    /// </summary>
    private void UpgradeEnergyWeapon(EntityUid uid, GunGameRuleComponent rule)
    {
        var upgradeEnt = HasComp<BatteryComponent>(uid)
            ? uid
            : TryComp<PowerCellSlotComponent>(uid, out var cellSlot)
                ? _itemSlots.GetItemOrNull(uid, cellSlot.CellSlotId)
                : null;

        if (upgradeEnt is not { } upgradable)
            return;

        var batteryRecharge = EnsureComp<BatterySelfRechargerComponent>(upgradable);
        batteryRecharge.AutoRechargeRate = Math.Max(rule.DefaultEnergyWeaponRechargeRate, batteryRecharge.AutoRechargeRate);
    }
}
