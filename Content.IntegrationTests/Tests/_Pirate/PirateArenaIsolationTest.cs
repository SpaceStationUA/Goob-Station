// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Pirate.Arenas;
using Content.Server._Pirate.GunGame.Components;
using Content.IntegrationTests.Pair;
using Content.Server.GameTicking;
using Content.Server._EinsteinEngines.Atmos.Components;
using Content.Server.Store.Systems;
using Content.Server.Preferences.Managers;
using Content.Shared.Body.Part;
using Content.Shared.Body.Components;
using Content.Shared.Mobs;
using Content.Shared.Body.Systems;
using Content.Shared.CCVar;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Mobs.Systems;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests._Pirate;

[TestFixture]
public sealed class PirateArenaIsolationTest
{
    [Test]
    public async Task SuspicionStockIsPrivateAndSurvivesStoreRefresh()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var stores = server.System<StoreSystem>();
            var standard = em.SpawnEntity("UplinkImplant", map.GridCoords);
            var standardStore = em.GetComponent<StoreComponent>(standard);
            var arena = em.SpawnEntity("SusTraitorUplinkImplant", map.GridCoords);
            var arenaStore = em.GetComponent<StoreComponent>(arena);
            var detective = em.SpawnEntity("SusDetectiveUplinkImplant", map.GridCoords);
            var detectiveStore = em.GetComponent<StoreComponent>(detective);
            var buyer = em.SpawnEntity("MobHuman", map.GridCoords);
            const string listingId = "UplinkPistolViperSSS";
            stores.RefreshAllListings(arenaStore);
            Assert.That(standardStore.FullListingsCatalog.Any(x => x.ID == listingId), Is.False);
            var listing = arenaStore.FullListingsCatalog.Single(l => l.ID == listingId);
            Assert.That(stores.GetAvailableListings(buyer, arena, arenaStore).Any(l => l.ID == listingId));
            Assert.That(stores.GetAvailableListings(buyer, detective, detectiveStore).Any(l => l.ID == listingId), Is.False);
            var before = arenaStore.Balance["Telecrystal"];
            var price = listing.Cost["Telecrystal"];
            em.EventBus.RaiseLocalEvent(arena, new StoreBuyListingMessage(listingId) { Actor = buyer });
            Assert.That(arenaStore.Balance["Telecrystal"], Is.EqualTo(before - price));
            Assert.That(listing.PurchaseAmount, Is.EqualTo(1));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GunGameRespawnsAnOfflineDeathAndEndsWithoutRearming()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        var em = server.EntMan;
        var ticker = server.System<GameTicker>();
        var other = await Connect(pair, "ArenaDisconnect");
        await server.WaitPost(() => server.ResolveDependency<IServerPreferencesManager>()
            .SetProfile(other.UserId, 0, HumanoidCharacterProfile.DefaultWithSpecies("Experiment")).Wait());
        await server.WaitPost(() =>
        {
            server.CfgMan.SetCVar(CCVars.GameMap, "MW-Dock");
            ticker.SetGamePreset("GunGame", resetDelay: 1);
            ticker.ToggleReadyAll(true);
            ticker.StartRound();
        });
        await pair.RunTicksSync(5);
        var victim = other.AttachedEntity!.Value;
        var shooter = pair.Player!.AttachedEntity!.Value;
        var rule = em.EntityQuery<GunGameRuleComponent>().Single();
        void Kill(EntityUid target)
        {
            var chest = server.System<SharedBodySystem>().GetBodyChildrenOfType(target, BodyPartType.Chest).Single().Id;
            var damage = new DamageSpecifier(server.ProtoMan.Index<DamageTypePrototype>("Blunt"), 400);
            server.System<DamageableSystem>().ChangeDamage(chest, damage, ignoreResistances: true, origin: shooter, canMiss: false);
            Assert.That(server.System<MobStateSystem>().IsDead(target), Is.True);
        }

        await server.RemoveDummySession(other);
        await server.WaitAssertion(() =>
        {
            rule.KillsPerWeapon = 2;
            Kill(victim);
            Assert.That(rule.PlayerInfo[pair.Player.UserId].Kills, Is.EqualTo(1));
        });
        await pair.RunTicksSync(server.Timing.TickRate * 5);
        var returning = await Connect(pair, "ArenaDisconnect");
        await pair.RunTicksSync(12);
        var next = returning.AttachedEntity!.Value;
        await server.WaitAssertion(() =>
        {
            Assert.That(next, Is.Not.EqualTo(victim));
            Assert.That(em.EntityExists(victim), Is.False);
            Assert.That(em.GetComponent<GunGameTrackerComponent>(next).CurrentRewards, Is.Not.Empty);
            ticker.MakeJoinGame(returning, EntityUid.Invalid, silent: true);
            Assert.That(returning.AttachedEntity, Is.EqualTo(next));
            var player = rule.PlayerInfo[pair.Player.UserId];
            while (player.RewardQueue.Count > 1)
                player.RewardQueue.Dequeue();
            Kill(next);
            Assert.That(rule.Victor, Is.EqualTo(pair.Player.UserId));
        });
        await pair.RunTicksSync(server.Timing.TickRate * 5);
        await server.WaitAssertion(() =>
        {
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PostRound));
            // A gibbed body may transfer its mind to a brain or a ghost after the winning hit.
            Assert.That(em.EntityQuery<GunGameTrackerComponent>().Any(body =>
                body.Player == returning.UserId && (!body.DeathRecorded || body.CurrentRewards.Count != 0)), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("Vox")]
    [TestCase("Plasmaman")]
    public async Task GunGameTradeCreditsBothPlayersWithoutHealingTheDefeatedShooter(string species)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        var ticker = server.System<GameTicker>();
        var opponent = await Connect(pair, "ArenaTrade");
        await server.WaitPost(() => server.ResolveDependency<IServerPreferencesManager>()
            .SetProfile(pair.Player!.UserId, 0, HumanoidCharacterProfile.DefaultWithSpecies(species)).Wait());
        await server.WaitAssertion(() =>
        {
            server.CfgMan.SetCVar(CCVars.GameMap, "MW-Dock");
            ticker.SetGamePreset("GunGame", resetDelay: 1);
            ticker.ToggleReadyAll(true);
            ticker.StartRound();

            var rule = server.EntMan.EntityQuery<GunGameRuleComponent>().Single();
            var shooter = pair.Player!.AttachedEntity!.Value;
            var other = opponent.AttachedEntity!.Value;
            var internals = server.EntMan.GetComponent<InternalsComponent>(shooter);
            var startTank = internals.GasTankEntity;
            Assert.That(startTank, Is.Not.Null);
            var states = server.System<MobStateSystem>();
            states.ChangeMobState(other, MobState.Dead, origin: shooter);
            Assert.That(internals.GasTankEntity, Is.EqualTo(startTank));
            if (species == "Plasmaman")
                Assert.That(server.EntMan.GetComponent<IgniteFromGasComponent>(shooter).HasImmunity, Is.True);
            states.ChangeMobState(shooter, MobState.Dead, origin: other);
            Assert.That(rule.PlayerInfo[pair.Player.UserId].RewardQueue, Has.Count.EqualTo(30));
            Assert.That(rule.PlayerInfo[opponent.UserId].RewardQueue, Has.Count.EqualTo(30));
            Assert.That(states.IsDead(other), Is.True);
            Assert.That(server.EntMan.GetComponent<GunGameTrackerComponent>(other).CurrentRewards, Is.Empty);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GunGameStageUpgradeHealsCriticalShooterBeforeEquippingWeapon()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        var ticker = server.System<GameTicker>();
        var opponent = await Connect(pair, "ArenaDeferredShot");
        await server.WaitPost(() =>
        {
            var preferences = server.ResolveDependency<IServerPreferencesManager>();
            preferences.SetProfile(pair.Player!.UserId, 0, HumanoidCharacterProfile.DefaultWithSpecies("Experiment")).Wait();
            preferences.SetProfile(opponent.UserId, 0, HumanoidCharacterProfile.DefaultWithSpecies("Experiment")).Wait();
        });
        await server.WaitAssertion(() =>
        {
            server.CfgMan.SetCVar(CCVars.GameMap, "MW-Dock");
            ticker.SetGamePreset("GunGame", resetDelay: 1);
            ticker.ToggleReadyAll(true);
            ticker.StartRound();

            var shooter = pair.Player!.AttachedEntity!.Value;
            var victim = opponent.AttachedEntity!.Value;
            var hands = server.System<SharedHandsSystem>();
            var guns = server.System<SharedGunSystem>();
            foreach (var participant in new[] { shooter, victim })
            {
                var starterWeapons = hands.EnumerateHeld(participant).ToArray();
                Assert.That(starterWeapons, Has.Length.EqualTo(2));
                foreach (var weapon in starterWeapons)
                {
                    Assert.That(server.EntMan.GetComponent<MetaDataComponent>(weapon).EntityPrototype?.ID, Is.EqualTo("WeaponSubMachineGunAtreides"));
                    Assert.That(server.EntMan.HasComponent<ArenaMagazineDisplayComponent>(weapon), Is.True);
                    Assert.That(guns.GetMagazineEntity(weapon), Is.Not.Null);
                    Assert.That(guns.GetAmmoCount(weapon), Is.EqualTo(30));
                }
            }

            var states = server.System<MobStateSystem>();
            DamageChest(shooter, 160, victim);
            Assert.That(states.IsCritical(shooter), Is.True);

            // The shot already left the weapon before the shooter was incapacitated.
            DamageChest(victim, 250, shooter);
            Assert.That(states.IsDead(victim), Is.True);
            Assert.That(states.IsAlive(shooter), Is.True);

            var rule = server.EntMan.EntityQuery<GunGameRuleComponent>().Single();
            Assert.That(rule.PlayerInfo[pair.Player.UserId].RewardQueue.Peek().Id, Is.EqualTo("GunGameDeathSquad"));
            var held = hands.EnumerateHeld(shooter)
                .Select(item => server.EntMan.GetComponent<MetaDataComponent>(item).EntityPrototype?.ID);
            Assert.That(held, Does.Contain("WeaponPulseCarbine"));
        });
        await pair.CleanReturnAsync();

        void DamageChest(EntityUid body, int amount, EntityUid origin)
        {
            var chest = server.System<SharedBodySystem>().GetBodyChildrenOfType(body, BodyPartType.Chest).Single().Id;
            var damage = new DamageSpecifier(server.ProtoMan.Index<DamageTypePrototype>("Blunt"), amount);
            server.System<DamageableSystem>().ChangeDamage(chest, damage, ignoreResistances: true, origin: origin, canMiss: false);
        }
    }

    [Test]
    public async Task GunGameQuartermasterStageDisplaysTheEquippedKnuckles()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        var opponent = await Connect(pair, "ArenaQuartermaster");
        await server.WaitPost(() => server.ResolveDependency<IServerPreferencesManager>()
            .SetProfile(pair.Player!.UserId, 0, HumanoidCharacterProfile.DefaultWithSpecies("Human")).Wait());
        await server.WaitAssertion(() =>
        {
            var ticker = server.System<GameTicker>();
            server.CfgMan.SetCVar(CCVars.GameMap, "MW-Dock");
            ticker.SetGamePreset("GunGame", resetDelay: 1);
            ticker.ToggleReadyAll(true);
            ticker.StartRound();
            var player = pair.Player!;
            var shooter = player.AttachedEntity!.Value;
            var rule = server.EntMan.EntityQuery<GunGameRuleComponent>().Single();
            var stages = rule.PlayerInfo[player.UserId].RewardQueue;
            while (stages.Count > 3)
                stages.Dequeue();

            server.System<MobStateSystem>().ChangeMobState(opponent.AttachedEntity!.Value, MobState.Dead, origin: shooter);
            Assert.That(stages.Peek().Id, Is.EqualTo("GunGameQM"));
            Assert.That(server.System<InventorySystem>().TryGetSlotEntity(shooter, "gloves", out var gloves), Is.True);
            var weapon = server.EntMan.GetComponent<MetaDataComponent>(gloves!.Value).EntityPrototype!;
            Assert.That(weapon.ID, Is.EqualTo("ClothingHandsKnuckleDustersQM"));
            var report = new RoundEndTextAppendEvent();
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, report);
            Assert.That(report.Text, Does.Contain(weapon.Name));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GunGameWithOneReadyPlayerStaysInLobby()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var ticker = server.System<GameTicker>();
            server.CfgMan.SetCVar(CCVars.GameLobbyFallbackEnabled, true);
            server.CfgMan.SetCVar(CCVars.GameMap, "MW-Dock");
            ticker.SetGamePreset("GunGame", resetDelay: 1);
            ticker.ToggleReadyAll(true);
            ticker.StartRound();
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));
        });
        await pair.CleanReturnAsync();
    }

    private static async Task<ICommonSession> Connect(TestPair pair, string name)
    {
        var server = pair.Server;
        var session = await server.AddDummySession(name);
        await server.WaitPost(() =>
        {
            if (session.Status == SessionStatus.Connected)
                server.PlayerMan.JoinGame(session);
        });
        await pair.RunTicksSync(5);
        return session;
    }
}
