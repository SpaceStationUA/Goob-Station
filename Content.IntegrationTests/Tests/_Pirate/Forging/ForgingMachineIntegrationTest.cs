// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Server._Pirate.Forging;
using Content.Server.Temperature.Systems;
using Content.Shared._Pirate.Forging;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Fluids.Components;
using Content.Shared.Temperature.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Pirate.Forging;

[TestFixture]
public sealed class ForgingMachineIntegrationTest
{
    [TestPrototypes]
    private const string TestPrototypes = @"
- type: entity
  parent: BaseItem
  id: PirateImmediateBloomeryTestResult

- type: entity
  parent: BaseItem
  id: PirateImmediateBloomeryTestSource
  components:
  - type: BloomerySmelter
    result: PirateImmediateBloomeryTestResult
    duration: 0

- type: entity
  parent: BaseItem
  id: PirateImmediateReplacementTestResult

- type: entity
  parent: BaseItem
  id: PirateImmediateReplacementTestSource
  components:
  - type: ScheduledEntityReplacement
    result: PirateImmediateReplacementTestResult
    duration: 0
";

    private static readonly (string Metal, string Ingot, string Overheated)[] MetalEntities =
    {
        ("Brass", "BrassIngot", "OverheatedBrass"),
        ("Steel", "SteelIngot", "OverheatedSteel"),
        ("Plasteel", "PlasteelIngot", "OverheatedPlasteel"),
        ("Gold", "GoldIngot", "GoldMolten"),
        ("Silver", "SilverIngot", "SilverMolten"),
        ("Adamantine", "AdamantineIngot", "OverheatedAdamantine"),
    };

    [Test]
    public async Task EveryMetalHasAWorkingAcquisitionAndOverheatPath()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var prototypes = server.ProtoMan;
        var bloomery = server.System<BloomerySmelterSystem>();
        var burnable = server.System<BurnableForgedSystem>();
        var damageable = server.System<DamageableSystem>();
        var metalSystem = server.System<SharedMetalSystem>();
        var temperature = server.System<TemperatureSystem>();
        var blunt = prototypes.Index<DamageTypePrototype>("Blunt");
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var user = entMan.SpawnEntity("Crowbar", map.GridCoords);
            var acquiredIngots = new List<EntityUid>();
            var bloomeryPaths = new[]
            {
                (Lit: "BloomeryLitBrass", Complete: "BloomeryCompleteBrass", Bloom: "BrassBloom", Ingot: "BrassIngot", Metal: "Brass"),
                (Lit: "BloomeryLitSteel", Complete: "BloomeryCompleteSteel", Bloom: "SteelBloom", Ingot: "SteelIngot", Metal: "Steel"),
                (Lit: "BloomeryLitPlasteel", Complete: "BloomeryCompletePlasteel", Bloom: "PlasteelBloom", Ingot: "PlasteelIngot", Metal: "Plasteel"),
            };

            foreach (var path in bloomeryPaths)
            {
                var lit = entMan.SpawnEntity(path.Lit, map.GridCoords);
                var litCoordinates = entMan.GetComponent<TransformComponent>(lit).Coordinates;

                Assert.That(bloomery.Complete(lit), Is.True, $"{path.Metal}: lit bloomery did not complete");
                var complete = FindSingle(entMan, path.Complete, $"{path.Metal}: completed bloomery");
                Assert.That(entMan.GetComponent<TransformComponent>(complete).Coordinates,
                    Is.EqualTo(litCoordinates), $"{path.Metal}: bloomery coordinates were not preserved");

                damageable.TryChangeDamage(complete,
                    new DamageSpecifier(blunt, 6),
                    ignoreResistances: true,
                    origin: user,
                    canMiss: false);
                var bloom = FindSingle(entMan, path.Bloom, $"{path.Metal}: bloom");
                var work = entMan.GetComponent<WorkableComponent>(bloom).Remaining;
                damageable.TryChangeDamage(bloom,
                    new DamageSpecifier(blunt, work),
                    ignoreResistances: true,
                    origin: user,
                    canMiss: false);

                var ingot = FindSingle(entMan, path.Ingot, $"{path.Metal}: ingot");
                Assert.That(metalSystem.TryGetMetal(ingot, out var found), Is.True);
                Assert.That(found.Id, Is.EqualTo(path.Metal));
                acquiredIngots.Add(ingot);
            }

            var directPaths = new[]
            {
                (Scraps: "GoldScraps", Ingot: "GoldIngot", Metal: "Gold"),
                (Scraps: "SilverScraps", Ingot: "SilverIngot", Metal: "Silver"),
                (Scraps: "AdamantineScraps", Ingot: "AdamantineIngot", Metal: "Adamantine"),
            };

            foreach (var path in directPaths)
            {
                var scraps = entMan.SpawnEntity(path.Scraps, map.GridCoords);
                var metallic = entMan.GetComponent<MetallicComponent>(scraps);
                temperature.ForceChangeTemperature(scraps, metallic.IdealTemp);
                Assert.That(metalSystem.IsWorkable(scraps), Is.True, $"{path.Metal}: heated scraps are not workable");

                var work = entMan.GetComponent<WorkableComponent>(scraps).Remaining;
                damageable.TryChangeDamage(scraps,
                    new DamageSpecifier(blunt, work),
                    ignoreResistances: true,
                    origin: user,
                    canMiss: false);

                var ingot = FindSingle(entMan, path.Ingot, $"{path.Metal}: direct ingot");
                Assert.That(metalSystem.TryGetMetal(ingot, out var found), Is.True);
                Assert.That(found.Id, Is.EqualTo(path.Metal));
                acquiredIngots.Add(ingot);
            }

            Assert.That(acquiredIngots, Has.Count.EqualTo(6));
            foreach (var uid in acquiredIngots)
                entMan.DeleteEntity(uid);

            foreach (var (metalId, ingotId, overheatedId) in MetalEntities)
            {
                var metal = prototypes.Index<MetalPrototype>(metalId);
                Assert.That(metal.Overheated.Id, Is.EqualTo(overheatedId));

                var ingot = entMan.SpawnEntity(ingotId, MapCoordinates.Nullspace);
                var burn = entMan.GetComponent<BurnableForgedComponent>(ingot);
                temperature.ForceChangeTemperature(ingot, burn.BurnTemp - 1f);
                Assert.That(burnable.CompleteBurn(ingot), Is.False,
                    $"{metalId}: ingot burned below its threshold");

                temperature.ForceChangeTemperature(ingot, burn.BurnTemp);
                Assert.That(burnable.CompleteBurn(ingot), Is.True,
                    $"{metalId}: ingot did not burn at its inclusive threshold");
                var overheated = FindSingle(entMan, overheatedId, $"{metalId}: overheated result");
                entMan.DeleteEntity(overheated);
            }

            Assert.That(bloomery.Complete(user), Is.False,
                "Bloomery completion accepted an entity without BloomerySmelter.");
        });

        await server.WaitRunTicks(1);
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ForgeQuenchReplacementAndMoltenPuddlesAreEventDriven()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var slots = server.System<ItemSlotsSystem>();
        var temperature = server.System<TemperatureSystem>();
        var solutions = server.System<SharedSolutionContainerSystem>();
        var replacement = server.System<ScheduledEntityReplacementSystem>();
        var map = await pair.CreateTestMap();

        EntityUid forge = default;
        EntityUid forgeIngot = default;
        EntityUid quench = default;
        EntityUid quenchIngot = default;
        EntityUid immediateBloomery = default;
        EntityUid immediateReplacement = default;

        await server.WaitAssertion(() =>
        {
            forge = entMan.SpawnEntity("CharcoalForgeLit", MapCoordinates.Nullspace);
            forgeIngot = entMan.SpawnEntity("SteelIngot", MapCoordinates.Nullspace);
            temperature.ForceChangeTemperature(forgeIngot, 290f);
            Assert.That(slots.TryInsert(forge, "ItemCabinet", forgeIngot, null), Is.True);

            var nonMetal = entMan.SpawnEntity("Crowbar", map.GridCoords);
            Assert.That(slots.TryInsert(forge, "ItemCabinet", nonMetal, null), Is.False,
                "The forge accepted a non-metal item or ignored its occupied slot.");
            entMan.DeleteEntity(nonMetal);

            quench = entMan.SpawnEntity("QuenchingBarrel", MapCoordinates.Nullspace);
            quenchIngot = entMan.SpawnEntity("SteelIngot", MapCoordinates.Nullspace);
            temperature.ForceChangeTemperature(quenchIngot, 1300f);
            Assert.That(slots.TryInsert(quench, "ItemCabinet", quenchIngot, null), Is.True);

            immediateBloomery = entMan.SpawnEntity("PirateImmediateBloomeryTestSource", map.GridCoords);
            immediateReplacement = entMan.SpawnEntity("PirateImmediateReplacementTestSource", map.GridCoords);
            entMan.SpawnEntity("GoldMolten", map.GridCoords);
            entMan.SpawnEntity("SilverMolten", map.GridCoords);

            Assert.That(replacement.Complete(forge), Is.False,
                "Scheduled replacement accepted an entity without its component.");
        });

        await server.WaitRunTicks(35);

        await server.WaitAssertion(() =>
        {
            var heated = entMan.GetComponent<TemperatureComponent>(forgeIngot).CurrentTemperature;
            var cooled = entMan.GetComponent<TemperatureComponent>(quenchIngot).CurrentTemperature;
            Assert.That(heated, Is.GreaterThan(290f), "The real charcoal forge did not heat its inserted ingot.");
            Assert.That(cooled, Is.LessThan(1300f), "The real quenching barrel did not cool its inserted ingot.");

            Assert.That(entMan.Deleted(immediateBloomery), Is.True,
                "Bloomery MapInit did not schedule its zero-duration completion.");
            Assert.That(FindEntities(entMan, "PirateImmediateBloomeryTestResult"), Has.Count.EqualTo(1));
            Assert.That(entMan.Deleted(immediateReplacement), Is.True,
                "Scheduled replacement MapInit did not schedule its zero-duration completion.");
            Assert.That(FindEntities(entMan, "PirateImmediateReplacementTestResult"), Has.Count.EqualTo(1));

            var gold = 0.0;
            var silver = 0.0;
            var query = entMan.AllEntityQueryEnumerator<PuddleComponent>();
            while (query.MoveNext(out var uid, out var puddle))
            {
                if (!solutions.TryGetSolution(uid, puddle.SolutionName, out _, out var solution))
                    continue;

                gold += solution.GetReagentQuantity(new ReagentId("Gold", null)).Double();
                silver += solution.GetReagentQuantity(new ReagentId("Silver", null)).Double();
            }

            Assert.That(gold, Is.EqualTo(30d).Within(0.001d), "Molten gold did not create its configured puddle.");
            Assert.That(silver, Is.EqualTo(30d).Within(0.001d), "Molten silver did not create its configured puddle.");

            Assert.That(slots.TryEject(forge, "ItemCabinet", null, out var ejected, doAfter: false), Is.True);
            Assert.That(ejected, Is.EqualTo(forgeIngot));
        });

        var temperatureAfterEject = 0f;
        await server.WaitAssertion(() =>
        {
            temperatureAfterEject = entMan.GetComponent<TemperatureComponent>(forgeIngot).CurrentTemperature;
        });
        await server.WaitRunTicks(35);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<TemperatureComponent>(forgeIngot).CurrentTemperature,
                Is.EqualTo(temperatureAfterEject).Within(0.001f),
                "The forge kept scheduling heat after its item was removed.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AnvilFiltersRangeTemperatureMetalAndConsumesExactCost()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var prototypes = server.ProtoMan;
        var anvilSystem = server.System<AnvilSystem>();
        var temperature = server.System<TemperatureSystem>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var actor = entMan.SpawnEntity("Crowbar", map.GridCoords);
            var anvil = entMan.SpawnEntity("AnvilComplete", map.GridCoords);
            var anvilComp = entMan.GetComponent<ForgingAnvilComponent>(anvil);
            var close = new EntityCoordinates(map.Grid.Owner, new Vector2(0.2f, 0f));
            var far = new EntityCoordinates(map.Grid.Owner, new Vector2(2f, 0f));

            var hot1 = entMan.SpawnEntity("SteelIngot", close);
            var hot2 = entMan.SpawnEntity("SteelIngot", close);
            var cold = entMan.SpawnEntity("SteelIngot", close);
            temperature.ForceChangeTemperature(cold, 290f);
            var wrongMetal = entMan.SpawnEntity("BrassIngot", close);
            var outsideRange = entMan.SpawnEntity("SteelIngot", far);

            Assert.That(anvilSystem.TryStartItem((anvil, anvilComp), actor, "MissingMetal", "LongswordBlade"), Is.Null);
            Assert.That(anvilSystem.TryStartItem((anvil, anvilComp), actor, "Steel", "MissingItem"), Is.Null);
            Assert.That(anvilSystem.TryStartItem((anvil, anvilComp), actor, "Gold", "CartridgeGauss"), Is.Null,
                "The anvil ignored a forged item's metal whitelist.");
            Assert.That(anvilSystem.TryStartItem((anvil, anvilComp), actor, "Steel", "LongswordBlade"), Is.Null,
                "The anvil counted cold, wrong-metal, or out-of-range ingots.");
            Assert.That(entMan.Deleted(hot1), Is.False);
            Assert.That(entMan.Deleted(hot2), Is.False);

            var hot3 = entMan.SpawnEntity("SteelIngot", close);
            var hot4 = entMan.SpawnEntity("SteelIngot", close);
            var hot = new[] { hot1, hot2, hot3, hot4 };
            var result = anvilSystem.TryStartItem((anvil, anvilComp), actor, "Steel", "LongswordBlade");
            Assert.That(result, Is.Not.Null, "The anvil rejected three valid hot steel ingots.");
            Assert.That(hot.Count(entMan.Deleted), Is.EqualTo(3), "The anvil did not consume exactly the recipe cost.");
            Assert.That(entMan.Deleted(cold), Is.False, "The anvil consumed a cold ingot.");
            Assert.That(entMan.Deleted(wrongMetal), Is.False, "The anvil consumed the wrong metal.");
            Assert.That(entMan.Deleted(outsideRange), Is.False, "The anvil consumed an out-of-range ingot.");

            var workable = entMan.GetComponent<WorkableComponent>(result!.Value);
            var expectedWork = prototypes.Index<ForgedItemPrototype>("LongswordBlade").Work *
                               prototypes.Index<MetalPrototype>("Steel").WorkScale *
                               anvilComp.WorkScale;
            Assert.That(workable.Remaining, Is.EqualTo(expectedWork),
                "The anvil produced the wrong work amount.");
            Assert.That(entMan.GetComponent<TransformComponent>(result.Value).Coordinates.Position,
                Is.EqualTo(entMan.GetComponent<TransformComponent>(anvil).Coordinates.Position));

            var freeIngot = entMan.SpawnEntity("SteelIngot", close);
            anvilComp.CostScale = 0;
            var freeResult = anvilSystem.TryStartItem((anvil, anvilComp), actor, "Steel", "Crowbar");
            Assert.That(freeResult, Is.Not.Null, "A deliberately zero-cost anvil recipe did not start.");
            Assert.That(entMan.Deleted(freeIngot), Is.False,
                "A zero-cost recipe deleted nearby ingots.");
        });

        await server.WaitRunTicks(1);
        await pair.CleanReturnAsync();
    }

    [Test]
    public void ForgingSystemsHaveNoPerFrameUpdateLoop()
    {
        var types = new[]
        {
            typeof(AnvilSystem),
            typeof(ForgingSystem),
            typeof(WorkableSystem),
            typeof(DamageOnHoldingImmuneSystem),
            typeof(Content.Server._Pirate.Forging.MetalSystem),
            typeof(BloomerySmelterSystem),
            typeof(BurnableForgedSystem),
            typeof(ItemSlotHeaterSystem),
            typeof(ScheduledEntityReplacementSystem),
        };

        foreach (var type in types)
        {
            var updates = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method => method.DeclaringType == type && method.Name == "Update")
                .ToArray();
            Assert.That(updates, Is.Empty, $"{type.Name} must remain event/timer driven.");
        }
    }

    private static EntityUid FindSingle(IEntityManager entMan, string prototype, string context)
    {
        var entities = FindEntities(entMan, prototype);
        Assert.That(entities, Has.Count.EqualTo(1), $"{context}: expected exactly one {prototype}");
        return entities[0];
    }

    private static List<EntityUid> FindEntities(IEntityManager entMan, string prototype)
    {
        var entities = new List<EntityUid>();
        var query = entMan.AllEntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out var uid, out var metadata))
        {
            if (!entMan.Deleted(uid) && metadata.EntityPrototype?.ID == prototype)
                entities.Add(uid);
        }

        return entities;
    }
}
