// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._Pirate.CaptureTheFlag;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Systems.Ghost.Widgets;
using Content.Goobstation.Common.CCVar;
using Content.IntegrationTests.Pair;
using Content.Server.Atmos.EntitySystems;
using Content.Server._EinsteinEngines.Atmos.Components;
using Content.Server.Body.Systems;
using Content.Server.Destructible;
using Content.Server.GameTicking;
using Content.Server.Hands.Systems;
using Content.Server.Preferences.Managers;
using Content.Server._Pirate.CaptureTheFlag;
using Content.Shared._Shitmed.Restrict;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.CCVar;
using Content.Shared.Damage.Components;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Preferences;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.CaptureTheFlag;

[TestFixture]
public sealed class CaptureTheFlagTest
{
    private static readonly ProtoId<TagPrototype> WallTag = "Wall";

    [Test]
    public async Task HullAndCoverSurviveGunfireAndKeepTheCornersPressurized()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        await StartMatch(pair, 1);
        var server = pair.Server;
        var entities = server.EntMan;
        var grid = GetRule(pair).Grid;
        var mapGrid = entities.GetComponent<MapGridComponent>(grid);
        var maps = server.System<SharedMapSystem>();
        var atmos = server.System<AtmosphereSystem>();
        var guns = server.System<SharedGunSystem>();
        var tags = server.System<TagSystem>();
        Vector2i[] directions = [new(1, 0), new(-1, 0), new(0, 1), new(0, -1)];
        Vector2i[] corners = [new(-18, -7), new(-18, 7), new(18, -7), new(18, 7)];
        var pressures = new Dictionary<Vector2i, float>();
        var targets = new List<(EntityUid Wall, EntityCoordinates Origin, Vector2 Direction)>();

        float Pressure(Vector2i tile) => atmos.GetTileMixture(grid, null, tile, excite: true)!.Pressure;

        await server.WaitAssertion(() =>
        {
            var floor = maps.GetAllTiles(grid, mapGrid).Select(tile => tile.GridIndices).ToHashSet();
            var rim = floor.Where(tile => directions.Any(dir => !floor.Contains(tile + dir))).ToHashSet();
            Assert.That(rim, Is.Not.Empty);
            foreach (var tile in rim)
                Assert.That(atmos.IsTileAirBlocked(grid, tile), Is.True, $"Open hull at {tile}");

            var coverTypes = new HashSet<string>();
            foreach (var tile in floor)
            {
                foreach (var uid in maps.GetAnchoredEntities(grid, mapGrid, tile))
                {
                    if (!tags.HasTag(uid, WallTag))
                        continue;

                    var id = entities.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID;
                    Assert.That(entities.HasComponent<DestructibleComponent>(uid), Is.False, $"{id} at {tile}");
                    var corner = corners.Any(pos => tile == pos + new Vector2i(0, Math.Sign(pos.Y)));
                    if (!coverTypes.Add(id) && !corner)
                        continue;

                    var approach = directions.First(dir => floor.Contains(tile - dir) && !atmos.IsTileAirBlocked(grid, tile - dir));
                    var center = (Vector2) tile + new Vector2(0.5f);
                    targets.Add((uid, new EntityCoordinates(grid, center - (Vector2) approach), (Vector2) approach));
                }
            }
            Assert.That(coverTypes, Does.Contain("CaptureTheFlagHull"));
            Assert.That(coverTypes, Does.Contain("CaptureTheFlagCover"));
            foreach (var tile in corners)
            {
                pressures[tile] = Pressure(tile);
                Assert.That(pressures[tile], Is.InRange(90f, 110f), $"Corner {tile}");
            }
        });

        for (var volley = 0; volley < 12; volley++)
        {
            await server.WaitPost(() =>
            {
                foreach (var (_, origin, direction) in targets)
                {
                    var bullet = entities.SpawnEntity(volley % 2 == 0 ? "BulletPistol" : "BulletAntiMateriel", origin);
                    guns.ShootProjectile(bullet, direction, Vector2.Zero, null);
                }
            });
            await pair.RunTicksSync(4);
        }

        await pair.RunTicksSync(server.Timing.TickRate * 90);
        await server.WaitAssertion(() =>
        {
            foreach (var (wall, _, _) in targets)
            {
                Assert.That(entities.EntityExists(wall), Is.True);
                Assert.That(entities.GetComponent<DamageableComponent>(wall).TotalDamage.Float(), Is.Positive, $"Shots missed {wall}");
            }
            foreach (var tile in corners)
            {
                var after = Pressure(tile);
                TestContext.Out.WriteLine($"Corner {tile}: {pressures[tile]:F2} -> {after:F2} kPa after gunfire.");
                Assert.That(after, Is.EqualTo(pressures[tile]).Within(1f), $"Pressure loss at {tile}");
            }

            // Open a control breach to verify that the running atmosphere actually vents to space.
            var breach = corners[0] + new Vector2i(0, -1);
            var hullSegment = maps.GetAnchoredEntities(grid, mapGrid, breach).Single(uid => tags.HasTag(uid, WallTag));
            entities.DeleteEntity(hullSegment);
        });
        await pair.RunTicksSync(server.Timing.TickRate * 30);
        await server.WaitAssertion(() =>
        {
            var vented = Pressure(corners[0]);
            TestContext.Out.WriteLine($"Control breach at {corners[0]}: {vented:F2} kPa.");
            Assert.That(vented, Is.LessThan(pressures[corners[0]] - 5f));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CaptureRequiresHomeFlagAndUnlocksGearForBothTeams()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var hands = server.System<HandsSystem>();
        var interaction = server.System<SharedInteractionSystem>();
        await SetPlayerSpecies(pair, "Oni");
        await StartMatch(pair, 1);
        // Newly equipped weapons have the usual hand-selection attack cooldown.
        await pair.RunTicksSync(server.Timing.TickRate * 2);
        await pair.Client.WaitAssertion(() =>
        {
            Assert.That(pair.Client.ResolveDependency<IStateManager>().CurrentState, Is.TypeOf<GameplayState>());
            AssertScoreboard(pair, 0, 0, 1);
        });
        var rule = GetRule(pair);
        var redSession = server.PlayerMan.Sessions.Single(s => Team(pair, s) == CaptureTheFlagTeam.Red);
        var red = redSession.AttachedEntity!.Value;
        var blue = server.PlayerMan.Sessions.Single(s => Team(pair, s) == CaptureTheFlagTeam.Blue).AttachedEntity!.Value;
        var oni = pair.Player!.AttachedEntity!.Value;
        var redHome = GetHome(pair, CaptureTheFlagTeam.Red);
        var blueHome = GetHome(pair, CaptureTheFlagTeam.Blue);
        var redSupply = redHome.Comp.Supply;
        var blueSupply = blueHome.Comp.Supply;

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<MapGridComponent>(rule.Grid), Is.True);
            Assert.That(redHome.Comp.Spawns, Has.Count.EqualTo(5));
            Assert.That(blueHome.Comp.Spawns, Has.Count.EqualTo(5));
            Assert.That(server.System<AtmosphereSystem>().IsMixtureProbablySafe(server.System<AtmosphereSystem>().GetTileMixture(red)), Is.True);
            AssertUniform(pair, red, "ClothingUniformJumpsuitColorRed");
            AssertUniform(pair, blue, "ClothingUniformJumpsuitColorBlue");
            Assert.That(entMan.GetComponent<HumanoidAppearanceComponent>(oni).Species.Id, Is.EqualTo("Oni"));
            AssertArenaGunUse(pair, oni, "WeaponPistolMk58");

            MoveTo(pair, red, redSupply);
            Use(pair, red, redSupply);
            Assert.That(CountPrototype(pair, "WeaponLaserSvalinn"), Is.Zero);

            var redFlag = redHome.Comp.Flag;
            var blueFlag = blueHome.Comp.Flag;
            GrabFlag(pair, red, blueFlag);
            GrabFlag(pair, blue, redFlag);

            MoveTo(pair, red, redHome);
            Use(pair, red, redHome);
            Assert.That(redHome.Comp.Score, Is.Zero, "A capture requires the allied flag at its stand.");

            var containers = server.System<SharedContainerSystem>();
            var bag = entMan.SpawnAttachedTo("ClothingBackpackSatchel", Coordinates(pair, red));
            var bagStorage = containers.GetContainer(bag, "storagebase");
            Assert.That(containers.Insert(blueFlag, bagStorage), Is.False, "Stowing a carried flag would hide it.");

            Assert.That(hands.TryDrop(blue, redFlag), Is.True);
            MoveTo(pair, red, redFlag);
            foreach (var item in hands.EnumerateHeld(red).Where(item => item != blueFlag).ToArray())
                Assert.That(hands.TryDrop(red, item), Is.True);

            Assert.That(hands.TryGetEmptyHand(red, out var empty), Is.True);
            Assert.That(hands.TrySetActiveHand(red, empty), Is.True);
            Assert.That(interaction.InteractHand(red, redFlag), Is.True);
            Assert.That(redHome.Comp.Flag, Is.Not.EqualTo(redFlag));

            SetActiveItem(pair, red, blueFlag);
            MoveTo(pair, red, redHome);
            Use(pair, red, redHome);
            Use(pair, red, redHome);
            Assert.That(redHome.Comp.Score, Is.EqualTo(1), "The captured flag can only score once, even before its old entity is deleted.");
            Assert.That(rule.Tier, Is.EqualTo(2));
        });

        await pair.RunUntilSynced();
        await pair.RunTicksSync(1);
        await pair.Client.WaitAssertion(() => AssertScoreboard(pair, 1, 0, 2));
        await server.WaitAssertion(() =>
        {
            MoveTo(pair, blue, redSupply);
            Use(pair, blue, redSupply);
            Assert.That(CountPrototype(pair, "WeaponLaserSvalinn"), Is.Zero, "Only allied players can claim equipment.");

            foreach (var (player, supply) in new[] { (red, redSupply), (blue, blueSupply) })
            {
                MoveTo(pair, player, supply);
                Use(pair, player, supply);
                Use(pair, player, supply);
            }

            Assert.That(CountPrototype(pair, "WeaponLaserSvalinn"), Is.EqualTo(2), "Each team gets one claim per player life.");
            AssertArenaGunUse(pair, oni, "WeaponLaserSvalinn");

            server.PlayerMan.SetAttachedEntity(redSession, null);
            server.System<GameTicker>().MakeJoinGame(redSession, EntityUid.Invalid);
            Assert.That(redSession.AttachedEntity, Is.EqualTo(red));
            Use(pair, red, redSupply);
            Assert.That(CountPrototype(pair, "WeaponLaserSvalinn"), Is.EqualTo(2), "Re-entry preserves the active life and claim history.");

            Capture(pair, blue, redHome, blueHome);
            Assert.That(blueHome.Comp.Score, Is.EqualTo(1));
            Assert.That(rule.Tier, Is.EqualTo(2));

            Capture(pair, red, blueHome, redHome);
            Assert.That(rule.Tier, Is.EqualTo(3));
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            foreach (var (player, supply) in new[] { (red, redSupply), (blue, blueSupply) })
            {
                MoveTo(pair, player, supply);
                Use(pair, player, supply);
            }

            Assert.That(CountPrototype(pair, "WeaponLaserCarbine"), Is.EqualTo(2));
            AssertArenaGunUse(pair, oni, "WeaponLaserCarbine");

            Capture(pair, red, blueHome, redHome);
            Assert.That(redHome.Comp.Score, Is.EqualTo(3));
            Assert.That(rule.Finished, Is.True);
            Assert.That(server.System<GameTicker>().RunLevel, Is.EqualTo(GameRunLevel.PostRound));
        });

        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.EntityQuery<CaptureTheFlagFlagComponent>().Count(), Is.EqualTo(2));
            Assert.That(GetRule(pair).Players, Is.Empty);
            Assert.That(GetRule(pair).EndAt, Is.Null);

            var ticker = server.System<GameTicker>();
            ticker.SetGamePreset("sandbox");
            ticker.RestartRound();
            server.CfgMan.SetCVar(CCVars.GameMap, PoolManager.TestMap);
            ticker.ToggleReadyAll(true);
            ticker.StartRound(force: true);

            Assert.That(ticker.CurrentPreset?.ID, Is.EqualTo("Sandbox"));
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            Assert.That(entMan.EntityQuery<CaptureTheFlagRuleComponent>(), Is.Empty);
            Assert.That(entMan.EntityQuery<CaptureTheFlagFlagComponent>(), Is.Empty);
            Assert.That(entMan.EntityQuery<CaptureTheFlagBodyComponent>(), Is.Empty);
        });
        await pair.RunUntilSynced();
        await pair.RunTicksSync(1);
        await pair.Client.WaitAssertion(() => Assert.That(GetScoreboard(pair), Is.Null));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemovingRedStandEndsTheMatchAndClosesTheScoreboard()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        await StartMatch(pair, 1);
        await pair.RunUntilSynced();
        await pair.Client.WaitAssertion(() => AssertScoreboard(pair, 0, 0, 1));

        var server = pair.Server;
        var rule = GetRule(pair);
        await server.WaitPost(() => server.EntMan.DeleteEntity(GetHome(pair, CaptureTheFlagTeam.Red)));
        await AdvanceMatch(pair, 1);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<GameTicker>().RunLevel, Is.EqualTo(GameRunLevel.PostRound));
            Assert.That(rule.EndAt, Is.Null);
            Assert.That(rule.Players, Is.Empty);
        });
        await pair.RunUntilSynced();
        await pair.Client.WaitAssertion(() => Assert.That(GetScoreboard(pair), Is.Null));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayCaptureProducesAContestedSharedReserveWithoutScoring()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        var ticker = server.System<GameTicker>();
        await StartMatch(pair, 3, dummySpecies: "Plasmaman");

        var rule = GetRule(pair);
        var relayUid = rule.Relay;
        var relay = server.EntMan.GetComponent<CaptureTheFlagRelayComponent>(relayUid);
        var session = pair.Player!;
        var team = Team(pair, session);
        var player = session.AttachedEntity!.Value;
        var teammate = server.PlayerMan.Sessions.Single(other => other != session && Team(pair, other) == team).AttachedEntity!.Value;
        var opponents = server.PlayerMan.Sessions.Where(other => Team(pair, other) != team).ToArray();
        var opponent = opponents[0].AttachedEntity!.Value;
        var home = GetHome(pair, team);
        var enemyHome = GetHome(pair, OtherTeam(team));
        var verbs = server.System<SharedVerbSystem>();

        await server.WaitAssertion(() =>
        {
            Assert.That(relay.Team, Is.Null);
            MoveTo(pair, player, relayUid, -0.75f);
        });
        await AdvanceMatch(pair, 2);
        await server.WaitAssertion(() =>
        {
            Assert.That(relay.ClaimingTeam, Is.EqualTo(team));
            MoveTo(pair, opponent, relayUid, 0.75f);
        });
        await AdvanceMatch(pair, 2);
        await pair.RunUntilSynced();
        await pair.Client.WaitAssertion(() =>
        {
            var center = GetScoreboard(pair)!.GetChild(0).Children.OfType<Label>().Single();
            Assert.That(center.Text, Is.EqualTo(Loc.GetString("ctf-hud-center",
                ("state", Loc.GetString("ctf-center-contested")), ("red", 0), ("blue", 0))));
        });
        await server.WaitAssertion(() =>
        {
            Assert.That(relay.ClaimingTeam, Is.Null, "An opponent interrupts and clears capture progress.");
            MoveTo(pair, opponent, enemyHome);
        });
        await AdvanceMatch(pair, 3);
        await server.WaitAssertion(() => Assert.That(relay.Team, Is.Null, "The complete capture time must elapse again."));
        await AdvanceMatch(pair, 3);
        await server.WaitAssertion(() =>
        {
            Assert.That(relay.Team, Is.EqualTo(team));
            Assert.That(server.EntMan.GetComponent<PointLightComponent>(relayUid).Color,
                Is.EqualTo(Color.FromHex(team == CaptureTheFlagTeam.Red ? "#FF5266" : "#62BAFF")));

            GrabFlag(pair, player, enemyHome.Comp.Flag);
            MoveTo(pair, player, relayUid, -0.75f);
            Use(pair, player, relayUid);
            Assert.That(home.Comp.Score, Is.Zero);
            Assert.That(enemyHome.Comp.Score, Is.Zero);
            Assert.That(rule.Tier, Is.EqualTo(1), "Only scoring flags unlock equipment tiers.");
        });

        await AdvanceMatch(pair, 15);
        await server.WaitAssertion(() =>
        {
            Assert.That(home.Comp.ReserveCharges, Is.Zero);
            foreach (var absent in opponents)
                server.PlayerMan.SetAttachedEntity(absent, null);
        });
        await AdvanceMatch(pair, 15);
        await server.WaitAssertion(() =>
        {
            Assert.That(home.Comp.ReserveCharges, Is.Zero, "An empty opposing team pauses the relay.");
            foreach (var absent in opponents)
                ticker.MakeJoinGame(absent, EntityUid.Invalid);

            MoveTo(pair, opponent, relayUid, 0.75f);
        });
        await AdvanceMatch(pair, 15);
        await server.WaitAssertion(() =>
        {
            Assert.That(home.Comp.ReserveCharges, Is.Zero, "Contested control pauses production too.");
            MoveTo(pair, opponent, enemyHome);
        });
        await AdvanceMatch(pair, 15);
        await server.WaitAssertion(() => Assert.That(home.Comp.ReserveCharges, Is.EqualTo(1), "Production resumes from saved progress."));
        await AdvanceMatch(pair, 55);

        await server.WaitAssertion(() =>
        {
            Assert.That(home.Comp.ReserveCharges, Is.EqualTo(2));
            Assert.That(enemyHome.Comp.ReserveCharges, Is.Zero);
            var magazines = CountPrototype(pair, "MagazinePistol");
            var kits = CountPrototype(pair, "Brutepack");
            var ointments = CountPrototype(pair, "Ointment");
            var ammoText = Loc.GetString("ctf-reserve-ammo");
            var medicalText = Loc.GetString("ctf-reserve-medical");

            MoveTo(pair, opponent, home.Comp.Supply);
            Assert.That(verbs.GetLocalVerbs(home.Comp.Supply, opponent, typeof(Verb)).Select(verb => verb.Text),
                Does.Not.Contain(ammoText).And.Not.Contain(medicalText));

            MoveTo(pair, player, home.Comp.Supply);
            MoveTo(pair, teammate, home.Comp.Supply);
            Use(pair, player, home.Comp.Supply);
            Assert.That(home.Comp.ReserveCharges, Is.EqualTo(2), "The normal supply interaction does not spend a charge.");

            var ammo = verbs.GetLocalVerbs(home.Comp.Supply, player, typeof(Verb)).Single(verb => verb.Text == ammoText);
            var medical = verbs.GetLocalVerbs(home.Comp.Supply, teammate, typeof(Verb)).Single(verb => verb.Text == medicalText);
            verbs.ExecuteVerb(ammo, player, home.Comp.Supply);
            Assert.That(home.Comp.ReserveCharges, Is.EqualTo(1));
            verbs.ExecuteVerb(medical, teammate, home.Comp.Supply);
            verbs.ExecuteVerb(ammo, player, home.Comp.Supply);
            Assert.That(home.Comp.ReserveCharges, Is.Zero);
            Assert.That(CountPrototype(pair, "MagazinePistol"), Is.EqualTo(magazines + 1));
            Assert.That(CountPrototype(pair, "Brutepack"), Is.EqualTo(kits + 1));
            Assert.That(CountPrototype(pair, "Ointment"), Is.EqualTo(ointments + 1));
        });

        await AdvanceMatch(pair, 28);
        await server.WaitAssertion(() =>
        {
            Assert.That(home.Comp.ReserveCharges, Is.EqualTo(1));
            ticker.RestartRound();
            ticker.SetGamePreset("CaptureTheFlag", resetDelay: 1);
            ticker.ToggleReadyAll(true);
            ticker.StartRound();

            var newRelay = GetRule(pair).Relay;
            Assert.That(newRelay, Is.Not.EqualTo(relayUid));
            Assert.That(server.EntMan.GetComponent<CaptureTheFlagRelayComponent>(newRelay).Team, Is.Null);
            Assert.That(GetHome(pair, team).Comp.ReserveCharges, Is.Zero);
            Assert.That(GetHome(pair, OtherTeam(team)).Comp.ReserveCharges, Is.Zero);
        });
        await pair.RunUntilSynced();
        await pair.CleanReturnAsync();
    }

    [TestCase("Vox", "NitrogenTankFilled")]
    [TestCase("Plasmaman", "PlasmaTankFilledInternals")]
    public async Task ParticipantsCanBreatheWithoutGivingUpTeamGear(string species, string tankPrototype)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        await SetPlayerSpecies(pair, species);
        await StartMatch(pair, 1);

        var player = pair.Player!;
        var body = player.AttachedEntity!.Value;
        var team = Team(pair, player);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var inventory = server.System<InventorySystem>();
            var respirator = server.System<RespiratorSystem>();
            var ambientAir = server.System<AtmosphereSystem>().GetTileMixture(body);
            Assert.That(entities.GetComponent<HumanoidAppearanceComponent>(body).Species.Id, Is.EqualTo(species));
            Assert.That(ambientAir, Is.Not.Null);
            Assert.That(respirator.CanMetabolizeInhaledAir(body, ambientAir!), Is.False);

            Assert.That(inventory.TryGetSlotEntity(body, "mask", out var mask), Is.True);
            Assert.That(entities.GetComponent<MetaDataComponent>(mask!.Value).EntityPrototype?.ID, Is.EqualTo("ClothingMaskBreath"));
            Assert.That(inventory.TryGetSlotEntity(body, "suitstorage", out var tank), Is.True);
            Assert.That(entities.GetComponent<MetaDataComponent>(tank!.Value).EntityPrototype?.ID, Is.EqualTo(tankPrototype));
            var gas = entities.GetComponent<GasTankComponent>(tank.Value);
            Assert.That(respirator.CanMetabolizeInhaledAir(body, gas.Air), Is.True);
            Assert.That(entities.GetComponent<InternalsComponent>(body).GasTankEntity, Is.EqualTo(tank));
            Assert.That(server.System<InternalsSystem>().AreInternalsWorking(body), Is.True);

            AssertUniform(pair, body, team == CaptureTheFlagTeam.Red ? "ClothingUniformJumpsuitColorRed" : "ClothingUniformJumpsuitColorBlue");
            var hands = server.System<HandsSystem>();
            var startingWeapon = hands.EnumerateHeld(body).Single();
            Assert.That(entities.GetComponent<MetaDataComponent>(startingWeapon).EntityPrototype?.ID, Is.EqualTo("WeaponPistolMk58"));
            Assert.That(hands.TryGetEmptyHand(body, out _), Is.True);
        });

        await pair.RunTicksSync(Ticks(pair, TimeSpan.FromSeconds(65)));
        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(body));
            Assert.That(server.System<RespiratorSystem>().IsBreathing(body), Is.True);
            Assert.That(server.System<MobStateSystem>().IsAlive(body), Is.True);
            if (species != "Plasmaman")
                return;

            var entities = server.EntMan;
            var sensitivity = entities.GetComponent<IgniteFromGasComponent>(body);
            Assert.That(sensitivity.HasImmunity, Is.True);
            Assert.That(entities.GetComponent<FlammableComponent>(body).OnFire, Is.False);
            var inventory = server.System<InventorySystem>();
            Assert.That(inventory.TryGetSlotEntity(body, "jumpsuit", out var issued), Is.True);
            var uniform = entities.GetComponent<MetaDataComponent>(issued!.Value).EntityPrototype!.ID;
            var ordinary = entities.SpawnAtPosition(uniform, Coordinates(pair, body));
            Assert.That(entities.HasComponent<IgniteFromGasImmunityComponent>(ordinary), Is.False);
            Assert.That(inventory.TryUnequip(body, "jumpsuit", silent: true, force: true), Is.True);
            Assert.That(sensitivity.HasImmunity, Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeathDropsFlagAndRespawnsAtTeamBase()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var opponent = await AddSession(pair, "ctf_synth_opponent");
        await server.WaitPost(() => server.ResolveDependency<IServerPreferencesManager>()
            .SetProfile(opponent.UserId, 0, HumanoidCharacterProfile.DefaultWithSpecies("Synth")).Wait());
        await StartMatch(pair, 0);

        var player = pair.Player!;
        var firstBody = player.AttachedEntity!.Value;
        var team = Team(pair, player);
        var enemyHome = GetHome(pair, OtherTeam(team));
        var enemyFlag = enemyHome.Comp.Flag;
        await server.WaitAssertion(() =>
        {
            GrabFlag(pair, firstBody, enemyFlag);
            server.System<MobStateSystem>().ChangeMobState(firstBody, MobState.Dead);
            Assert.That(entMan.GetComponent<CaptureTheFlagFlagComponent>(enemyFlag).Carrier, Is.Null);
        });

        var rule = GetRule(pair);
        await pair.RunTicksSync(Ticks(pair, rule.RespawnDelay));
        await server.WaitAssertion(() =>
        {
            var newBody = player.AttachedEntity!.Value;
            Assert.That(newBody, Is.Not.EqualTo(firstBody));
            Assert.That(entMan.EntityExists(firstBody), Is.False);
            Assert.That(Team(pair, player), Is.EqualTo(team));
            Assert.That(GetHome(pair, team).Comp.Spawns.Select(id => Coordinates(pair, id)), Does.Contain(Coordinates(pair, newBody)));
        });

        await pair.RunTicksSync(Ticks(pair, rule.DroppedFlagDuration - rule.RespawnDelay));
        await server.WaitAssertion(() =>
        {
            Assert.That(enemyHome.Comp.Flag, Is.Not.EqualTo(enemyFlag));
            Assert.That(entMan.GetComponent<CaptureTheFlagFlagComponent>(enemyHome.Comp.Flag).AtHome, Is.True);
            var coordinates = Coordinates(pair, enemyHome);
            var lostFlag = enemyHome.Comp.Flag;
            server.System<SharedTransformSystem>().SetCoordinates(lostFlag,
                new EntityCoordinates(coordinates.EntityId, new Vector2(30f, 30f)));
        });

        await pair.RunTicksSync(Ticks(pair, TimeSpan.Zero));
        await server.WaitAssertion(() =>
            Assert.That(entMan.GetComponent<CaptureTheFlagFlagComponent>(enemyHome.Comp.Flag).AtHome, Is.True));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EmptyTeamRebalancesOnRespawnAndReconnectKeepsAssignment()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        var ticker = server.System<GameTicker>();
        var initialDummies = await StartMatch(pair, 3);

        var carrier = pair.Player!;
        var startingTeam = Team(pair, carrier);
        var carrierBody = carrier.AttachedEntity!.Value;
        var remainingTeammate = initialDummies.Single(s => Team(pair, s) == startingTeam);
        var opponents = initialDummies.Where(s => Team(pair, s) != startingTeam).ToArray();
        Assert.That(opponents, Has.Length.EqualTo(2));

        var spectator = await AddSession(pair, "ctf_spectator");
        var opponentFlag = GetHome(pair, OtherTeam(startingTeam)).Comp.Flag;
        var teammateBody = remainingTeammate.AttachedEntity!.Value;
        await server.WaitAssertion(() =>
        {
            ticker.JoinAsObserver(spectator);
            Assert.That(server.EntMan.HasComponent<CaptureTheFlagBodyComponent>(spectator.AttachedEntity), Is.False);
            GrabFlag(pair, carrierBody, opponentFlag);
        });

        foreach (var opponent in opponents)
            await server.RemoveDummySession(opponent);

        var home = GetHome(pair, startingTeam);
        await server.WaitAssertion(() =>
        {
            MoveTo(pair, carrierBody, home);
            Use(pair, carrierBody, home);
            Assert.That(home.Comp.Score, Is.Zero, "An empty team cannot concede a point.");
            server.System<MobStateSystem>().ChangeMobState(teammateBody, MobState.Dead);
        });

        await pair.RunTicksSync(Ticks(pair, GetRule(pair).RespawnDelay));
        await server.WaitAssertion(() =>
        {
            Assert.That(remainingTeammate.AttachedEntity, Is.Not.EqualTo(teammateBody));
            Assert.That(Team(pair, remainingTeammate), Is.EqualTo(OtherTeam(startingTeam)));
            Assert.That(Team(pair, carrier), Is.EqualTo(startingTeam));
            Assert.That(server.System<HandsSystem>().IsHolding(carrierBody, opponentFlag), Is.True);
        });

        var rebalancedBody = remainingTeammate.AttachedEntity!.Value;
        await server.RemoveDummySession(remainingTeammate);
        var reconnected = await AddSession(pair, remainingTeammate.Name);
        await server.WaitAssertion(() =>
        {
            Assert.That(reconnected.UserId, Is.EqualTo(remainingTeammate.UserId));
            Assert.That(reconnected.AttachedEntity, Is.EqualTo(rebalancedBody));
            Assert.That(Team(pair, reconnected), Is.EqualTo(OtherTeam(startingTeam)));
        });

        var firstLate = await AddSession(pair, "ctf_late_1");
        var secondLate = await AddSession(pair, "ctf_late_2");
        await server.WaitAssertion(() =>
        {
            ticker.MakeJoinGame(firstLate, EntityUid.Invalid);
            ticker.MakeJoinGame(secondLate, EntityUid.Invalid);
            Assert.That(Team(pair, firstLate), Is.Not.EqualTo(Team(pair, secondLate)));
            Assert.That(Team(pair, carrier), Is.EqualTo(startingTeam));
            Assert.That(server.System<HandsSystem>().IsHolding(carrierBody, opponentFlag), Is.True);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LastCarrierWaitsForReconnectAndWinsByForfeit()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        var opponent = (await StartMatch(pair, 1)).Single();
        var carrier = pair.Player!;
        var body = carrier.AttachedEntity!.Value;
        var team = Team(pair, carrier);
        var home = GetHome(pair, team);
        var flag = GetHome(pair, OtherTeam(team)).Comp.Flag;
        var rule = GetRule(pair);
        await server.WaitAssertion(() => GrabFlag(pair, body, flag));

        await server.RemoveDummySession(opponent);
        await pair.RunTicksSync(Ticks(pair, TimeSpan.Zero));
        await server.WaitAssertion(() =>
        {
            Assert.That(rule.ForfeitAt, Is.Not.Null);
            Assert.That(rule.ReconnectGrace, Is.EqualTo(TimeSpan.FromSeconds(60)));
            MoveTo(pair, body, home);
            Use(pair, body, home);
            Assert.That(home.Comp.Score, Is.Zero);
            Assert.That(server.System<HandsSystem>().IsHolding(body, flag), Is.True);
            Assert.That(Team(pair, carrier), Is.EqualTo(team));
        });

        var returned = await AddSession(pair, opponent.Name);
        await pair.RunTicksSync(Ticks(pair, TimeSpan.Zero));
        await server.WaitAssertion(() =>
        {
            Assert.That(returned.UserId, Is.EqualTo(opponent.UserId));
            Assert.That(Team(pair, returned), Is.EqualTo(OtherTeam(team)));
            Assert.That(rule.ForfeitAt, Is.Null);
            Use(pair, body, home);
            Assert.That(home.Comp.Score, Is.EqualTo(1), "Returning opposition makes the held flag capturable again.");
        });

        await server.RemoveDummySession(returned);
        await WaitForForfeit(pair, rule);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<GameTicker>().RunLevel, Is.EqualTo(GameRunLevel.PostRound));
            Assert.That(rule.ForfeitWinner, Is.EqualTo(team));
            Assert.That(home.Comp.Score, Is.EqualTo(1));
            var teamName = Loc.GetString(team == CaptureTheFlagTeam.Red ? "ctf-team-red" : "ctf-team-blue");
            Assert.That(GetRoundEndReport(pair), Does.Contain(Loc.GetString("ctf-round-forfeit", ("team", teamName))));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EstablishedMatchIsAbandonedWhenBothTeamsLeave()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        var opponent = (await StartMatch(pair, 1)).Single();
        var rule = GetRule(pair);

        await server.RemoveDummySession(opponent);
        await server.WaitPost(() => server.PlayerMan.SetAttachedEntity(pair.Player!, null));
        await WaitForForfeit(pair, rule);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<GameTicker>().RunLevel, Is.EqualTo(GameRunLevel.PostRound));
            Assert.That(rule.ForfeitWinner, Is.Null);
            Assert.That(GetRoundEndReport(pair), Does.Contain(Loc.GetString("ctf-round-abandoned")));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ForcedSoloPracticeDoesNotForfeit()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        await StartMatch(pair, 0, force: true);
        var rule = GetRule(pair);

        await pair.RunTicksSync(Ticks(pair, rule.ReconnectGrace));
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<GameTicker>().RunLevel, Is.EqualTo(GameRunLevel.InRound));
            Assert.That(rule.ForfeitAt, Is.Null);
            Assert.That(rule.Players, Has.Count.EqualTo(1));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PostRoundLobbyPlayerJoinsAsAnObserver()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        await StartMatch(pair, 1);
        var late = await AddSession(pair, "ctf_postround_viewer");
        await server.WaitAssertion(() =>
        {
            var ticker = server.System<GameTicker>();
            var rule = GetRule(pair);
            ticker.EndRound();
            Assert.That(rule.Finished, Is.True);
            Assert.That(late.AttachedEntity, Is.Null);
            ticker.MakeJoinGame(late, EntityUid.Invalid);
            Assert.That(server.EntMan.HasComponent<GhostComponent>(late.AttachedEntity), Is.True);
            Assert.That(rule.Players, Does.Not.ContainKey(late.UserId));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AnUnreadyPlayerCannotStartAMatch()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        await AddSession(pair, "ctf_unready");
        server.CfgMan.SetCVar(CCVars.GameLobbyFallbackEnabled, true);
        server.CfgMan.SetCVar(CCVars.GameLobbyFallbackPreset, "Extended");

        await server.WaitAssertion(() =>
        {
            var ticker = server.System<GameTicker>();
            ticker.SetGamePreset("ctf", resetDelay: 1);
            ticker.ToggleReady(pair.Player!, true);
            Assert.That(ticker.ReadyPlayerCount(), Is.EqualTo(1));
            ticker.StartRound();
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));
            Assert.That(server.EntMan.EntityQuery<CaptureTheFlagBodyComponent>(), Is.Empty);
            Assert.That(server.EntMan.EntityQuery<CaptureTheFlagBaseComponent>(includePaused: true), Is.Empty);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TiedMatchEndsAtTimeLimit()
    {
        const string shortRule = """
            - type: entity
              id: CaptureTheFlagRule
              parent: BaseGameRule
              components:
              - type: GameRule
                minPlayers: 2
              - type: CaptureTheFlagRule
                matchDuration: 1
            """;

        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Destructive = true });
        var server = pair.Server;
        await server.WaitPost(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var changed = new Dictionary<Type, HashSet<string>>();
            prototypes.LoadString(shortRule, overwrite: true, changed: changed);
            prototypes.ReloadPrototypes(changed);
        });

        await StartMatch(pair, 1);
        var rule = GetRule(pair);
        Assert.That(rule.MatchDuration, Is.EqualTo(TimeSpan.FromSeconds(1)));
        await pair.RunTicksSync(Ticks(pair, rule.MatchDuration));
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<GameTicker>().RunLevel, Is.EqualTo(GameRunLevel.PostRound));
            Assert.That(GetRoundEndReport(pair), Does.Contain(Loc.GetString("ctf-round-draw")));
        });
        await pair.CleanReturnAsync();
    }

    private static async Task<ICommonSession[]> StartMatch(TestPair pair, int dummyCount, bool force = false, string? dummySpecies = null)
    {
        var server = pair.Server;
        // The integration loop does not run the round intro's rendered frame callback.
        await pair.Client.WaitPost(() => pair.Client.CfgMan.SetCVar(GoobCVars.RoundIntroEnabled, false));
        server.CfgMan.SetCVar(CCVars.GridFill, true);
        var dummies = new ICommonSession[dummyCount];
        for (var i = 0; i < dummyCount; i++)
            dummies[i] = await AddSession(pair, $"ctf_player_{i}");

        await server.WaitAssertion(() =>
        {
            if (dummySpecies != null)
            {
                var preferences = server.ResolveDependency<IServerPreferencesManager>();
                foreach (var dummy in dummies)
                    preferences.SetProfile(dummy.UserId, 0, HumanoidCharacterProfile.DefaultWithSpecies(dummySpecies)).Wait();
            }

            var ticker = server.System<GameTicker>();
            var preset = ticker.FindGamePreset("ctf");
            Assert.That(preset?.ID, Is.EqualTo("CaptureTheFlag"));
            Assert.That(preset!.MinPlayers, Is.EqualTo(2));
            ticker.SetGamePreset(preset, resetDelay: 1);
            ticker.ToggleReadyAll(true);
            ticker.StartRound(force);
            Assert.That(ticker.CurrentPreset?.ID, Is.EqualTo(preset.ID));
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
        });
        await pair.RunTicksSync(5);
        return dummies;
    }

    private static Task SetPlayerSpecies(TestPair pair, string species)
    {
        var server = pair.Server;
        return server.WaitPost(() => server.ResolveDependency<IServerPreferencesManager>()
            .SetProfile(pair.Player!.UserId, 0, HumanoidCharacterProfile.DefaultWithSpecies(species)).Wait());
    }

    private static async Task<ICommonSession> AddSession(TestPair pair, string name)
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

    private static int Ticks(TestPair pair, TimeSpan time)
    {
        var deadline = time + CaptureTheFlagRuleSystem.ClockInterval;
        return (int) Math.Ceiling(pair.Server.Timing.TickRate * deadline.TotalSeconds) + 2;
    }

    private static Task AdvanceMatch(TestPair pair, double seconds)
    {
        return pair.RunTicksSync(Ticks(pair, TimeSpan.FromSeconds(seconds)));
    }

    private static async Task WaitForForfeit(TestPair pair, CaptureTheFlagRuleComponent rule)
    {
        await AdvanceMatch(pair, 1);
        var remaining = TimeSpan.Zero;
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(rule.ForfeitAt, Is.Not.Null);
            remaining = rule.ForfeitAt!.Value - pair.Server.Timing.CurTime;
        });
        await pair.RunTicksSync(Ticks(pair, remaining + CaptureTheFlagRuleSystem.ClockInterval));
        await pair.RunUntilSynced();
    }

    private static CaptureTheFlagScoreboard? GetScoreboard(TestPair pair)
    {
        var ui = pair.Client.ResolveDependency<IUserInterfaceManager>();
        return ui.GetActiveUIWidgetOrNull<GhostGui>()?.Parent?.Children.OfType<CaptureTheFlagScoreboard>().SingleOrDefault();
    }

    private static void AssertScoreboard(TestPair pair, int red, int blue, int tier)
    {
        var scoreboard = GetScoreboard(pair);
        Assert.That(scoreboard, Is.Not.Null);
        var labels = scoreboard!.Children.Single().Children.OfType<BoxContainer>().Single()
            .Children.OfType<Label>().Select(label => label.Text);
        var team = Team(pair, pair.Player!);
        var redText = Loc.GetString("ctf-hud-red", ("score", red));
        var blueText = Loc.GetString("ctf-hud-blue", ("score", blue));
        Assert.That(labels, Is.EqualTo(new[]
        {
            team == CaptureTheFlagTeam.Red ? Loc.GetString("ctf-hud-yours", ("team", redText)) : redText,
            team == CaptureTheFlagTeam.Blue ? Loc.GetString("ctf-hud-yours", ("team", blueText)) : blueText,
            Loc.GetString("ctf-hud-tier", ("tier", tier)),
        }));
    }

    private static int CountPrototype(TestPair pair, string id)
    {
        return pair.Server.EntMan.EntityQuery<MetaDataComponent>().Count(c => c.EntityPrototype?.ID == id);
    }

    private static string GetRoundEndReport(TestPair pair)
    {
        var report = new RoundEndTextAppendEvent();
        pair.Server.EntMan.EventBus.RaiseEvent(EventSource.Local, report);
        return report.Text;
    }

    private static void AssertArenaGunUse(TestPair pair, EntityUid oni, string prototype)
    {
        var entities = pair.Server.EntMan;
        var arena = GetRule(pair).Grid;
        var query = entities.AllEntityQueryEnumerator<GunComponent, MetaDataComponent, TransformComponent>();
        var issued = 0;
        while (query.MoveNext(out var uid, out var gun, out var metadata, out var transform))
        {
            if (metadata.EntityPrototype?.ID != prototype || transform.GridUid != arena)
                continue;

            var restrictions = entities.GetComponent<RestrictGunshotsByUserTagComponent>(uid);
            Assert.That(restrictions.DoesntContain.Any(tag => tag.Id == "Oni"), Is.False, metadata.EntityPrototype.ID);
            if (prototype == "WeaponPistolMk58")
            {
                var shot = new ShotAttemptedEvent { User = oni, Used = (uid, gun) };
                entities.EventBus.RaiseLocalEvent(uid, ref shot);
                Assert.That(shot.Cancelled, Is.False);
            }
            issued++;
        }

        Assert.That(issued, Is.EqualTo(2), "Check the guns issued to both teams.");
        var ordinaryGun = entities.SpawnAttachedTo(prototype, Coordinates(pair, oni));
        Assert.That(entities.GetComponent<RestrictGunshotsByUserTagComponent>(ordinaryGun).DoesntContain.Any(tag => tag.Id == "Oni"));
        var ordinaryShot = new ShotAttemptedEvent { User = oni, Used = (ordinaryGun, entities.GetComponent<GunComponent>(ordinaryGun)) };
        entities.EventBus.RaiseLocalEvent(ordinaryGun, ref ordinaryShot);
        Assert.That(ordinaryShot.Cancelled, Is.True, "The ordinary gun still excludes Oni.");
        entities.DeleteEntity(ordinaryGun);
    }

    private static CaptureTheFlagRuleComponent GetRule(TestPair pair)
    {
        return pair.Server.EntMan.EntityQuery<CaptureTheFlagRuleComponent>().Single();
    }

    private static CaptureTheFlagTeam Team(TestPair pair, ICommonSession session)
    {
        return GetRule(pair).Players[session.UserId].Team;
    }

    private static CaptureTheFlagTeam OtherTeam(CaptureTheFlagTeam team)
    {
        return team == CaptureTheFlagTeam.Red ? CaptureTheFlagTeam.Blue : CaptureTheFlagTeam.Red;
    }

    private static Entity<CaptureTheFlagBaseComponent> GetHome(TestPair pair, CaptureTheFlagTeam team)
    {
        var uid = GetRule(pair).Homes[team];
        return (uid, pair.Server.EntMan.GetComponent<CaptureTheFlagBaseComponent>(uid));
    }

    private static void AssertUniform(TestPair pair, EntityUid actor, string prototype)
    {
        Assert.That(pair.Server.System<InventorySystem>().TryGetSlotEntity(actor, "jumpsuit", out var equipment), Is.True);
        Assert.That(pair.Server.EntMan.GetComponent<MetaDataComponent>(equipment!.Value).EntityPrototype?.ID, Is.EqualTo(prototype));
    }

    private static void GrabFlag(TestPair pair, EntityUid actor, EntityUid flag)
    {
        MoveTo(pair, actor, flag);
        var hands = pair.Server.System<HandsSystem>();
        if (!hands.TryGetEmptyHand(actor, out var hand))
        {
            Assert.That(hands.TryDrop(actor, hands.GetActiveItem(actor)!.Value), Is.True);
            Assert.That(hands.TryGetEmptyHand(actor, out hand), Is.True);
        }

        hands.TrySetActiveHand(actor, hand);
        Assert.That(pair.Server.System<SharedInteractionSystem>().InteractHand(actor, flag), Is.True);
        Assert.That(hands.IsHolding(actor, flag), Is.True);
    }

    private static void SetActiveItem(TestPair pair, EntityUid actor, EntityUid item)
    {
        var hands = pair.Server.System<HandsSystem>();
        Assert.That(hands.IsHolding(actor, item, out var hand), Is.True);
        hands.TrySetActiveHand(actor, hand);
    }

    private static void Capture(TestPair pair, EntityUid actor, Entity<CaptureTheFlagBaseComponent> enemy,
        Entity<CaptureTheFlagBaseComponent> home)
    {
        GrabFlag(pair, actor, enemy.Comp.Flag);
        MoveTo(pair, actor, home);
        Use(pair, actor, home);
    }

    private static void Use(TestPair pair, EntityUid actor, EntityUid target)
    {
        var interaction = pair.Server.System<SharedInteractionSystem>();
        if (pair.Server.System<HandsSystem>().TryGetActiveItem(actor, out var item))
            interaction.InteractUsing(actor, item.Value, target, Coordinates(pair, target));
        else
            interaction.InteractHand(actor, target);
    }

    private static EntityCoordinates Coordinates(TestPair pair, EntityUid entity)
    {
        return pair.Server.EntMan.GetComponent<TransformComponent>(entity).Coordinates;
    }

    private static void MoveTo(TestPair pair, EntityUid body, EntityUid target, float xOffset = 0f)
    {
        var targetPosition = Coordinates(pair, target);
        pair.Server.System<SharedTransformSystem>().SetCoordinates(body,
            new EntityCoordinates(targetPosition.EntityId, targetPosition.Position + new Vector2(xOffset, -0.25f)));
    }
}
