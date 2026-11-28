// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server._Pirate.Traits;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Rotting;
using Content.Server.Lathe;
using Content.Server.Power.Components;
using Content.Shared._Pirate.Cassette;
using Content.Shared._Pirate.Medical.IV;
using Content.Shared._Pirate.Ninja;
using Content.Shared._Pirate.Traits;
using Content.Shared.Actions;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Communications;
using Content.Shared.Construction.Components;
using Content.Shared.CriminalRecords.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Materials;
using Content.Shared.Mobs.Systems;
using Content.Shared.Ninja.Components;
using Content.Shared.Ninja.Systems;
using Content.Shared.Preferences;
using Content.Shared.Research.Prototypes;
using Content.Shared.Research.Systems;
using Content.Shared.Temperature.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Pirate.Forum;

[TestFixture]
public sealed class ForumRequestsTest
{
    [Test]
    public async Task NinjaConsoleProgressFollowsCloakDuringHack()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var ninja = entities.SpawnAttachedTo("MobHuman", map.GridCoords);
            entities.EnsureComponent<SpaceNinjaComponent>(ninja);
            var suit = entities.SpawnAttachedTo("ClothingOuterSuitSpaceNinja", map.GridCoords);
            Assert.That(server.System<InventorySystem>().TryEquip(ninja, suit, "outerClothing", force: true), Is.True);
            var toggle = server.System<ItemToggleSystem>();
            var research = new ResearchStealDoAfterEvent();
            Assert.That(NinjaHackVisibility.IsHidden(entities, ninja, research), Is.False);
            Assert.That(toggle.TryActivate(suit, ninja), Is.True);
            Assert.That(NinjaHackVisibility.IsHidden(entities, ninja, research), Is.True);
            Assert.That(NinjaHackVisibility.IsHidden(entities, ninja, new TerrorDoAfterEvent()), Is.True);
            Assert.That(NinjaHackVisibility.IsHidden(entities, ninja, new CriminalRecordsHackDoAfterEvent()), Is.True);
            Assert.That(NinjaHackVisibility.IsHidden(entities, ninja, new DrainDoAfterEvent()), Is.False);
            Assert.That(toggle.TryDeactivate(suit, ninja), Is.True);
            Assert.That(NinjaHackVisibility.IsHidden(entities, ninja, research), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CassetteActionsWorkAlongsideHeadset()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var wearer = entities.SpawnAttachedTo("MobHuman", map.GridCoords);
            var headset = entities.SpawnAttachedTo("ClothingHeadsetGrey", map.GridCoords);
            var player = entities.SpawnAttachedTo("RMCCassettePlayer", map.GridCoords);
            var inventory = server.System<InventorySystem>();
            Assert.That(inventory.TryEquip(wearer, headset, "cassettePlayer", silent: true), Is.False);
            Assert.That(inventory.TryEquip(wearer, headset, "ears"), Is.True);
            Assert.That(inventory.TryEquip(wearer, player, "cassettePlayer"), Is.True);
            var action = entities.GetComponent<CassettePlayerComponent>(player).PlayPauseAction;
            var actions = server.System<SharedActionsSystem>();
            Assert.That(action, Is.Not.Null);
            Assert.That(actions.GetActions(wearer).Any(a => a.Owner == action), Is.True);
            Assert.That(inventory.TryUnequip(wearer, "cassettePlayer"), Is.True);
            Assert.That(actions.GetActions(wearer).Any(a => a.Owner == action), Is.False);
            Assert.That(inventory.TryGetSlotEntity(wearer, "ears", out var remaining) && remaining == headset, Is.True);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("PirateSlowCorpseDecay", 0.75f)]
    [TestCase("PirateFastCorpseDecay", 1.25f)]
    public async Task CorpseTraitsScaleBothStagesAndRespectSpecies(string trait, float multiplier)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var traits = server.System<TraitSystem>();
        var rot = server.System<RottingSystem>();
        EntityUid body = default;
        await server.WaitAssertion(() =>
        {
            var moles = new float[Atmospherics.AdjustedNumberOfGases];
            moles[(int) Gas.Oxygen] = 21.824779f;
            moles[(int) Gas.Nitrogen] = 82.10312f;
            server.System<AtmosphereSystem>().SetMapAtmosphere(map.MapUid, false, new GasMixture(moles, Atmospherics.T20C));
            body = entities.SpawnAttachedTo("MobHuman", map.GridCoords);
            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human");
            traits.ApplyProfileTraits(body, profile.WithTraitPreferences(["PirateSlowCorpseDecay", "PirateFastCorpseDecay"]), null, "Passenger");
            Assert.That(entities.HasComponent<CorpseDecayModifierComponent>(body), Is.False);
            traits.ApplyProfileTraits(body, profile.WithTraitPreferences([trait]), null, "Passenger");
            Assert.That(entities.GetComponent<CorpseDecayModifierComponent>(body).Multiplier, Is.EqualTo(multiplier));
            var perish = entities.GetComponent<PerishableComponent>(body);
            Assert.That(rot.IsRotProgressing(body, perish), Is.False);
            var damage = new DamageSpecifier { DamageDict = { ["Asphyxiation"] = 500 } };
            server.System<DamageableSystem>().TryChangeDamage(body, damage, ignoreResistances: true);
            Assert.That(server.System<MobStateSystem>().IsDead(body), Is.True);
            Assert.That(rot.IsRotProgressing(body, perish), Is.True);

            var vox = entities.SpawnAttachedTo("MobVox", map.GridCoords);
            traits.ApplyProfileTraits(vox, HumanoidCharacterProfile.DefaultWithSpecies("Vox")
                .WithTraitPreferences([trait]), null, "Passenger");
            Assert.That(entities.HasComponent<CorpseDecayModifierComponent>(vox), Is.False);
        });
        await pair.RunSeconds(5.1f);
        await server.WaitAssertion(() =>
        {
            var perish = entities.GetComponent<PerishableComponent>(body);
            Assert.That(rot.IsRotProgressing(body, perish), Is.True,
                $"Dead: {server.System<MobStateSystem>().IsDead(body)}, temperature: {entities.GetComponent<TemperatureComponent>(body).CurrentTemperature}");
            Assert.That(perish.RotAccumulator, Is.EqualTo(perish.PerishUpdateRate * multiplier),
                $"Current time: {server.Timing.CurTime}, next update: {perish.RotNextUpdate}");
            // Advance to the second stage without simulating the entire decay interval.
            rot.ReduceAccumulator(body, -perish.RotAfter);
        });
        await pair.RunSeconds(10.2f);
        await server.WaitAssertion(() =>
        {
            var rotten = entities.GetComponent<RottingComponent>(body);
            Assert.That(rotten.TotalRotTime, Is.EqualTo(rotten.RotUpdateRate * multiplier));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MedicalTechFabPrintsDeployableIvStand()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        await server.WaitAssertion(() =>
        {
            var fab = entities.SpawnAttachedTo("MedicalTechFab", map.GridCoords);
            entities.RemoveComponent<ApcPowerReceiverComponent>(fab);
            var lathe = server.System<LatheSystem>();
            Assert.That(lathe.TryGetAvailableRecipes(fab, out var recipes), Is.True);
            Assert.That(recipes!.Any(id => id.Id == "IVDripFlatpack"), Is.True);
            var recipe = server.ProtoMan.Index<LatheRecipePrototype>("IVDripFlatpack");
            foreach (var (material, amount) in recipe.Materials)
                Assert.That(server.System<SharedMaterialStorageSystem>().TryChangeMaterialAmount(fab, material, amount), Is.True);
            Assert.That(lathe.TryAddToQueue(fab, recipe, 1), Is.True);
            Assert.That(lathe.TryStartProducing(fab), Is.True);
        });
        await pair.RunTicksSync(360);
        await server.WaitAssertion(() =>
        {
            var lookup = server.System<EntityLookupSystem>();
            var pack = lookup.GetEntitiesInRange<FlatpackComponent>(map.GridCoords, 3f).Single(p => p.Comp.Entity?.Id == "IVDrip");
            var user = entities.SpawnAttachedTo("MobHuman", map.GridCoords);
            var tool = entities.SpawnAttachedTo("Multitool", map.GridCoords);
            var use = new InteractUsingEvent(user, tool, pack.Owner, map.GridCoords);
            entities.EventBus.RaiseLocalEvent(pack.Owner, use);
            Assert.That(use.Handled, Is.True);
            Assert.That(lookup.GetEntitiesInRange<IVDripComponent>(map.GridCoords, 3f), Has.Count.EqualTo(1));
        });
        await pair.CleanReturnAsync();
    }
}
