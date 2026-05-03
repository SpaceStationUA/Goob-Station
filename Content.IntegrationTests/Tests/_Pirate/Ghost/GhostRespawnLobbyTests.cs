using System;
using System.Linq;
using System.Threading.Tasks;
using Content.Client.UserInterface.Systems.Ghost.Widgets;
using Content.Goobstation.Common.Cloning;
using Content.Goobstation.Server.MisandryBox.Mind;
using Content.Goobstation.Shared.MisandryBox.Thunderdome;
using Content.IntegrationTests.Pair;
using Content.Server._Pirate.Ghost;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Mind;
using Content.Shared.Bed.Cryostorage;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Pirate.Ghost;

[TestFixture]
public sealed class GhostRespawnLobbyTests
{
    private const string GhostRoleProtoId = "PirateGhostRespawnTestRole";

    [TestPrototypes]
    private const string Prototypes = $$"""
        - type: entity
          id: {{GhostRoleProtoId}}
          components:
          - type: MindContainer
          - type: GhostRole
          - type: GhostTakeoverAvailable
          - type: MobState
        """;

    [Test]
    public async Task CrewGhostButtonCountsDownAndRespawnsToLobby()
    {
        var delay = TimeSpan.FromSeconds(1);
        await using var pair = await SetupRoundPair(delay);
        var userId = pair.Client.User!.Value;
        var respawnSystem = pair.Server.System<PirateGhostRespawnSystem>();
        var ticker = pair.Server.System<GameTicker>();
        var ui = pair.Client.ResolveDependency<IUserInterfaceManager>();

        await Ghost(pair);

        await pair.Server.WaitAssertion(() =>
        {
            var state = respawnSystem.GetDebugState(userId);
            Assert.That(state.HasCrewCycle, Is.True);
            Assert.That(state.TimerArmed, Is.True);
        });

        string disabledText = string.Empty;
        await pair.Client.WaitAssertion(() =>
        {
            var button = GetReturnToRoundButton(ui);
            Assert.That(button.Disabled, Is.True);
            Assert.That(button.Text, Is.Not.Null.And.Not.Empty);
            Assert.That(button.Text!.Any(char.IsDigit), Is.True);
            disabledText = button.Text;
        });

        await pair.RunTicksSync(GetTicksForDelay(pair, delay) + 5);

        Button? enabledButton = null;
        string enabledText = string.Empty;
        await pair.Client.WaitAssertion(() =>
        {
            enabledButton = GetReturnToRoundButton(ui);
            Assert.That(enabledButton.Disabled, Is.False);
            enabledText = enabledButton.Text;
        });

        Assert.That(enabledText, Is.Not.EqualTo(disabledText));

        await ClickControl(pair, enabledButton!);
        await pair.RunTicksSync(10);

        await pair.Server.WaitAssertion(() =>
        {
            var session = pair.Server.PlayerMan.Sessions.Single();
            Assert.That(ticker.PlayerGameStatuses[userId], Is.EqualTo(PlayerGameStatus.NotReadyToPlay));
            Assert.That(session.AttachedEntity, Is.Null);
        });

        await pair.Server.WaitPost(() =>
        {
            var session = pair.Server.PlayerMan.Sessions.Single();
            ticker.JoinAsObserver(session);
        });
        await pair.RunTicksSync(10);

        await pair.Server.WaitAssertion(() =>
        {
            var session = pair.Server.PlayerMan.Sessions.Single();
            var state = respawnSystem.GetDebugState(userId);
            var availability = respawnSystem.GetDebugAvailability(userId);
            Assert.That(ticker.PlayerGameStatuses[userId], Is.EqualTo(PlayerGameStatus.JoinedGame));
            Assert.That(session.AttachedEntity, Is.Not.Null);
            Assert.That(pair.Server.EntMan.HasComponent<GhostComponent>(session.AttachedEntity!.Value), Is.True);
            Assert.That(state.HasCrewCycle, Is.False);
            Assert.That(availability.CanRespawn, Is.True);
        });

        await pair.Client.WaitAssertion(() =>
        {
            var button = GetReturnToRoundButton(ui);
            Assert.That(button.Disabled, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CriticalGhostStartsTimer()
    {
        var delay = TimeSpan.FromSeconds(5);
        await using var pair = await SetupRoundPair(delay, ghostKillCrit: false);
        var userId = pair.Client.User!.Value;
        var respawnSystem = pair.Server.System<PirateGhostRespawnSystem>();
        var mobState = pair.Server.System<MobStateSystem>();

        await pair.Server.WaitPost(() =>
        {
            var entity = pair.Server.PlayerMan.Sessions.Single().AttachedEntity!.Value;
            mobState.ChangeMobState(entity, MobState.Critical);
        });
        await pair.RunTicksSync(5);

        await Ghost(pair);

        await pair.Server.WaitAssertion(() =>
        {
            var state = respawnSystem.GetDebugState(userId);
            var availability = respawnSystem.GetDebugAvailability(userId);
            Assert.That(state.TimerArmed, Is.True);
            Assert.That(availability.CanRespawn, Is.False);
            Assert.That(availability.RemainingTime, Is.GreaterThan(TimeSpan.Zero));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeadGhostStartsTimer()
    {
        var delay = TimeSpan.FromSeconds(5);
        await using var pair = await SetupRoundPair(delay);
        var userId = pair.Client.User!.Value;
        var respawnSystem = pair.Server.System<PirateGhostRespawnSystem>();
        var mobState = pair.Server.System<MobStateSystem>();

        await pair.Server.WaitPost(() =>
        {
            var entity = pair.Server.PlayerMan.Sessions.Single().AttachedEntity!.Value;
            mobState.ChangeMobState(entity, MobState.Dead);
        });
        await pair.RunTicksSync(5);

        await Ghost(pair);

        await pair.Server.WaitAssertion(() =>
        {
            var state = respawnSystem.GetDebugState(userId);
            var availability = respawnSystem.GetDebugAvailability(userId);
            Assert.That(state.TimerArmed, Is.True);
            Assert.That(availability.CanRespawn, Is.False);
            Assert.That(availability.RemainingTime, Is.GreaterThan(TimeSpan.Zero));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ObserverRespawnIsImmediate()
    {
        await using var pair = await SetupObserverPair();
        var userId = pair.Client.User!.Value;
        var respawnSystem = pair.Server.System<PirateGhostRespawnSystem>();
        var ui = pair.Client.ResolveDependency<IUserInterfaceManager>();

        await pair.Server.WaitAssertion(() =>
        {
            var state = respawnSystem.GetDebugState(userId);
            var availability = respawnSystem.GetDebugAvailability(userId);
            Assert.That(state.HasCrewCycle, Is.False);
            Assert.That(availability.CanRespawn, Is.True);
        });

        await pair.Client.WaitAssertion(() =>
        {
            var button = GetReturnToRoundButton(ui);
            Assert.That(button.Disabled, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CustomCrewSpawnWithoutPlayerSpawnCompleteStartsTimer()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
            Dirty = true,
            InLobby = true
        });
        var userId = pair.Client.User!.Value;
        var respawnSystem = pair.Server.System<PirateGhostRespawnSystem>();
        var ticker = pair.Server.System<GameTicker>();
        var mindSystem = pair.Server.System<MindSystem>();
        var mapSystem = pair.Server.System<SharedMapSystem>();
        var entMan = pair.Server.EntMan;
        MapId mapId = default;

        await pair.Server.WaitAssertion(() =>
        {
            var state = respawnSystem.GetDebugState(userId);
            Assert.That(state.HasCrewCycle, Is.False);
        });

        await pair.Server.WaitPost(() =>
        {
            pair.Server.CfgMan.SetCVar(CCVars.GhostRespawnDelay, 5);
            mapSystem.CreateMap(out mapId);

            var session = pair.Server.PlayerMan.Sessions.Single();
            var body = entMan.SpawnEntity("MobHuman", new MapCoordinates(0, 0, mapId));
            var (mindId, _) = mindSystem.CreateMind(session.UserId, session.Name);
            mindSystem.SetUserId(mindId, session.UserId);
            ticker.PlayerJoinGame(session);
            mindSystem.TransferTo(mindId, body);
        });
        await pair.RunTicksSync(10);

        await pair.Server.WaitAssertion(() =>
        {
            var session = pair.Server.PlayerMan.Sessions.Single();
            var state = respawnSystem.GetDebugState(userId);
            Assert.That(session.AttachedEntity, Is.Not.Null);
            Assert.That(state.HasCrewCycle, Is.True);
            Assert.That(state.TimerArmed, Is.False);
        });

        await Ghost(pair);

        await pair.Server.WaitAssertion(() =>
        {
            var state = respawnSystem.GetDebugState(userId);
            var availability = respawnSystem.GetDebugAvailability(userId);
            Assert.That(state.TimerArmed, Is.True);
            Assert.That(availability.CanRespawn, Is.False);
            Assert.That(availability.RemainingTime, Is.GreaterThan(TimeSpan.Zero));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CryoGhostRespawnIsImmediate()
    {
        await using var pair = await SetupRoundPair(TimeSpan.FromSeconds(5));
        var userId = pair.Client.User!.Value;
        var respawnSystem = pair.Server.System<PirateGhostRespawnSystem>();
        var ui = pair.Client.ResolveDependency<IUserInterfaceManager>();

        await pair.Server.WaitPost(() =>
        {
            var entity = pair.Server.PlayerMan.Sessions.Single().AttachedEntity!.Value;
            pair.Server.EntMan.EnsureComponent<CryostorageContainedComponent>(entity);
        });
        await pair.RunTicksSync(5);

        await Ghost(pair);

        await pair.Server.WaitAssertion(() =>
        {
            var availability = respawnSystem.GetDebugAvailability(userId);
            Assert.That(availability.CanRespawn, Is.True);
        });

        await pair.Client.WaitAssertion(() =>
        {
            var button = GetReturnToRoundButton(ui);
            Assert.That(button.Disabled, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task VisitingGhostDoesNotArmRespawnTimer()
    {
        await using var pair = await SetupRoundPair(TimeSpan.FromSeconds(5));
        var userId = pair.Client.User!.Value;
        var respawnSystem = pair.Server.System<PirateGhostRespawnSystem>();
        var mindSystem = pair.Server.System<MindSystem>();
        var ui = pair.Client.ResolveDependency<IUserInterfaceManager>();
        var entMan = pair.Server.EntMan;

        await pair.Server.WaitPost(() =>
        {
            var session = pair.Server.PlayerMan.Sessions.Single();
            Assert.That(mindSystem.TryGetMind(session.UserId, out EntityUid? mindId, out var mind), Is.True);
            var body = mind!.OwnedEntity!.Value;
            var coords = entMan.GetComponent<TransformComponent>(body).Coordinates;
            var ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, coords);
            mindSystem.Visit(mindId!.Value, ghost, mind);
        });
        await pair.RunTicksSync(10);

        await pair.Server.WaitAssertion(() =>
        {
            var session = pair.Server.PlayerMan.Sessions.Single();
            var state = respawnSystem.GetDebugState(userId);
            Assert.That(session.AttachedEntity, Is.Not.Null);
            Assert.That(entMan.HasComponent<VisitingMindComponent>(session.AttachedEntity!.Value), Is.True);
            Assert.That(state.TimerArmed, Is.False);
        });

        await pair.Client.WaitAssertion(() =>
        {
            var button = GetReturnToRoundButton(ui);
            Assert.That(button.Visible, Is.False);
        });

        await pair.Server.WaitPost(() =>
        {
            var session = pair.Server.PlayerMan.Sessions.Single();
            mindSystem.UnVisit(session);
        });
        await pair.RunTicksSync(10);

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CryoOverridesExistingRespawnTimer()
    {
        await using var pair = await SetupRoundPair(TimeSpan.FromSeconds(5));
        var userId = pair.Client.User!.Value;
        var respawnSystem = pair.Server.System<PirateGhostRespawnSystem>();
        var mindSystem = pair.Server.System<MindSystem>();
        var mobState = pair.Server.System<MobStateSystem>();
        var ui = pair.Client.ResolveDependency<IUserInterfaceManager>();

        await pair.Server.WaitPost(() =>
        {
            var entity = pair.Server.PlayerMan.Sessions.Single().AttachedEntity!.Value;
            mobState.ChangeMobState(entity, MobState.Dead);
        });
        await pair.RunTicksSync(5);

        await Ghost(pair);

        await pair.Server.WaitAssertion(() =>
        {
            var state = respawnSystem.GetDebugState(userId);
            Assert.That(state.TimerArmed, Is.True);
        });

        await pair.Server.WaitPost(() =>
        {
            var session = pair.Server.PlayerMan.Sessions.Single();
            Assert.That(mindSystem.TryGetMind(session.UserId, out EntityUid? mindId, out var mind), Is.True);
            var body = mind!.OwnedEntity!.Value;
            mobState.ChangeMobState(body, MobState.Alive);
            mindSystem.UnVisit(mindId!.Value, mind);
            pair.Server.EntMan.EnsureComponent<CryostorageContainedComponent>(body);
        });
        await pair.RunTicksSync(10);

        await Ghost(pair);

        await pair.Server.WaitAssertion(() =>
        {
            var availability = respawnSystem.GetDebugAvailability(userId);
            Assert.That(availability.CanRespawn, Is.True);
        });

        await pair.Client.WaitAssertion(() =>
        {
            var button = GetReturnToRoundButton(ui);
            Assert.That(button.Disabled, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RevivedCrewGhostRestartsRespawnTimer()
    {
        var delay = TimeSpan.FromSeconds(5);
        await using var pair = await SetupRoundPair(delay);
        var userId = pair.Client.User!.Value;
        var respawnSystem = pair.Server.System<PirateGhostRespawnSystem>();
        var mindSystem = pair.Server.System<MindSystem>();
        var mobState = pair.Server.System<MobStateSystem>();

        await pair.Server.WaitPost(() =>
        {
            var entity = pair.Server.PlayerMan.Sessions.Single().AttachedEntity!.Value;
            mobState.ChangeMobState(entity, MobState.Dead);
        });
        await pair.RunTicksSync(5);

        await Ghost(pair);

        var originalAvailableAt = TimeSpan.Zero;
        await pair.Server.WaitAssertion(() =>
        {
            var state = respawnSystem.GetDebugState(userId);
            Assert.That(state.TimerArmed, Is.True);
            originalAvailableAt = state.RespawnAvailableAt;
        });

        await pair.RunTicksSync(GetTicksForDelay(pair, delay) + 5);

        await pair.Server.WaitPost(() =>
        {
            var session = pair.Server.PlayerMan.Sessions.Single();
            Assert.That(mindSystem.TryGetMind(session.UserId, out EntityUid? mindId, out var mind), Is.True);
            var body = mind!.OwnedEntity!.Value;
            mobState.ChangeMobState(body, MobState.Alive);
            mindSystem.UnVisit(mindId!.Value, mind);
        });
        await pair.RunTicksSync(10);

        await pair.Server.WaitAssertion(() =>
        {
            var session = pair.Server.PlayerMan.Sessions.Single();
            var state = respawnSystem.GetDebugState(userId);
            Assert.That(session.AttachedEntity, Is.Not.Null);
            Assert.That(pair.Server.EntMan.HasComponent<GhostComponent>(session.AttachedEntity!.Value), Is.False);
            Assert.That(state.TimerArmed, Is.False);
        });

        await pair.Server.WaitPost(() =>
        {
            var entity = pair.Server.PlayerMan.Sessions.Single().AttachedEntity!.Value;
            mobState.ChangeMobState(entity, MobState.Dead);
        });
        await pair.RunTicksSync(5);

        await Ghost(pair);

        await pair.Server.WaitAssertion(() =>
        {
            var state = respawnSystem.GetDebugState(userId);
            var availability = respawnSystem.GetDebugAvailability(userId);
            Assert.That(state.TimerArmed, Is.True);
            Assert.That(state.RespawnAvailableAt, Is.GreaterThan(originalAvailableAt));
            Assert.That(availability.CanRespawn, Is.False);
            Assert.That(availability.RemainingTime, Is.GreaterThan(TimeSpan.Zero));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RevivedCrewDyingAgainWhileStillGhostRearmsRespawnTimer()
    {
        var delay = TimeSpan.FromSeconds(5);
        await using var pair = await SetupRoundPair(delay);
        var userId = pair.Client.User!.Value;
        var respawnSystem = pair.Server.System<PirateGhostRespawnSystem>();
        var mindSystem = pair.Server.System<MindSystem>();
        var mobState = pair.Server.System<MobStateSystem>();
        var ui = pair.Client.ResolveDependency<IUserInterfaceManager>();

        await pair.Server.WaitPost(() =>
        {
            var entity = pair.Server.PlayerMan.Sessions.Single().AttachedEntity!.Value;
            mobState.ChangeMobState(entity, MobState.Dead);
        });
        await pair.RunTicksSync(5);

        await Ghost(pair);

        await pair.Server.WaitAssertion(() =>
        {
            var state = respawnSystem.GetDebugState(userId);
            Assert.That(state.TimerArmed, Is.True);
        });

        await pair.Server.WaitPost(() =>
        {
            var session = pair.Server.PlayerMan.Sessions.Single();
            Assert.That(mindSystem.TryGetMind(session.UserId, out _, out var mind), Is.True);
            var body = mind!.OwnedEntity!.Value;
            mobState.ChangeMobState(body, MobState.Alive);
        });
        await pair.RunTicksSync(10);

        await pair.Server.WaitAssertion(() =>
        {
            var state = respawnSystem.GetDebugState(userId);
            Assert.That(state.TimerArmed, Is.False);
        });

        await pair.Client.WaitAssertion(() =>
        {
            var button = GetReturnToRoundButton(ui);
            Assert.That(button.Visible, Is.False);
        });

        await pair.Server.WaitPost(() =>
        {
            var session = pair.Server.PlayerMan.Sessions.Single();
            Assert.That(mindSystem.TryGetMind(session.UserId, out _, out var mind), Is.True);
            var body = mind!.OwnedEntity!.Value;
            mobState.ChangeMobState(body, MobState.Dead);
        });
        await pair.RunTicksSync(10);

        await pair.Server.WaitAssertion(() =>
        {
            var state = respawnSystem.GetDebugState(userId);
            var availability = respawnSystem.GetDebugAvailability(userId);
            Assert.That(state.TimerArmed, Is.True);
            Assert.That(availability.CanRespawn, Is.False);
            Assert.That(availability.RemainingTime, Is.GreaterThan(TimeSpan.Zero));
        });

        await pair.Client.WaitAssertion(() =>
        {
            var button = GetReturnToRoundButton(ui);
            Assert.That(button.Visible, Is.True);
            Assert.That(button.Disabled, Is.True);
            Assert.That(button.Text, Is.Not.Null.And.Not.Empty);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClonedCrewGhostRestartsRespawnTimer()
    {
        var delay = TimeSpan.FromSeconds(5);
        await using var pair = await SetupRoundPair(delay);
        var userId = pair.Client.User!.Value;
        var respawnSystem = pair.Server.System<PirateGhostRespawnSystem>();
        var mindSystem = pair.Server.System<MindSystem>();
        var mobState = pair.Server.System<MobStateSystem>();
        var entMan = pair.Server.EntMan;

        await pair.Server.WaitPost(() =>
        {
            var entity = pair.Server.PlayerMan.Sessions.Single().AttachedEntity!.Value;
            mobState.ChangeMobState(entity, MobState.Dead);
        });
        await pair.RunTicksSync(5);

        await Ghost(pair);

        var originalAvailableAt = TimeSpan.Zero;
        await pair.Server.WaitAssertion(() =>
        {
            var state = respawnSystem.GetDebugState(userId);
            Assert.That(state.TimerArmed, Is.True);
            originalAvailableAt = state.RespawnAvailableAt;
        });

        await pair.RunTicksSync(GetTicksForDelay(pair, delay) + 5);

        await pair.Server.WaitPost(() =>
        {
            var session = pair.Server.PlayerMan.Sessions.Single();
            Assert.That(mindSystem.TryGetMind(session.UserId, out EntityUid? mindId, out var mind), Is.True);
            var originalBody = mind!.OwnedEntity!.Value;
            var coords = entMan.GetComponent<TransformComponent>(originalBody).Coordinates;
            var clone = entMan.SpawnEntity("MobHuman", coords);
            mindSystem.TransferTo(mindId!.Value, clone, ghostCheckOverride: true, mind: mind);

            var ev = new TransferredToCloneEvent(clone);
            entMan.EventBus.RaiseLocalEvent(originalBody, ref ev);
        });
        await pair.RunTicksSync(10);

        await pair.Server.WaitAssertion(() =>
        {
            var session = pair.Server.PlayerMan.Sessions.Single();
            var state = respawnSystem.GetDebugState(userId);
            Assert.That(session.AttachedEntity, Is.Not.Null);
            Assert.That(pair.Server.EntMan.HasComponent<GhostComponent>(session.AttachedEntity!.Value), Is.False);
            Assert.That(state.TimerArmed, Is.False);
        });

        await pair.Server.WaitPost(() =>
        {
            var entity = pair.Server.PlayerMan.Sessions.Single().AttachedEntity!.Value;
            mobState.ChangeMobState(entity, MobState.Dead);
        });
        await pair.RunTicksSync(5);

        await Ghost(pair);

        var cloneAvailableAt = TimeSpan.Zero;
        await pair.Server.WaitAssertion(() =>
        {
            var state = respawnSystem.GetDebugState(userId);
            var availability = respawnSystem.GetDebugAvailability(userId);
            Assert.That(state.TimerArmed, Is.True);
            Assert.That(state.RespawnAvailableAt, Is.GreaterThan(originalAvailableAt));
            Assert.That(availability.CanRespawn, Is.False);
            Assert.That(availability.RemainingTime, Is.GreaterThan(TimeSpan.Zero));
            cloneAvailableAt = state.RespawnAvailableAt;
        });

        await pair.RunTicksSync(GetTicksForDelay(pair, delay) + 5);

        await pair.Server.WaitPost(() =>
        {
            var session = pair.Server.PlayerMan.Sessions.Single();
            Assert.That(mindSystem.TryGetMind(session.UserId, out EntityUid? mindId, out var mind), Is.True);
            var body = mind!.OwnedEntity!.Value;
            mobState.ChangeMobState(body, MobState.Alive);
            mindSystem.UnVisit(mindId!.Value, mind);
        });
        await pair.RunTicksSync(10);

        await pair.Server.WaitAssertion(() =>
        {
            var state = respawnSystem.GetDebugState(userId);
            Assert.That(state.TimerArmed, Is.False);
        });

        await pair.Server.WaitPost(() =>
        {
            var entity = pair.Server.PlayerMan.Sessions.Single().AttachedEntity!.Value;
            mobState.ChangeMobState(entity, MobState.Dead);
        });
        await pair.RunTicksSync(5);

        await Ghost(pair);

        await pair.Server.WaitAssertion(() =>
        {
            var state = respawnSystem.GetDebugState(userId);
            var availability = respawnSystem.GetDebugAvailability(userId);
            Assert.That(state.TimerArmed, Is.True);
            Assert.That(state.RespawnAvailableAt, Is.GreaterThan(cloneAvailableAt));
            Assert.That(availability.CanRespawn, Is.False);
            Assert.That(availability.RemainingTime, Is.GreaterThan(TimeSpan.Zero));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonCloneTransferredCrewGhostRestartsRespawnTimer()
    {
        var delay = TimeSpan.FromSeconds(5);
        await using var pair = await SetupRoundPair(delay);
        var userId = pair.Client.User!.Value;
        var respawnSystem = pair.Server.System<PirateGhostRespawnSystem>();
        var mindSystem = pair.Server.System<MindSystem>();
        var mobState = pair.Server.System<MobStateSystem>();
        var entMan = pair.Server.EntMan;

        await pair.Server.WaitPost(() =>
        {
            var entity = pair.Server.PlayerMan.Sessions.Single().AttachedEntity!.Value;
            mobState.ChangeMobState(entity, MobState.Dead);
        });
        await pair.RunTicksSync(5);

        await Ghost(pair);

        var originalAvailableAt = TimeSpan.Zero;
        await pair.Server.WaitAssertion(() =>
        {
            var state = respawnSystem.GetDebugState(userId);
            Assert.That(state.TimerArmed, Is.True);
            originalAvailableAt = state.RespawnAvailableAt;
        });

        await pair.RunTicksSync(GetTicksForDelay(pair, delay) + 5);

        await pair.Server.WaitPost(() =>
        {
            var session = pair.Server.PlayerMan.Sessions.Single();
            Assert.That(mindSystem.TryGetMind(session.UserId, out EntityUid? mindId, out var mind), Is.True);
            var originalBody = mind!.OwnedEntity!.Value;
            var coords = entMan.GetComponent<TransformComponent>(originalBody).Coordinates;
            var replacement = entMan.SpawnEntity("MobHuman", coords);
            mindSystem.TransferTo(mindId!.Value, replacement, ghostCheckOverride: true, mind: mind);
        });
        await pair.RunTicksSync(10);

        await pair.Server.WaitAssertion(() =>
        {
            var session = pair.Server.PlayerMan.Sessions.Single();
            var state = respawnSystem.GetDebugState(userId);
            Assert.That(session.AttachedEntity, Is.Not.Null);
            Assert.That(pair.Server.EntMan.HasComponent<GhostComponent>(session.AttachedEntity!.Value), Is.False);
            Assert.That(state.TimerArmed, Is.False);
        });

        await pair.Server.WaitPost(() =>
        {
            var entity = pair.Server.PlayerMan.Sessions.Single().AttachedEntity!.Value;
            mobState.ChangeMobState(entity, MobState.Dead);
        });
        await pair.RunTicksSync(5);

        await Ghost(pair);

        await pair.Server.WaitAssertion(() =>
        {
            var state = respawnSystem.GetDebugState(userId);
            var availability = respawnSystem.GetDebugAvailability(userId);
            Assert.That(state.TimerArmed, Is.True);
            Assert.That(state.RespawnAvailableAt, Is.GreaterThan(originalAvailableAt));
            Assert.That(availability.CanRespawn, Is.False);
            Assert.That(availability.RemainingTime, Is.GreaterThan(TimeSpan.Zero));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DisconnectReconnectKeepsRespawnTimer()
    {
        var delay = TimeSpan.FromSeconds(5);
        await using var pair = await SetupRoundPair(delay);
        var userId = pair.Client.User!.Value;
        var respawnSystem = pair.Server.System<PirateGhostRespawnSystem>();
        var ui = pair.Client.ResolveDependency<IUserInterfaceManager>();

        await Ghost(pair);

        var availableAt = TimeSpan.Zero;
        await pair.Server.WaitAssertion(() =>
        {
            var state = respawnSystem.GetDebugState(userId);
            Assert.That(state.TimerArmed, Is.True);
            availableAt = state.RespawnAvailableAt;
        });

        await DisconnectReconnect(pair);
        await pair.RunTicksSync(10);

        await pair.Server.WaitAssertion(() =>
        {
            var state = respawnSystem.GetDebugState(userId);
            Assert.That(state.TimerArmed, Is.True);
            Assert.That(state.RespawnAvailableAt, Is.EqualTo(availableAt));
        });

        await pair.Client.WaitAssertion(() =>
        {
            var button = GetReturnToRoundButton(ui);
            Assert.That(button.Disabled, Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GhostRoleGhostDoesNotResetRespawnTimer()
    {
        await using var pair = await SetupRoundPair(TimeSpan.FromSeconds(5));
        var userId = pair.Client.User!.Value;
        var respawnSystem = pair.Server.System<PirateGhostRespawnSystem>();
        var ghostRoleSystem = pair.Server.System<GhostRoleSystem>();
        var entMan = pair.Server.EntMan;

        await Ghost(pair);

        var originalAvailableAt = TimeSpan.Zero;
        await pair.Server.WaitAssertion(() =>
        {
            var state = respawnSystem.GetDebugState(userId);
            Assert.That(state.TimerArmed, Is.True);
            originalAvailableAt = state.RespawnAvailableAt;
        });

        await pair.Server.WaitPost(() =>
        {
            var session = pair.Server.PlayerMan.Sessions.Single();
            var roleEntity = entMan.SpawnEntity(GhostRoleProtoId, MapCoordinates.Nullspace);
            var identifier = entMan.GetComponent<GhostRoleComponent>(roleEntity).Identifier;
            ghostRoleSystem.Takeover(session, identifier);
        });
        await pair.RunTicksSync(10);

        await Ghost(pair);

        await pair.Server.WaitAssertion(() =>
        {
            var state = respawnSystem.GetDebugState(userId);
            Assert.That(state.RespawnAvailableAt, Is.EqualTo(originalAvailableAt));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ThunderdomeGhostDoesNotResetRespawnTimer()
    {
        await using var pair = await SetupRoundPair(TimeSpan.FromSeconds(5));
        var userId = pair.Client.User!.Value;
        var respawnSystem = pair.Server.System<PirateGhostRespawnSystem>();
        var tempMindSystem = pair.Server.System<TemporaryMindSystem>();
        var entMan = pair.Server.EntMan;

        await Ghost(pair);

        var originalAvailableAt = TimeSpan.Zero;
        await pair.Server.WaitAssertion(() =>
        {
            var state = respawnSystem.GetDebugState(userId);
            Assert.That(state.TimerArmed, Is.True);
            originalAvailableAt = state.RespawnAvailableAt;
        });

        await pair.Server.WaitPost(() =>
        {
            var session = pair.Server.PlayerMan.Sessions.Single();
            var tempBody = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.EnsureComponent<MindContainerComponent>(tempBody);
            entMan.EnsureComponent<ThunderdomePlayerComponent>(tempBody);
            Assert.That(tempMindSystem.TrySwapTempMind(session, tempBody), Is.True);
        });
        await pair.RunTicksSync(10);

        await Ghost(pair);

        await pair.Server.WaitAssertion(() =>
        {
            var state = respawnSystem.GetDebugState(userId);
            Assert.That(state.RespawnAvailableAt, Is.EqualTo(originalAvailableAt));
        });

        await pair.CleanReturnAsync();
    }

    private static async Task<TestPair> SetupRoundPair(TimeSpan delay, bool ghostKillCrit = false)
    {
        var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
            Dirty = true,
            InLobby = true
        });

        var ticker = pair.Server.System<GameTicker>();
        await pair.Server.WaitPost(() =>
        {
            pair.Server.CfgMan.SetCVar(CCVars.GhostRespawnDelay, Math.Max(0, (int) Math.Ceiling(delay.TotalSeconds)));
            pair.Server.CfgMan.SetCVar(CCVars.GhostKillCrit, ghostKillCrit);
            ticker.ToggleReadyAll(true);
            ticker.StartRound();
        });
        await pair.RunTicksSync(20);

        return pair;
    }

    private static async Task<TestPair> SetupObserverPair()
    {
        var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
            Dirty = true,
            InLobby = true
        });

        var ticker = pair.Server.System<GameTicker>();
        var mapSystem = pair.Server.System<SharedMapSystem>();
        var entMan = pair.Server.EntMan;
        await pair.Server.WaitPost(() =>
        {
            pair.Server.CfgMan.SetCVar(CCVars.GhostRespawnDelay, 5);
            mapSystem.CreateMap(out var mapId);
            entMan.SpawnEntity("SpawnPointObserver", new MapCoordinates(0, 0, mapId));
            ticker.JoinAsObserver(pair.Server.PlayerMan.Sessions.Single());
        });
        await pair.RunTicksSync(20);
        await pair.Server.WaitAssertion(() =>
        {
            var attached = pair.Server.PlayerMan.Sessions.Single().AttachedEntity;
            Assert.That(attached, Is.Not.Null);
            Assert.That(pair.Server.EntMan.HasComponent<GhostComponent>(attached!.Value), Is.True);
        });

        return pair;
    }

    private static async Task Ghost(TestPair pair)
    {
        var console = pair.Client.ResolveDependency<IConsoleHost>();
        await pair.Client.WaitPost(() => console.ExecuteCommand("ghost"));
        await pair.RunTicksSync(10);
    }

    private static int GetTicksForDelay(TestPair pair, TimeSpan delay)
    {
        var timing = pair.Server.ResolveDependency<IGameTiming>();
        return (int) Math.Ceiling(delay.TotalSeconds / timing.TickPeriod.TotalSeconds);
    }

    private static async Task DisconnectReconnect(TestPair pair)
    {
        var netManager = pair.Client.ResolveDependency<IClientNetManager>();
        var player = pair.Server.PlayerMan.Sessions.Single();
        var name = player.Name;
        var userId = player.UserId;

        await pair.Client.WaitAssertion(() => netManager.ClientDisconnect("Ghost respawn reconnect test"));
        await pair.RunTicksSync(10);

        await Task.WhenAll(pair.Client.WaitIdleAsync(), pair.Server.WaitIdleAsync());
        pair.Client.SetConnectTarget(pair.Server);
        await pair.Client.WaitPost(() => netManager.ClientConnect(null!, 0, name));
        await pair.RunTicksSync(10);

        await pair.Server.WaitAssertion(() =>
        {
            var session = pair.Server.PlayerMan.Sessions.Single();
            Assert.That(session.UserId, Is.EqualTo(userId));
            Assert.That(session.AttachedEntity, Is.Not.Null);
        });
    }

    private static GhostGui GetGhostGui(IUserInterfaceManager ui)
    {
        GhostGui? gui = null;
        if (ui.ActiveScreen == null || !TryFindControl(ui.ActiveScreen, out gui))
            Assert.Fail("Could not find an active GhostGui.");

        return gui!;
    }

    private static Button GetReturnToRoundButton(IUserInterfaceManager ui)
    {
        var gui = GetGhostGui(ui);
        Button? button = null;
        if (!TryFindControl(gui, out button, control => control.Name == "ReturnToRoundButton"))
            Assert.Fail("Could not find the ReturnToRoundButton.");

        return button!;
    }

    private static bool TryFindControl<TControl>(Control root, out TControl? found, Predicate<TControl>? predicate = null)
        where TControl : Control
    {
        if (root is TControl control && (predicate == null || predicate(control)))
        {
            found = control;
            return true;
        }

        foreach (var child in root.Children)
        {
            if (TryFindControl(child, out found, predicate))
                return true;
        }

        found = null;
        return false;
    }

    private static async Task ClickControl(TestPair pair, Control control)
    {
        var screenCoords = new ScreenCoordinates(
            control.GlobalPixelPosition + control.PixelSize / 2,
            control.Window?.Id ?? default);

        var relativePos = screenCoords.Position / control.UIScale - control.GlobalPosition;
        var relativePixelPos = screenCoords.Position - control.GlobalPixelPosition;

        var down = new GUIBoundKeyEventArgs(
            EngineKeyFunctions.UIClick,
            BoundKeyState.Down,
            screenCoords,
            default,
            relativePos,
            relativePixelPos);

        await pair.Client.DoGuiEvent(control, down);
        await pair.RunTicksSync(1);

        var up = new GUIBoundKeyEventArgs(
            EngineKeyFunctions.UIClick,
            BoundKeyState.Up,
            screenCoords,
            default,
            relativePos,
            relativePixelPos);

        await pair.Client.DoGuiEvent(control, up);
        await pair.RunTicksSync(1);
    }
}
