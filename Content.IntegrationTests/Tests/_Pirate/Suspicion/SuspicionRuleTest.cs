// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Content.Client._Pirate.Suspicion.UserInterface.Widgets;
using Content.Client.UserInterface.Systems.Ghost.Widgets;
using Content.Goobstation.Common.CCVar;
using Content.IntegrationTests.Pair;
using Content.Server.GameTicking;
using Content.Server.Ghost;
using Content.Server.Mind;
using Content.Server.Preferences.Managers;
using Content.Server.Roles;
using Content.Shared._Shitmed.Restrict;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CCVar;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Damage.Systems;
using Content.Shared._Pirate.Suspicion.SuspicionGameRule.Components;
using Content.Shared.Ghost;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs.Systems;
using Content.Shared.NukeOps;
using Content.Shared.Preferences;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests._Pirate.Suspicion;

[TestFixture]
public sealed class SuspicionRuleTest
{
    [TestPrototypes]
    private const string LootPrototypes = """
        - type: entity
          id: TestSuspicionLootItem
        - type: entity
          id: TestSuspicionLootMarker
          components:
          - type: SuspicionLootSpawner
            table: !type:EntSelector
              id: TestSuspicionLootItem
            delay: 1
            offset: 0
        """;

    [Test]
    public async Task LootSpawnsOnlyOnTheActiveArenaAndStopsAtRoundEnd()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var outside = await pair.CreateTestMap();
        int CountLoot() => entities.EntityQuery<MetaDataComponent>().Count(meta => meta.EntityPrototype?.ID == "TestSuspicionLootItem");

        await server.WaitPost(() => entities.SpawnAtPosition("TestSuspicionLootMarker", outside.GridCoords));
        await pair.RunTicksSync(server.Timing.TickRate * 2);
        await server.WaitAssertion(() => Assert.That(CountLoot(), Is.Zero));

        await AddPlayer(pair, "ArenaLootOpponent");
        await StartPreparing(pair, "1v1CitySSS", 30);
        EntityCoordinates arena = default;
        await server.WaitPost(() =>
        {
            var player = pair.Player!.AttachedEntity!.Value;
            arena = entities.GetComponent<TransformComponent>(player).Coordinates;
            entities.SpawnAtPosition("TestSuspicionLootMarker", outside.GridCoords);
            entities.SpawnAtPosition("TestSuspicionLootMarker", arena);
        });
        await pair.RunTicksSync(server.Timing.TickRate * 2);
        await server.WaitAssertion(() => Assert.That(CountLoot(), Is.EqualTo(1)));

        await server.WaitPost(() =>
        {
            entities.SpawnAtPosition("TestSuspicionLootMarker", arena);
            server.System<GameTicker>().EndRound();
        });
        await pair.RunTicksSync(server.Timing.TickRate * 2);
        await server.WaitAssertion(() => Assert.That(CountLoot(), Is.EqualTo(1)));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ReconnectingAfterRoleLockAssignsInnocent()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        var second = await AddPlayer(pair, "LockPartner");
        var third = await AddPlayer(pair, "LockWitness");
        await StartPreparing(pair, "RooftopsSSS", 3);

        var disconnected = pair.Player!;
        var body = disconnected.AttachedEntity!.Value;
        var clientNetwork = pair.Client.ResolveDependency<IClientNetManager>();
        await pair.Client.WaitPost(() => clientNetwork.ClientDisconnect("Suspicion role lock test"));
        await pair.RunTicksSync(10);
        await server.WaitAssertion(() => Assert.That(server.PlayerMan.TryGetSessionById(disconnected.UserId, out _), Is.False));

        await pair.RunTicksSync(server.Timing.TickRate * 4);
        await server.WaitAssertion(() =>
        {
            Assert.That(GetRule(pair).GameState, Is.EqualTo(SuspicionGameState.InProgress));
            Assert.That(RoleOf(pair, body).Role, Is.EqualTo(SuspicionRole.Pending));
            var activeRoles = new[] { RoleOf(pair, second.AttachedEntity!.Value).Role, RoleOf(pair, third.AttachedEntity!.Value).Role };
            Assert.That(activeRoles, Is.EquivalentTo(new[] { SuspicionRole.Traitor, SuspicionRole.Detective }));
        });

        await Task.WhenAll(pair.Client.WaitIdleAsync(), server.WaitIdleAsync());
        pair.Client.SetConnectTarget(server);
        await pair.Client.WaitPost(() => clientNetwork.ClientConnect(null!, 0, disconnected.Name));
        await pair.RunTicksSync(20);
        await server.WaitAssertion(() =>
        {
            Assert.That(pair.Player?.UserId, Is.EqualTo(disconnected.UserId));
            Assert.That(pair.Player?.AttachedEntity, Is.EqualTo(body));
            Assert.That(RoleOf(pair, body).Role, Is.EqualTo(SuspicionRole.Innocent));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TraitorMarkersFollowPrivateViewerPermissions()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var partner = await AddPlayer(pair, "MarkerPartner");
        var witness = await AddPlayer(pair, "MarkerWitness");
        await StartPreparing(pair, "RooftopsSSS", 1);
        await pair.RunTicksSync(server.Timing.TickRate * 2);

        var viewer = pair.Player!;
        var body = viewer.AttachedEntity!.Value;
        var traitor = partner.AttachedEntity!.Value;
        var target = NetEntity.Invalid;
        var minds = server.System<MindSystem>();
        var ghosts = server.System<GhostSystem>();
        await server.WaitAssertion(() =>
        {
            Assert.That(GetRule(pair).GameState, Is.EqualTo(SuspicionGameState.InProgress));
            RoleOf(pair, traitor).Role = SuspicionRole.Traitor;
            RoleOf(pair, witness.AttachedEntity!.Value).Role = SuspicionRole.Innocent;
            server.System<SharedTransformSystem>().SetCoordinates(traitor, entities.GetComponent<TransformComponent>(body).Coordinates);
            target = entities.GetNetEntity(traitor);
            ReattachAs(SuspicionRole.Detective);
        });
        await AssertTraitorMarker(pair, target, false);

        await server.WaitAssertion(() => ReattachAs(SuspicionRole.Traitor));
        await AssertTraitorMarker(pair, target, true);

        await server.WaitAssertion(() =>
        {
            Assert.That(minds.TryGetMind(viewer.UserId, out var mind, out var mindComp), Is.True);
            Assert.That(ghosts.SpawnGhost((mind!.Value, mindComp), body, canReturn: true), Is.Not.Null);
        });
        await AssertTraitorMarker(pair, target, false);

        await server.WaitAssertion(() => minds.UnVisit(viewer));
        await AssertTraitorMarker(pair, target, true);
        await server.WaitAssertion(() => ReattachAs(SuspicionRole.Innocent));
        await AssertTraitorMarker(pair, target, false);

        await server.WaitAssertion(() =>
        {
            Assert.That(minds.TryGetMind(viewer.UserId, out var mind, out var mindComp), Is.True);
            Assert.That(ghosts.SpawnGhost((mind!.Value, mindComp), body), Is.Not.Null);
        });
        await AssertTraitorMarker(pair, target, true);
        await server.WaitAssertion(() => server.System<GameTicker>().EndRound());
        await AssertTraitorMarker(pair, target, false);
        await pair.CleanReturnAsync();

        void ReattachAs(SuspicionRole role)
        {
            RoleOf(pair, body).Role = role;
            Assert.That(server.PlayerMan.SetAttachedEntity(viewer, null), Is.True);
            Assert.That(server.PlayerMan.SetAttachedEntity(viewer, body), Is.True);
        }
    }

    [Test]
    public async Task SoftCriticalOpponentCanRecoverBeforeEitherSideWins()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        var opponent = await AddPlayer(pair, "RecoveryOpponent");
        await server.WaitPost(() => server.ResolveDependency<IServerPreferencesManager>()
            .SetProfile(pair.Player!.UserId, 0, HumanoidCharacterProfile.DefaultWithSpecies("Experiment")).Wait());
        await StartPreparing(pair, "1v1CitySSS", 1);
        await pair.RunTicksSync(server.Timing.TickRate * 2);

        var player = pair.Player!.AttachedEntity!.Value;
        await server.WaitAssertion(() =>
        {
            DamageChest(pair, player, 100, opponent.AttachedEntity);
            Assert.That(server.System<MobStateSystem>().IsSoftCritical(player), Is.True);
        });
        await pair.RunTicksSync(server.Timing.TickRate + 20);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<MobStateSystem>().IsSoftCritical(player), Is.True);
            Assert.That(GetRule(pair).GameState, Is.EqualTo(SuspicionGameState.InProgress));
            Assert.That(GetRule(pair).Winner, Is.Null);
            DamageChest(pair, player, -30);
            Assert.That(server.System<MobStateSystem>().IsAlive(player), Is.True);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task PreparationDeathDoesNotExcludeAReadyPlayer(bool visitingGhost)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        var opponent = await AddPlayer(pair, "PreparationDeathOpponent");
        await server.WaitPost(() => server.ResolveDependency<IServerPreferencesManager>()
            .SetProfile(pair.Player!.UserId, 0, HumanoidCharacterProfile.DefaultWithSpecies("Experiment")).Wait());
        await StartPreparing(pair, "1v1CitySSS", 3);
        var player = pair.Player!;
        var body = player.AttachedEntity!.Value;
        await server.WaitAssertion(() =>
        {
            DamageChest(pair, body, 250, opponent.AttachedEntity);
            Assert.That(server.System<MobStateSystem>().IsDead(body), Is.True);
            if (visitingGhost)
            {
                Assert.That(server.System<MindSystem>().TryGetMind(player.UserId, out var mind, out var mindComp), Is.True);
                Assert.That(server.System<GhostSystem>().SpawnGhost((mind!.Value, mindComp), body, canReturn: true), Is.Not.Null);
                Assert.That(player.AttachedEntity, Is.Not.EqualTo(body));
            }
        });
        await pair.RunTicksSync(server.Timing.TickRate * 4);
        await server.WaitAssertion(() =>
        {
            Assert.That(GetRule(pair).GameState, Is.EqualTo(SuspicionGameState.InProgress));
            Assert.That(player.AttachedEntity, Is.EqualTo(body));
            Assert.That(server.System<MobStateSystem>().IsAlive(body), Is.True);
            var roles = new[] { RoleOf(pair, body).Role, RoleOf(pair, opponent.AttachedEntity!.Value).Role };
            Assert.That(roles, Is.EquivalentTo(new[] { SuspicionRole.Traitor, SuspicionRole.Detective }));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PreparationHudAfterLobbyUsesActualDeadline()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        await AddPlayer(pair, "ClockPartner");
        await pair.RunTicksSync(pair.Server.Timing.TickRate * 12);
        await StartPreparing(pair, "1v1CitySSS", 30);
        await pair.RunTicksSync(20);
        await pair.RunUntilSynced();
        await pair.Client.WaitAssertion(() =>
        {
            var hud = GetHud(pair);
            hud.UpdateClock!.Invoke();
            Assert.That(hud.TimerText.Visible, Is.True);
            var remaining = TimeSpan.ParseExact(hud.TimerText.Text!, @"m\:ss", CultureInfo.InvariantCulture);
            Assert.That(remaining.TotalSeconds, Is.InRange(25, 30));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LateJoiningPostRoundObserverHasNoCountdown()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        await AddPlayer(pair, "LateClockFirst");
        await AddPlayer(pair, "LateClockSecond");
        await StartPreparing(pair, "1v1CitySSS", 1, readyClient: false);
        await pair.RunTicksSync(server.Timing.TickRate * 2);
        await server.WaitAssertion(() =>
        {
            var ticker = server.System<GameTicker>();
            Assert.That(pair.Player!.AttachedEntity, Is.Null);
            Assert.That(GetRule(pair).GameState, Is.EqualTo(SuspicionGameState.InProgress));
            ticker.EndRound();
            ticker.MakeJoinGame(pair.Player, EntityUid.Invalid);
        });
        await pair.RunTicksSync(20);
        await pair.RunUntilSynced();
        await server.WaitAssertion(() => Assert.That(server.EntMan.HasComponent<GhostComponent>(pair.Player!.AttachedEntity)));
        await pair.Client.WaitAssertion(() =>
        {
            var hud = GetHud(pair);
            hud.UpdateClock!.Invoke();
            Assert.That(hud.TimerText.Visible, Is.False);
        });
        await pair.CleanReturnAsync();
    }

    private static SSSStatusGui GetHud(TestPair pair)
    {
        var ui = pair.Client.ResolveDependency<IUserInterfaceManager>();
        var hud = ui.GetActiveUIWidgetOrNull<GhostGui>()?.Parent?.Children.OfType<SSSStatusGui>().SingleOrDefault();
        Assert.That(hud, Is.Not.Null);
        return hud!;
    }

    [Test]
    public async Task HealthHudMeasuresVitalDamage()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        await AddPlayer(pair, "VitalsOpponent");
        await server.WaitPost(() => server.ResolveDependency<IServerPreferencesManager>()
            .SetProfile(pair.Player!.UserId, 0, HumanoidCharacterProfile.DefaultWithSpecies("Human")).Wait());
        await StartPreparing(pair, "1v1CitySSS", 1);
        await pair.RunTicksSync(server.Timing.TickRate * 2);
        await pair.RunUntilSynced();

        var player = pair.Player!.AttachedEntity!.Value;
        await AssertMatchesServer();
        await server.WaitAssertion(() =>
        {
            var body = server.System<SharedBodySystem>();
            var arm = body.GetBodyChildrenOfType(player, BodyPartType.Arm).First().Id;
            var blunt = server.ProtoMan.Index<DamageTypePrototype>("Blunt");
            server.System<DamageableSystem>().ChangeDamage(arm, new DamageSpecifier(blunt, 20),
                ignoreResistances: true, canMiss: false);
            var damage = server.EntMan.GetComponent<DamageableComponent>(player);
            Assert.That(server.System<MobThresholdSystem>().CheckVitalDamage(player, damage), Is.LessThan(damage.TotalDamage));
        });
        await AssertMatchesServer();
        await server.WaitAssertion(() => DamageChest(pair, player, 20));
        await AssertMatchesServer();
        await pair.CleanReturnAsync();

        async Task AssertMatchesServer()
        {
            await pair.RunUntilSynced();
            // Bleeding can change health between the server tick and the client's next snapshot.
            var observed = new HashSet<float>();
            for (var tick = 0; tick < 12; tick++)
            {
                await server.WaitAssertion(() =>
                {
                    var thresholds = server.System<MobThresholdSystem>();
                    var max = thresholds.GetThresholdForState(player, MobState.Critical, server.EntMan.GetComponent<MobThresholdsComponent>(player));
                    var damage = server.EntMan.GetComponent<DamageableComponent>(player);
                    var health = (float) System.Math.Max(0, System.Math.Ceiling((max - thresholds.CheckVitalDamage(player, damage)).Double()));
                    Assert.That(health, Is.Positive);
                    observed.Add(health);
                });
                await pair.RunTicksSync(1);
            }
            await pair.Client.WaitAssertion(() => Assert.That(observed, Does.Contain(GetHud(pair).HealthBar.Value)));
        }

    }

    [Test]
    public async Task OniCanFireParticipantHeldGunWithoutChangingNativeGuns()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        await AddPlayer(pair, "OniPartner");
        await server.WaitPost(() => server.ResolveDependency<IServerPreferencesManager>()
            .SetProfile(pair.Player!.UserId, 0, HumanoidCharacterProfile.DefaultWithSpecies("Oni")).Wait());
        await StartPreparing(pair, "1v1CitySSS", 1);
        await pair.RunTicksSync(server.Timing.TickRate * 2);

        var arenaGun = EntityUid.Invalid;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var player = pair.Player!.AttachedEntity!.Value;
            var coordinates = entities.GetComponent<TransformComponent>(player).Coordinates;
            arenaGun = entities.SpawnAtPosition("WeaponPistolMk58", coordinates);
            Assert.That(entities.GetComponent<RestrictGunshotsByUserTagComponent>(arenaGun).DoesntContain.Any(tag => tag.Id == "Oni"));
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(player, arenaGun), Is.True);

            Assert.That(entities.GetComponent<RestrictGunshotsByUserTagComponent>(arenaGun).DoesntContain.Any(tag => tag.Id == "Oni"), Is.False);
        });
        // Selecting a new active weapon applies Pirate's normal hand-switch firing delay.
        await pair.RunTicksSync(server.Timing.TickRate * 2);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var player = pair.Player!.AttachedEntity!.Value;
            var coordinates = entities.GetComponent<TransformComponent>(player).Coordinates;
            var ordinaryGun = entities.SpawnAtPosition("WeaponPistolMk58", coordinates);
            Assert.That(entities.GetComponent<RestrictGunshotsByUserTagComponent>(ordinaryGun).DoesntContain.Any(tag => tag.Id == "Oni"));

            var shot = new ShotAttemptedEvent { User = player, Used = (arenaGun, entities.GetComponent<GunComponent>(arenaGun)) };
            entities.EventBus.RaiseLocalEvent(arenaGun, ref shot);
            Assert.That(shot.Cancelled, Is.False);
            shot = new ShotAttemptedEvent { User = player, Used = (ordinaryGun, entities.GetComponent<GunComponent>(ordinaryGun)) };
            entities.EventBus.RaiseLocalEvent(ordinaryGun, ref shot);
            Assert.That(shot.Cancelled, Is.True);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DirectLethalDamageFromCrewAwardsJesterVictory()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        var attacker = await AddPlayer(pair, "JesterAttacker");
        var traitor = await AddPlayer(pair, "JesterWitness");
        await server.WaitPost(() => server.ResolveDependency<IServerPreferencesManager>()
            .SetProfile(pair.Player!.UserId, 0, HumanoidCharacterProfile.DefaultWithSpecies("Experiment")).Wait());
        await StartPreparing(pair, "RooftopsSSS", 1);
        await pair.RunTicksSync(server.Timing.TickRate * 2);

        await server.WaitAssertion(() =>
        {
            var victim = pair.Player!.AttachedEntity!.Value;
            var killer = attacker.AttachedEntity!.Value;
            var jester = RoleOf(pair, victim);
            jester.Role = SuspicionRole.Wildcard;
            jester.SubRole = SuspicionSubRole.Jester;
            RoleOf(pair, killer).Role = SuspicionRole.Detective;
            RoleOf(pair, traitor.AttachedEntity!.Value).Role = SuspicionRole.Traitor;
            Assert.That(server.System<MobStateSystem>().IsAlive(victim), Is.True);

            DamageChest(pair, victim, 250, killer);

            Assert.That(server.System<MobStateSystem>().IsDead(victim), Is.True);
            Assert.That(GetRule(pair).GameState, Is.EqualTo(SuspicionGameState.PostRound));
            Assert.That(GetRule(pair).Winner, Is.EqualTo("pirate-ttt-win-jester"));
        });
        await pair.CleanReturnAsync();
    }

    private static async Task<ICommonSession> AddPlayer(TestPair pair, string name)
    {
        var session = await pair.Server.AddDummySession(name);
        await pair.Server.WaitPost(() =>
        {
            if (session.Status == SessionStatus.Connected)
                pair.Server.PlayerMan.JoinGame(session);
        });
        await pair.RunTicksSync(5);
        return session;
    }

    private static async Task StartPreparing(TestPair pair, string map, int seconds, bool readyClient = true)
    {
        var server = pair.Server;
        await pair.Client.WaitPost(() => pair.Client.CfgMan.SetCVar(GoobCVars.RoundIntroEnabled, false));
        await server.WaitAssertion(() =>
        {
            var ticker = server.System<GameTicker>();
            server.CfgMan.SetCVar(CCVars.SSSPreparingDuration, seconds);
            server.CfgMan.SetCVar(CCVars.SSSWildcardChance, 0f);
            server.CfgMan.SetCVar(CCVars.GameMap, map);
            ticker.SetGamePreset("Suspicion", resetDelay: 1);
            ticker.ToggleReadyAll(true);
            if (!readyClient)
                ticker.ToggleReady(pair.Player!, false);
            ticker.StartRound();
            Assert.That(GetRule(pair).GameState, Is.EqualTo(SuspicionGameState.Preparing));
        });
    }

    private static SuspicionRuleComponent GetRule(TestPair pair)
        => pair.Server.EntMan.EntityQuery<SuspicionRuleComponent>().Single();

    private static SuspicionRoleComponent RoleOf(TestPair pair, EntityUid body)
    {
        var server = pair.Server;
        Assert.That(server.System<MindSystem>().TryGetMind(body, out var mind, out _), Is.True);
        Assert.That(server.System<RoleSystem>().MindHasRole<SuspicionRoleComponent>(mind, out var role), Is.True);
        return role!.Value.Comp2;
    }

    private static void DamageChest(TestPair pair, EntityUid body, int amount, EntityUid? attacker = null)
    {
        var server = pair.Server;
        var chest = server.System<SharedBodySystem>().GetBodyChildrenOfType(body, BodyPartType.Chest).Single().Id;
        var blunt = server.ProtoMan.Index<DamageTypePrototype>("Blunt");
        server.System<DamageableSystem>().ChangeDamage(chest, new DamageSpecifier(blunt, amount),
            ignoreResistances: true, origin: attacker, canMiss: false);
    }

    private static async Task AssertTraitorMarker(TestPair pair, NetEntity target, bool expected)
    {
        await pair.RunTicksSync(15);
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.EntMan;
            Assert.That(entities.TryGetEntity(target, out var entity), Is.True);
            Assert.That(entities.HasComponent<NukeOperativeComponent>(entity!.Value), Is.False);
            var icons = new List<StatusIconData>();
            var args = new GetStatusIconsEvent(entity.Value, icons);
            entities.EventBus.RaiseLocalEvent(entity.Value, ref args, true);
            Assert.That(icons.OfType<FactionIconPrototype>().Any(icon => icon.ID == "SyndicateFaction"), Is.EqualTo(expected));
        });
    }
}
