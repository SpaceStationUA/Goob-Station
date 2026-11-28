// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Goobstation.Server.StationEvents.Components;
using Content.Goobstation.Server.Supermatter.Systems;
using Content.Goobstation.Shared.Supermatter.Components;
using Content.Goobstation.Shared.Supermatter.Systems;
using Content.Pirate.Server.Supermatter;
using Content.Pirate.Server.CrystalMass;
using Content.Server.Spreader;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Radio.EntitySystems;
using Content.Server.Power.EntitySystems;
using Content.Server.Telephone;
using Content.Shared.Holopad;
using Content.Shared.Telephone;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared._Pirate.CCVars;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Medical.SuitSensors;
using Content.Server.RoundEnd;
using Robust.Shared.Containers;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Interaction.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared._Pirate.StatusEffectNew;
using Content.Server.GameTicking.Presets;
using Content.Server.StationEvents;
using Content.Server.StationEvents.Components;
using Content.Shared._Pirate.Breadcrumb.Components;
using Content.Shared._Pirate.Lavaland.Puzzle;
using Content.Shared._Pirate.StationAi;
using Content.Shared._Pirate.Supermatter;
using Content.Shared.Charges.Systems;
using Content.Shared.EntityTable;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Radio.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Crew;

[TestFixture]
public sealed partial class CrewFeatureTest
{
    [TestPrototypes]
    private const string FastCascade = """
        - type: entity
          id: PirateTestShortResonanceCascade
          parent: SupermatterCascadeDelamRule
          components:
          - type: CascadeRule
            durationToRoundEnd: 0.25
        """;

    [Test]
    public async Task BiomagnetismCanBeDrawnByShiftSchedulers()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            const string eventId = "BiomagneticPolarization";
            var protos = server.ProtoMan;
            var em = server.EntMan;
            var events = server.System<EventManagerSystem>();
            var tables = server.System<EntityTableSystem>();
            foreach (var (preset, scheduler) in new[] { ("Blueshift", "BlueshiftDirector"), ("SecretSalt", "SecretSalt") })
            {
                Assert.That(protos.Index<GamePresetPrototype>(preset).Rules.Select(id => id.Id),
                    Does.Contain(scheduler), preset);
                Assert.That(protos.Index<EntityPrototype>(scheduler).TryGetComponent<SelectedGameRulesComponent>(out var selected, em.ComponentFactory), Is.True);
                Assert.That(tables.GetSpawns(selected!.ScheduledGameRules).Select(id => id.Id),
                    Does.Contain(eventId), preset);
            }

            var basic = protos.Index<EntityTablePrototype>("BasicGameRulesTableNoAntag");
            Assert.That(tables.GetSpawns(basic).Select(id => id.Id), Does.Contain(eventId));
            var rule = protos.Index<EntityPrototype>(eventId);
            Assert.That(rule.TryGetComponent<StationEventComponent>(out var settings, em.ComponentFactory), Is.True);
            Assert.That(settings!.Weight, Is.Positive);
            Assert.That(settings.Chaos.Empty, Is.False);
            Assert.That(events.CanRun(rule, settings, 30, TimeSpan.FromMinutes(1)), Is.False);
            Assert.That(events.CanRun(rule, settings, 30, TimeSpan.FromHours(1)), Is.True);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MinersLeaveSeparateColoredStonesAndRefillFromShards()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var em = server.EntMan;
        EntityUid user = default, bag = default;
        await server.WaitAssertion(() =>
        {
            user = em.SpawnEntity("MobHuman", map.GridCoords);
            bag = em.SpawnEntity("GlowstoneDropperEmpty", map.GridCoords);
            var hands = server.System<SharedHandsSystem>();
            Assert.That(hands.TryPickupAnyHand(user, bag), Is.True);
            var charges = server.System<SharedChargesSystem>();
            Assert.That(charges.GetCurrentCharges(bag), Is.Zero);
            var shard = em.SpawnEntity("ShardCrystalBlue", map.GridCoords);
            em.EventBus.RaiseLocalEvent(bag, new InteractUsingEvent(user, shard, bag, map.GridCoords));
            Assert.That(charges.GetCurrentCharges(bag), Is.EqualTo(15));
            em.EventBus.RaiseLocalEvent(bag, new UseInHandEvent(user));
            Assert.That(em.GetComponent<BreadcrumbDropperComponent>(bag).Enabled, Is.True);
        });
        await pair.RunTicksSync(30);
        await server.WaitAssertion(() =>
        {
            var stones = em.AllComponentsList<BreadcrumbComponent>().Where(e => e.Component.Dropper == bag).ToArray();
            Assert.That(stones, Has.Length.EqualTo(1));
            Assert.That(stones[0].Component.Color, Is.EqualTo(em.GetComponent<BreadcrumbDropperComponent>(bag).Color));
            var light = em.GetComponent<Robust.Server.GameObjects.PointLightComponent>(stones[0].Uid);
            Assert.That(light.Enabled, Is.True);
            Assert.That(light.Color, Is.EqualTo(stones[0].Component.Color));
            Assert.That(em.GetComponent<TransformComponent>(stones[0].Uid).ParentUid, Is.EqualTo(map.Grid.Owner));
            var xform = server.System<SharedTransformSystem>();
            var maps = server.System<SharedMapSystem>();
            maps.SetTile(map.Grid, new Vector2i(6, 0), map.Tile.Tile);
            xform.SetCoordinates(user, maps.GridTileToLocal(map.Grid, map.Grid.Comp, new Vector2i(6, 0)));
        });
        await pair.RunTicksSync(30);
        await server.WaitAssertion(() =>
        {
            Assert.That(em.AllComponentsList<BreadcrumbComponent>().Count(e => e.Component.Dropper == bag), Is.EqualTo(2));
            Assert.That(server.System<SharedChargesSystem>().GetCurrentCharges(bag), Is.EqualTo(13));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SolveSlidingPuzzleAndOpenItsReward()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var em = server.EntMan;
        await server.WaitAssertion(() =>
        {
            var actor = em.SpawnEntity("MobHuman", map.GridCoords);
            var board = em.SpawnEntity("LavalandSlidingPuzzle", map.GridCoords);
            var puzzle = em.GetComponent<LavalandSlidingPuzzleComponent>(board);
            var xforms = server.System<SharedTransformSystem>();
            var maps = server.System<SharedMapSystem>();
            var center = maps.CoordinatesToTile(map.Grid, map.Grid.Comp, map.GridCoords);
            Vector2i Offset(int id) => new((id - 1) % 3 - 1, 1 - (id - 1) / 3);

            Assert.That(puzzle.Elements, Has.Count.EqualTo(8));
            var ordering = puzzle.Elements.OrderBy(uid =>
            {
                var pos = maps.CoordinatesToTile(map.Grid, map.Grid.Comp, em.GetComponent<TransformComponent>(uid).Coordinates) - center;
                return (1 - pos.Y) * 3 + pos.X + 2;
            }).Select(uid => em.GetComponent<LavalandSlidingPuzzleElementComponent>(uid).Id).ToArray();
            var inversions = ordering.SelectMany((a, i) => ordering.Skip(i + 1).Where(b => a > b)).Count();
            Assert.That(inversions % 2, Is.Zero, "The generated board can be solved by legal slides.");

            // Put the randomized board one legal slide away from its solution.
            var missing = puzzle.EmptyTileId;
            var last = missing % 3 == 1 ? missing + 1 : missing - 1;
            EntityUid lastPillar = default;
            foreach (var pillar in puzzle.Elements)
            {
                var id = em.GetComponent<LavalandSlidingPuzzleElementComponent>(pillar).Id;
                var dest = id == last ? missing : id;
                if (id == last)
                    lastPillar = pillar;
                xforms.Unanchor(pillar);
                xforms.SetCoordinates(pillar, maps.GridTileToLocal(map.Grid, map.Grid.Comp, center + Offset(dest)));
                xforms.AnchorEntity(pillar, em.GetComponent<TransformComponent>(pillar));
            }
            var click = new InteractHandEvent(actor, lastPillar);
            em.EventBus.RaiseLocalEvent(lastPillar, click);
            Assert.That(click.Handled && puzzle.Finished, Is.True);
            var cube = em.AllComponentsList<LavalandPrisonCubeComponent>().Single().Uid;
            var ct = server.System<Robust.Shared.Containers.SharedContainerSystem>();
            Assert.That(ct.TryGetContainingContainer(cube, out var chest), Is.True);
            Assert.That(em.GetComponent<MetaDataComponent>(chest!.Owner).EntityPrototype!.ID, Is.EqualTo("LavalandPuzzleChest"));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PrisonCubeRequiresReachAndReleasesItsPrisonerOnRemoval()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var em = server.EntMan;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var user = em.SpawnEntity("MobHuman", map.GridCoords);
            var target = em.SpawnEntity("MobHuman", map.GridCoords);
            var cube = em.SpawnEntity("LavalandPrisonCube", map.GridCoords);
            var invalid = new AfterInteractEvent(user, cube, target, map.GridCoords, true);
            em.EventBus.RaiseLocalEvent(cube, invalid);
            Assert.That(invalid.Handled, Is.False);
            server.System<MobStateSystem>().ChangeMobState(target, MobState.Critical);
            var distant = new AfterInteractEvent(user, cube, target, map.GridCoords, false);
            em.EventBus.RaiseLocalEvent(cube, distant);
            Assert.That(distant.Handled, Is.False);
            var use = new AfterInteractEvent(user, cube, target, map.GridCoords, true);
            em.EventBus.RaiseLocalEvent(cube, use);
            Assert.That(use.Handled, Is.True);
            var board = em.AllComponentsList<LavalandSlidingPuzzleComponent>().Single(x => x.Component.Prisoner == target);
            Assert.That(em.HasComponent<GodmodeComponent>(target), Is.True);
            em.DeleteEntity(board.Uid);
            Assert.That(em.EntityExists(target), Is.True);
            Assert.That(em.HasComponent<BlockMovementComponent>(target), Is.False);
            Assert.That(em.HasComponent<GodmodeComponent>(target), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MagneticEventPolarizesCrewAndOppositeChargesDischargeOnContact()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true, DummyTicker = false });
        var server = pair.Server;
        var em = server.EntMan;
        var map = await pair.CreateTestMap();
        var effects = server.System<StatusEffectsSystem>();
        EntityUid a = default, b = default, unmanned = default;
        await server.WaitAssertion(() =>
        {
            a = em.SpawnEntity("MobHuman", map.GridCoords);
            var maps = server.System<SharedMapSystem>();
            var otherTile = new Vector2i(4, 0);
            maps.SetTile(map.Grid, otherTile, map.Tile.Tile);
            b = em.SpawnEntity("MobHuman", maps.GridTileToLocal(map.Grid, map.Grid.Comp, otherTile));
            unmanned = em.SpawnEntity("MobHuman", map.GridCoords);
            var mind = server.System<MindSystem>();
            mind.TransferTo(mind.CreateMind(null, "A"), a);
            mind.TransferTo(mind.CreateMind(null, "B"), b);
            var ticker = server.System<GameTicker>();
            Assert.That(ticker.StartGameRule("BiomagneticPolarization"), Is.True);
            Assert.That(effects.TryGetStatusEffect(a, SharedBiomagneticPolarizationSystem.BiomagEffectID, out _), Is.False);
        });
        await pair.RunSeconds(21);
        await server.WaitAssertion(() =>
        {
            var id = SharedBiomagneticPolarizationSystem.BiomagEffectID;
            Assert.That(effects.TryGetStatusEffect(a, id, out var first), Is.True);
            Assert.That(effects.TryGetStatusEffect(b, id, out var second), Is.True);
            Assert.That(effects.TryGetStatusEffect(unmanned, id, out _), Is.False);
            em.GetComponent<BiomagneticPolarizationStatusEffectComponent>(first!.Value).Polarization = true;
            em.GetComponent<BiomagneticPolarizationStatusEffectComponent>(second!.Value).Polarization = false;
            var hug = new InteractionSuccessEvent(b);
            em.EventBus.RaiseLocalEvent(a, ref hug);
            Assert.That(em.GetComponent<BiomagneticPolarizationStatusEffectComponent>(first.Value).Expired, Is.True);
            Assert.That(em.GetComponent<BiomagneticPolarizationStatusEffectComponent>(second.Value).Expired, Is.True);
        });
        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(effects.TryGetStatusEffect(a, SharedBiomagneticPolarizationSystem.BiomagEffectID, out _), Is.False);
            Assert.That(effects.TryGetStatusEffect(b, SharedBiomagneticPolarizationSystem.BiomagEffectID, out _), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CascadeGasRecipeRequiresBothFractionsAndEnoughGas()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var em = server.EntMan;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var core = em.SpawnEntity("Supermatter", new EntityCoordinates(map.MapUid, 25, 25));
            var sm = em.GetComponent<SupermatterComponent>(core);
            var atmos = server.System<AtmosphereSystem>();
            var system = server.System<SupermatterSystem>();
            SharedSupermatterSystem.DelamType Choose(float frezon, float tritium, float nitrogen)
            {
                var gas = new GasMixture();
                gas.SetMoles(Gas.Frezon, frezon);
                gas.SetMoles(Gas.Tritium, tritium);
                gas.SetMoles(Gas.Nitrogen, nitrogen);
                atmos.SetMapAtmosphere(map.MapUid, false, gas);
                return system.ChooseDelamType(core, sm);
            }
            Assert.That(Choose(96, 96, 48), Is.EqualTo(SharedSupermatterSystem.DelamType.Cascade));
            Assert.That(Choose(95, 97, 48), Is.Not.EqualTo(SharedSupermatterSystem.DelamType.Cascade));
            Assert.That(Choose(97, 95, 48), Is.Not.EqualTo(SharedSupermatterSystem.DelamType.Cascade));
            Assert.That(Choose(95, 95, 46), Is.Not.EqualTo(SharedSupermatterSystem.DelamType.Cascade));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InstallingTheCrystalUsesTheHeldItemAndRejectsDamagedCores()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var em = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid core = default, crystal = default;
        await server.WaitAssertion(() =>
        {
            core = em.SpawnEntity("Supermatter", map.GridCoords);
            var actor = em.SpawnEntity("MobHuman", map.GridCoords.Offset(new Vector2(1.2f, 0)));
            crystal = em.SpawnEntity("DestabilizingCrystal", map.GridCoords);
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(actor, crystal), Is.True);
            var sm = em.GetComponent<SupermatterComponent>(core);
            sm.Damage = sm.DelaminationPoint * 0.25f;
            em.EventBus.RaiseLocalEvent(core, new InteractUsingEvent(actor, crystal, core, map.GridCoords));
            Assert.That(em.GetComponent<CascadeCoreComponent>(core).Destabilized, Is.False);
            sm.Damage = 0f;
            em.EventBus.RaiseLocalEvent(core, new InteractUsingEvent(actor, crystal, core, map.GridCoords));
        });
        await pair.RunTicksSync(pair.SecondsToTicks(5.5f));
        await server.WaitAssertion(() =>
        {
            Assert.That(em.GetComponent<CascadeCoreComponent>(core).Destabilized, Is.True);
            Assert.That(em.EntityExists(crystal), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AiRadioClickRequiresAHeardSpeakerAndPublicCoordinates()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var em = server.EntMan;
        var map = await pair.CreateTestMap();
        var maps = server.System<SharedMapSystem>();
        var transforms = server.System<SharedTransformSystem>();
        var sensors = server.System<SharedSuitSensorSystem>();
        EntityUid core = default, eye = default, speaker = default, uniform = default, holopad = default, brain = default;
        NetEntity radioSpeaker = default;
        MapCoordinates corePosition = default;

        await server.WaitAssertion(() =>
        {
            var tile = new Tile(server.ResolveDependency<ITileDefinitionManager>()["FloorSteel"].TileId);
            for (var x = 0; x <= 4; x++)
                maps.SetTile(map.Grid, new Vector2i(x, 0), tile);

            core = em.SpawnEntity("PlayerStationAiEmpty", map.GridCoords);
            brain = em.SpawnAttachedTo("StationAiBrain", map.GridCoords);
            var containers = server.System<SharedContainerSystem>();
            Assert.That(containers.Insert(brain, containers.GetContainer(core, StationAiCoreComponent.Container)), Is.True);
            server.PlayerMan.SetAttachedEntity(server.PlayerMan.Sessions.Single(), brain);
            eye = em.GetComponent<StationAiCoreComponent>(core).RemoteEntity!.Value;
            corePosition = transforms.GetMapCoordinates(core);
            holopad = em.SpawnAttachedTo("Holopad", maps.GridTileToLocal(map.Grid, map.Grid.Comp, new Vector2i(1, 0)));
            var power = server.System<PowerReceiverSystem>();
            power.SetNeedsPower(holopad, false);
            power.SetNeedsPower(core, false);
            var remoteTile = maps.GridTileToLocal(map.Grid, map.Grid.Comp, new Vector2i(4, 0));
            speaker = em.SpawnEntity("MobHuman", remoteTile);
            uniform = em.SpawnEntity("ClothingUniformJumpsuitCaptain", remoteTile);
            Assert.That(server.System<InventorySystem>().TryEquip(speaker, uniform, "jumpsuit", silent: true, force: true), Is.True);
            sensors.SetSensor(uniform, SuitSensorMode.SensorCords);
            em.EnsureComponent<TelecomExemptComponent>(speaker);
            radioSpeaker = em.GetNetEntity(speaker);
        });
        // Let the power network update the new receivers before establishing a real call.
        await pair.RunSeconds(1);
        await server.WaitAssertion(() =>
        {
            var telephone = server.System<TelephoneSystem>();
            var caller = (core, em.GetComponent<TelephoneComponent>(core));
            var receiver = (holopad, em.GetComponent<TelephoneComponent>(holopad));
            Assert.That(telephone.IsTelephonePowered(caller), Is.True, "The AI core needs power for the holocall.");
            Assert.That(telephone.IsTelephonePowered(receiver), Is.True, "The destination holopad needs power.");
            Assert.That(telephone.IsSourceInRangeOfReceiver(caller, receiver), Is.True);
            em.EventBus.RaiseLocalEvent(holopad, new HolopadActivateProjectorMessage { Actor = brain });
            Assert.That(telephone.IsSourceConnectedToReceiver(caller, receiver), Is.True);
            var coreState = em.GetComponent<StationAiCoreComponent>(core);
            Assert.That(coreState.Remote, Is.False);
            eye = coreState.RemoteEntity!.Value;
        });
        await pair.RunUntilSynced();

        async Task ClickSpeaker()
        {
            await client.WaitPost(() => client.EntMan.RaisePredictiveEvent(new AiRadioJumpEvent(radioSpeaker)));
            await pair.RunSeconds(1);
        }

        await ClickSpeaker();
        await server.WaitAssertion(() =>
        {
            Assert.That(transforms.GetMapCoordinates(eye).InRange(transforms.GetMapCoordinates(speaker), 0.2f), Is.False);
            sensors.SetSensor(uniform, SuitSensorMode.SensorVitals);
            server.System<RadioSystem>().SendRadioMessage(speaker, "Перевірка зв’язку.", "Common", speaker);
        });
        await ClickSpeaker();
        await server.WaitAssertion(() =>
        {
            Assert.That(transforms.GetMapCoordinates(eye).InRange(transforms.GetMapCoordinates(speaker), 0.2f), Is.False);
            Assert.That(em.GetComponent<StationAiCoreComponent>(core).Remote, Is.False);
            sensors.SetSensor(uniform, SuitSensorMode.SensorCords);
        });
        await ClickSpeaker();
        await server.WaitAssertion(() =>
        {
            var coreState = em.GetComponent<StationAiCoreComponent>(core);
            Assert.That(coreState.Remote, Is.True);
            Assert.That(em.GetComponent<HolopadComponent>(holopad).Hologram, Is.Null);
            Assert.That(server.System<TelephoneSystem>().IsTelephoneEngaged((core, em.GetComponent<TelephoneComponent>(core))), Is.False);
            Assert.That(transforms.GetMapCoordinates(coreState.RemoteEntity!.Value).InRange(transforms.GetMapCoordinates(speaker), 0.2f), Is.True);
            Assert.That(transforms.GetMapCoordinates(core), Is.EqualTo(corePosition));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AiCanFollowPublicImplantCoordinatesOnly()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var em = server.EntMan;
        await server.WaitAssertion(() =>
        {
            var wearer = em.SpawnEntity("MobHuman", map.GridCoords);
            var containers = server.System<SharedContainerSystem>();
            var implantSlot = containers.EnsureContainer<Container>(wearer, "implant");
            var sensors = server.System<SharedSuitSensorSystem>();
            var command = em.SpawnEntity("CommandTrackingImplant", map.GridCoords);
            Assert.That(containers.Insert(command, implantSlot), Is.True);
            sensors.SetSensor(command, SuitSensorMode.SensorCords);
            Assert.That(sensors.HasPublicCoordinates(wearer), Is.False);
            var tracking = em.SpawnEntity("TrackingImplant", map.GridCoords);
            Assert.That(containers.Insert(tracking, implantSlot), Is.True);
            sensors.SetSensor(tracking, SuitSensorMode.SensorCords);
            Assert.That(sensors.HasPublicCoordinates(wearer), Is.True);
            sensors.SetSensor(tracking, SuitSensorMode.SensorVitals);
            Assert.That(sensors.HasPublicCoordinates(wearer), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PrisonCubePreservesAnOverlappingBoardAndItsTarget()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var em = server.EntMan;
        await server.WaitAssertion(() =>
        {
            var board = em.SpawnEntity("LavalandSlidingPuzzle", map.GridCoords);
            var pieces = em.GetComponent<LavalandSlidingPuzzleComponent>(board).Elements.ToArray();
            var user = em.SpawnEntity("MobHuman", map.GridCoords);
            var prisoner = em.SpawnEntity("MobHuman", map.GridCoords);
            server.System<MobStateSystem>().ChangeMobState(prisoner, MobState.Critical);
            var cube = em.SpawnEntity("LavalandPrisonCube", map.GridCoords);
            var interaction = new AfterInteractEvent(user, cube, prisoner, map.GridCoords, true);
            em.EventBus.RaiseLocalEvent(cube, interaction);
            Assert.That(interaction.Handled, Is.False);
            Assert.That(em.HasComponent<BlockMovementComponent>(prisoner), Is.False);
            Assert.That(em.EntityExists(cube), Is.True);
            Assert.That(pieces, Has.Length.EqualTo(8));
            Assert.That(pieces.All(piece => em.EntityExists(piece)), Is.True);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DelayedCascadesKeepBothOriginsAndBlockAutomaticEvacuation()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true, DummyTicker = false });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var em = server.EntMan;
        var maps = server.System<SharedMapSystem>();
        var positions = new[] { new Vector2i(-3, 0), new Vector2i(3, 0) };
        await server.WaitAssertion(() =>
        {
            server.CfgMan.SetCVar(PirateVars.EventsDelayOverride, 1f);
            foreach (var tile in positions)
            {
                maps.SetTile(map.Grid, tile, map.Tile.Tile);
                var core = em.SpawnEntity("Supermatter", maps.GridTileToLocal(map.Grid, map.Grid.Comp, tile));
                em.EventBus.RaiseEvent(Robust.Shared.GameObjects.EventSource.Local, new SupermatterCascadeEvent(core));
            }
            Assert.That(em.AllComponentsList<CascadeRuleComponent>().Single().Component.PendingOrigins, Has.Count.EqualTo(2));
        });
        await pair.RunSeconds(2);
        await server.WaitAssertion(() =>
        {
            var rule = em.AllComponentsList<CascadeRuleComponent>().Single();
            Assert.That(rule.Component.PendingOrigins, Is.Empty);
            foreach (var tile in positions)
            {
                var anchored = maps.GetAnchoredEntities(map.Grid.Owner, map.Grid.Comp, tile);
                var found = false;
                while (anchored.MoveNext(out var entity))
                    found |= em.HasComponent<CrystalMassComponent>(entity);
                Assert.That(found, Is.True);
            }
            var request = new RoundEndRequestAttemptEvent(null);
            em.EventBus.RaiseEvent(Robust.Shared.GameObjects.EventSource.Local, ref request);
            Assert.That(request.Cancelled, Is.True);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RecoveredCrystalSurvivesTheLastCountdownTick()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var em = server.EntMan;
        EntityUid core = default;
        await server.WaitPost(() =>
        {
            core = em.SpawnEntity("Supermatter", map.GridCoords);
            var sm = em.GetComponent<SupermatterComponent>(core);
            sm.Activated = true;
            sm.Delamming = true;
            sm.Damage = 0f;
            sm.DelamTimerAccumulator = sm.DelamTimer - 1f;
            em.GetComponent<CascadeCoreComponent>(core).Destabilized = true;
        });
        await pair.RunSeconds(3);
        await server.WaitAssertion(() =>
        {
            var sm = em.GetComponent<SupermatterComponent>(core);
            Assert.That(sm.Delamming, Is.False);
            Assert.That(sm.DelamTimerAccumulator, Is.Zero);
            Assert.That(em.AllComponentsList<CascadeRuleComponent>(), Is.Empty);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CascadeKeepsAnExistingCrystalWhenSpreadSelectsItsTileAgain()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var em = server.EntMan;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var maps = server.System<SharedMapSystem>();
            var tile = new Vector2i(1, 0);
            maps.SetTile(map.Grid, tile, map.Tile.Tile);
            var source = em.SpawnEntity("CrystalMassSpreader", map.GridCoords);
            var neighbor = em.SpawnEntity("CrystalMassSpreader", maps.GridTileToLocal(map.Grid, map.Grid.Comp, tile));
            var crystal = em.GetComponent<CrystalMassComponent>(source);
            crystal.SpreadChance = 0f;
            crystal.SecondaryChance = 0f;
            var spread = new SpreadNeighborsEvent { Updates = 1 };
            spread.AllNeighbors.Add((map.Grid.Owner, map.Grid.Comp, tile));
            em.EventBus.RaiseLocalEvent(source, ref spread);
            Assert.That(spread.Updates, Is.Zero);
            Assert.That(em.AllComponentsList<CrystalMassComponent>().Select(x => x.Uid), Is.EquivalentTo(new[] { source, neighbor }));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FullSizeCrystalStartsOneCascadeAtItsOwnPosition()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var em = server.EntMan;
        EntityUid activeCascade = default;
        await server.WaitAssertion(() =>
        {
            var uid = em.SpawnEntity("Supermatter", map.GridCoords);
            var sm = em.GetComponent<SupermatterComponent>(uid);
            var processor = server.System<SupermatterSystem>();
            Assert.That(processor.ChooseDelamType(uid, sm), Is.EqualTo(SharedSupermatterSystem.DelamType.Explosion));
            em.GetComponent<CascadeCoreComponent>(uid).Destabilized = true;
            Assert.That(processor.ChooseDelamType(uid, sm), Is.EqualTo(SharedSupermatterSystem.DelamType.Cascade));
            em.EventBus.RaiseEvent(Robust.Shared.GameObjects.EventSource.Local, new SupermatterCascadeEvent(uid));
            var rule = em.AllComponentsList<CascadeRuleComponent>().Single();
            activeCascade = rule.Uid;
            Assert.That(rule.Component.PendingOrigins, Is.Empty);
            Assert.That(rule.Component.DurationToRoundEnd, Is.EqualTo(TimeSpan.FromMinutes(5)));
            Assert.That(server.System<GameTicker>().IsGameRuleActive(rule.Uid), Is.True);
            var tile = server.System<SharedMapSystem>().GetTileRef(map.Grid, map.GridCoords);
            Assert.That(server.ResolveDependency<ITileDefinitionManager>()[tile.Tile.TypeId].ID,
                Is.EqualTo("PlatingCrystalMass"));
        });
        await server.WaitAssertion(() =>
        {
            var ticker = server.System<GameTicker>();
            ticker.EndGameRule(activeCascade);
            Assert.That(ticker.StartGameRule("PirateTestShortResonanceCascade", out activeCascade), Is.True);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(0.5f));
        await server.WaitAssertion(() => Assert.That(em.GetComponent<CascadeRuleComponent>(activeCascade).Stage,
            Is.EqualTo(ResonanceCascadeStage.End)));
        await pair.CleanReturnAsync();
    }
}
