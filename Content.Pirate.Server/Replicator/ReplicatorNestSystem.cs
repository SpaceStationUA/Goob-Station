// SPDX-FileCopyrightText: 2025 beck <163376292+widgetbeck@users.noreply.github.com>

// SPDX-License-Identifier: AGPL-3.0-or-later

// these are HEAVILY based on the Bingle free-agent ghostrole from GoobStation, but reflavored and reprogrammed to make them more Robust (and less of a meme.)
// all credit for the core gameplay concepts and a lot of the core functionality of the code goes to the folks over at Goob, but I re-wrote enough of it to justify putting it in our filestructure.
// the original Bingle PR can be found here: https://github.com/Goob-Station/Goob-Station/pull/1519

using Content.Server.Audio;
using Content.Server.Buckle.Systems;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Pinpointer;
using Content.Server.Popups;
using Content.Server.Storage.EntitySystems;
using Content.Server.Stunnable;
using Content.Pirate.Shared.Replicator;
using Content.Shared.Actions;
using Content.Shared.Audio;
using Content.Shared.Buckle.Components;
using Content.Shared.Destructible;
using Content.Shared.Inventory;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Pinpointer;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Whitelist;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Linq;
using Content.Shared.Movement.Systems;
using Content.Shared.Storage.Components;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Pirate.Server.Replicator;

public sealed class ReplicatorNestSystem : SharedReplicatorNestSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    [Dependency] private readonly SharedReplicatorNestSystem _sharedNest = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly ContainerSystem _containerSystem = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly StunSystem _stun = default!;
    [Dependency] private readonly MovementModStatusSystem _movementMod = default!;
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly PinpointerSystem _pinpointer = default!;
    [Dependency] private readonly AmbientSoundSystem _ambientSound = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly EntityStorageSystem _entStorage = default!;
    [Dependency] private readonly BuckleSystem _buckle = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorNestComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ReplicatorNestComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
        SubscribeLocalEvent<ReplicatorNestComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
        SubscribeLocalEvent<ReplicatorNestComponent, StepTriggeredOffEvent>(OnStepTriggered);
        SubscribeLocalEvent<ReplicatorNestFallingComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<ReplicatorNestComponent, DestructionEventArgs>(OnDestroyed);
        SubscribeLocalEvent<ReplicatorNestComponent, EntityTerminatingEvent>(OnTerminating);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndTextAppend);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        HashSet<EntityUid> toDel = [];

        var query = EntityQueryEnumerator<ReplicatorNestFallingComponent>();
        while (query.MoveNext(out var uid, out var falling))
        {
            if (_timing.CurTime < falling.NextDeletionTime)
                continue;

            var nestComp = falling.FallingTarget.Comp;

            if (!nestComp.HasAnnounced && nestComp.CurrentLevel >= nestComp.AnnounceAtLevel)
            {
                nestComp.HasAnnounced = true;
                _chat.DispatchGlobalAnnouncement(Loc.GetString(nestComp.Announcement), colorOverride: Color.Red);
            }

            // Delete blacklisted entities, or entities that are neither preserved nor controlled by a player.
            var hasMind = TryComp<MindContainerComponent>(uid, out var mind) && mind.HasMind;
            if (_whitelist.IsWhitelistPass(nestComp.PreservationBlacklist, uid) ||
                (!_whitelist.IsWhitelistPass(nestComp.PreservationWhitelist, uid) && !hasMind))
                toDel.Add(uid);

            _containerSystem.Insert(uid, falling.FallingTarget.Comp.Hole);
            EnsureComp<StunnedComponent>(uid); // used stunned to prevent any funny being done inside the pit
            RemCompDeferred(uid, falling);
        }

        foreach (var uid in toDel)
        {
            QueueDel(uid);
        }
    }

    private void OnEntRemoved(Entity<ReplicatorNestComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        RemCompDeferred<StunnedComponent>(args.Entity);
    }

    private void OnMapInit(Entity<ReplicatorNestComponent> ent, ref MapInitEvent args)
    {
        if (!Transform(ent).Coordinates.IsValid(EntityManager))
            QueueDel(ent);

        ent.Comp.Hole = _containerSystem.EnsureContainer<Container>(ent, "hole");

        ent.Comp.NextSpawnAt = ent.Comp.SpawnNewAt;
        ent.Comp.NextUpgradeAt = ent.Comp.UpgradeAt;
        ent.Comp.NextTileConvertAt = ent.Comp.TileConvertAt;

        var pointsStorageEnt = Spawn("ReplicatorNestPointsStorage", Transform(ent).Coordinates);
        EnsureComp<ReplicatorNestPointsStorageComponent>(pointsStorageEnt);

        ent.Comp.PointsStorage = pointsStorageEnt;
        _sharedNest.SyncPointsStorage(ent);
    }

    private void OnStepTriggerAttempt(Entity<ReplicatorNestComponent> ent, ref StepTriggerAttemptEvent args)
    {
        args.Continue = true;
    }

    private void OnStepTriggered(Entity<ReplicatorNestComponent> ent, ref StepTriggeredOffEvent args)
    {
        // dont accept if they are already falling
        if (HasComp<ReplicatorNestFallingComponent>(args.Tripper))
            return;

        // *reject* if blacklisted
        if (_whitelist.IsWhitelistPass(ent.Comp.Blacklist, args.Tripper))
        {
            if (TryComp<PullableComponent>(args.Tripper, out var pullable) && pullable.BeingPulled)
                _pulling.TryStopPull(args.Tripper, pullable);

            var xform = Transform(ent);
            var xformQuery = GetEntityQuery<TransformComponent>();
            var worldPos = _xform.GetWorldPosition(xform, xformQuery);

            var direction = _xform.GetWorldPosition(args.Tripper, xformQuery) - worldPos;
            _throwing.TryThrow(args.Tripper, direction * 10, 7, ent, 0);
            return;
        }

        var isReplicator = HasComp<ReplicatorComponent>(args.Tripper);

        // Allow dead replicators regardless of current level.
        if (TryComp<MobStateComponent>(args.Tripper, out var mobState) && isReplicator && _mobState.IsDead(args.Tripper))
        {
            _sharedNest.StartFalling(ent, args.Tripper);
            return;
        }

        // Don't allow living beings. If you want those sweet bonus points, you have to kill.
        if (mobState != null && _mobState.IsAlive(args.Tripper))
            return;

        // if the ent is a container, all its contents go in the hole
        if (TryComp<EntityStorageComponent>(args.Tripper, out var entStorage))
        {
            _entStorage.EmptyContents(args.Tripper, entStorage);
        }

        if (TryComp<StrapComponent>(args.Tripper, out var strapComp) && strapComp.BuckledEntities.Count > 0)
        {
            foreach (var buckled in strapComp.BuckledEntities)
            {
                if (TryComp<BuckleComponent>(buckled, out var buckleComp))
                    _buckle.Unbuckle((buckled, buckleComp), null);
            }
        }

        _sharedNest.StartFalling(ent, args.Tripper);
    }

    private void OnUpdateCanMove(Entity<ReplicatorNestFallingComponent> ent, ref UpdateCanMoveEvent args)
    {
        args.Cancel();
    }

    private void OnDestroyed(Entity<ReplicatorNestComponent> ent, ref DestructionEventArgs args)
    {
        ent.Comp.PreservePointsStorage = true;
        HandleDestruction(ent);
    }

    private void OnTerminating(Entity<ReplicatorNestComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!ent.Comp.PreservePointsStorage && Exists(ent.Comp.PointsStorage))
            QueueDel(ent.Comp.PointsStorage);
    }

    private void HandleDestruction(Entity<ReplicatorNestComponent> ent)
    {
        // turn off the ambient sound on the points storage entity.
        if (TryComp<AmbientSoundComponent>(ent.Comp.PointsStorage, out var ambientComp))
            _ambientSound.SetAmbience(ent.Comp.PointsStorage, false, ambientComp);

        if (ent.Comp.Hole != null)
        {
            foreach (var uid in _containerSystem.EmptyContainer(ent.Comp.Hole))
            {
                RemCompDeferred<StunnedComponent>(uid);
                _stun.TryKnockdown(uid, TimeSpan.FromSeconds(2), false);
            }
        }

        // delete all unclaimed spawners
        foreach (var spawner in ent.Comp.UnclaimedSpawners)
        {
            QueueDel(spawner);
        }
        ent.Comp.UnclaimedSpawners.Clear();

        // remove the falling component from anyone currently falling into this nest
        var query = EntityQueryEnumerator<ReplicatorNestFallingComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.FallingTarget == ent)
                RemCompDeferred<ReplicatorNestFallingComponent>(uid);
        }

        // Figure out who the queen is & which replicators belonging to this nest are still alive.
        EntityUid? queen = null;
        HashSet<Entity<ReplicatorComponent>> livingReplicators = [];
        var repQuery = EntityQueryEnumerator<ReplicatorComponent>();
        while (repQuery.MoveNext(out var uid, out var comp))
        {
            if (!_mobState.IsAlive(uid))
                continue;

            if (comp.MyNest != ent.Owner)
                continue;

            comp.MyNest = null;
            if (comp.Queen)
                queen = uid;

            livingReplicators.Add((uid, comp));
        }

        // if there are living replicators, select one and give the action to create a new nest.
        if (livingReplicators.Count > 0)
        {
            var queenEntity = queen is { } existingQueen
                ? livingReplicators.First(rep => rep.Owner == existingQueen)
                : _random.Pick(livingReplicators);
            var controlledReplicators = livingReplicators
                .Where(rep => TryComp<MindContainerComponent>(rep.Owner, out var mind) && mind.HasMind)
                .ToList();

            if ((!TryComp<MindContainerComponent>(queenEntity.Owner, out var queenMind) || !queenMind.HasMind) &&
                controlledReplicators.Count > 0)
                queenEntity = _random.Pick(controlledReplicators);

            foreach (var replicator in livingReplicators)
                replicator.Comp.Queen = false;

            queenEntity.Comp.Queen = true;
            queenEntity.Comp.RelatedReplicators = livingReplicators;

            var queenSource = queenEntity.Owner;
            EntityUid? replacedQueenSource = null;
            var finalLivingReplicators = new HashSet<Entity<ReplicatorComponent>>();
            var upgradedQueen = ForceUpgrade(queenEntity, queenEntity.Comp.FinalStage);

            if (upgradedQueen is { } upgradedQueenUid &&
                TryComp<ReplicatorComponent>(upgradedQueenUid, out var upgradedQueenComp))
            {
                queen = upgradedQueenUid;
                replacedQueenSource = queenSource;
                upgradedQueenComp.Queen = true;
                finalLivingReplicators.Add((upgradedQueenUid, upgradedQueenComp));

                if (TryComp<MindContainerComponent>(upgradedQueenUid, out var mindContainer) &&
                    mindContainer.Mind is { } mind)
                {
                    upgradedQueenComp.Actions.Add(_actionContainer.AddAction(mind, upgradedQueenComp.SpawnNewNestAction));

                    upgradedQueenComp.HasSpawnedNest = true;
                }
            }
            else
            {
                // A mindless takeover body cannot be replaced yet, but it must remain claimable.
                queen = queenSource;
            }

            EnsureComp<ReplicatorSignComponent>(queen.Value);

            // Downgrade the remaining living members. A temporarily mindless takeover body stays intact.
            foreach (var (uid, comp) in livingReplicators)
            {
                if (uid == replacedQueenSource || TerminatingOrDeleted(uid))
                    continue;

                Entity<ReplicatorComponent> activeReplicator = (uid, comp);
                if (uid != queen &&
                    ForceUpgrade((uid, comp), comp.FirstStage) is { } upgradedUid &&
                    TryComp<ReplicatorComponent>(upgradedUid, out var upgradedComp))
                {
                    activeReplicator = (upgradedUid, upgradedComp);
                }

                finalLivingReplicators.Add(activeReplicator);
            }

            if (TryComp<ReplicatorComponent>(queen.Value, out var finalQueenComp))
                finalQueenComp.RelatedReplicators = finalLivingReplicators;

            foreach (var replicator in finalLivingReplicators)
            {
                _movementMod.TryUpdateMovementSpeedModDuration(
                    replicator.Owner,
                    "HoleDestroyedSlowdownStatusEffect",
                    TimeSpan.FromSeconds(3),
                    0.8f);
                _popup.PopupEntity(
                    Loc.GetString("replicator-nest-destroyed"),
                    replicator.Owner,
                    replicator.Owner,
                    PopupType.LargeCaution);

                if (!_inventory.TryGetSlotEntity(replicator.Owner, "pocket1", out var pocket1) ||
                    !TryComp<PinpointerComponent>(pocket1, out var pinpointer))
                    continue;

                _pinpointer.SetTarget(pocket1.Value, queen, pinpointer);
            }
        }
    }

    private void OnRoundEndTextAppend(RoundEndTextAppendEvent args)
    {
        List<Entity<ReplicatorNestPointsStorageComponent>> nests = [];

        // get all the nests that have existed this round in a list
        var query = AllEntityQuery<ReplicatorNestPointsStorageComponent>();
        while (query.MoveNext(out var uid, out var comp))
            nests.Add((uid, comp));

        if (nests.Count == 0)
            return;

        // linebreak
        args.AddLine("");

        var totalPoints = 0;
        var totalSpawned = 0;
        HashSet<int> levels = [];
        List<string> locations = [];

        // generate a summary of locations, levels, points, and total spawned replicators across all nests
        foreach (var ent in nests)
        {
            var pointsStorage = ent.Comp;
            var location = Loc.GetString("replicator-location-unknown");
            var mapCoords = _xform.ToMapCoordinates(Transform(ent).Coordinates);
            if (_navMap.TryGetNearestBeacon(mapCoords, out var beacon, out _) && beacon?.Comp.Text != null)
                location = beacon?.Comp.Text!;

            locations.Add($"[color=#d70aa0]{location}[/color]");

            totalPoints += pointsStorage.TotalPoints / 10; // dividing by ten gives us a slightly more manageable number + keeps it consistent with pre-stackcount point calculation.

            totalSpawned += pointsStorage.TotalReplicators;

            levels.Add(pointsStorage.Level);
        }

        var conjunction = Loc.GetString("replicator-list-and");
        var locationsList = locations.Count switch
        {
            1 => $"{locations[0]}.",
            2 => $"{locations[0]} {conjunction} {locations[1]}.",
            _ => $"{string.Join(", ", locations.Take(locations.Count - 1))}, {conjunction} {locations[^1]}.",
        };
        var highestLevel = levels.Max();

        // then push that summary.
        args.AddLine(Loc.GetString("replicator-nest-end-of-round", ("location", locationsList), ("level", highestLevel), ("points", totalPoints), ("replicators", totalSpawned)));
        args.AddLine("");
    }
}
