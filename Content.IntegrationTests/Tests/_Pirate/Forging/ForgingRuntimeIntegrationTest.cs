// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Server.Cargo.Components;
using Content.Server.Construction.Components;
using Content.Server.Destructible;
using Content.Server.Temperature.Systems;
using Content.Shared._Pirate.Durability;
using Content.Shared._Pirate.Forging;
using Content.Shared._Pirate.Knowledge.Quality;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Shared.Projectiles;
using Content.Shared.Temperature.Components;
using Content.Shared.Tools.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Wieldable.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Forging;

[TestFixture]
public sealed class ForgingRuntimeIntegrationTest
{
    [Test]
    public async Task MetalTemperatureTransitionsUseHysteresis()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var temperature = server.System<TemperatureSystem>();
        var metal = server.System<SharedMetalSystem>();

        await server.WaitAssertion(() =>
        {
            var ingot = entMan.SpawnEntity("SteelIngot", MapCoordinates.Nullspace);
            var component = entMan.GetComponent<MetallicComponent>(ingot);

            temperature.ForceChangeTemperature(ingot, 290f);
            Assert.That(metal.IsWorkable(ingot), Is.False, "Cold steel stayed workable.");

            temperature.ForceChangeTemperature(ingot, 1250f);
            Assert.That(metal.IsWorkable(ingot), Is.False,
                "Steel became workable before reaching its ideal temperature.");

            temperature.ForceChangeTemperature(ingot, component.IdealTemp);
            Assert.That(metal.IsWorkable(ingot), Is.True,
                "Steel did not become workable at its ideal temperature.");

            temperature.ForceChangeTemperature(ingot, component.IdealTemp + 100f);
            Assert.That(metal.IsWorkable(ingot), Is.True,
                "Steel stopped being workable after a forge step crossed its ideal temperature.");

            temperature.ForceChangeTemperature(ingot, component.MinTemp);
            Assert.That(metal.IsWorkable(ingot), Is.True,
                "Steel cooled at the inclusive lower boundary.");

            temperature.ForceChangeTemperature(ingot, component.MinTemp - 1f);
            Assert.That(metal.IsWorkable(ingot), Is.False,
                "Steel stayed workable below its lower temperature boundary.");

            entMan.DeleteEntity(ingot);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FinishedForgedItemAppliesQualityToItsCalculatedPrice()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var forging = server.System<ForgingSystem>();
        var metalSystem = server.System<SharedMetalSystem>();

        await server.WaitAssertion(() =>
        {
            var item = server.ProtoMan.Index<ForgedItemPrototype>("LongswordBlade");
            var metal = server.ProtoMan.Index<MetalPrototype>("Steel");
            var factors = server.ProtoMan.Index<QualityPrototype>("BaseQuality");
            var part = entMan.SpawnEntity("ForgedPart", MapCoordinates.Nullspace);
            var forged = entMan.EnsureComponent<ForgedItemComponent>(part);
            forged.Item = item.ID;
            forged.Completed = true;
            metalSystem.SetMetal(part, metal.ID);

            var partQuality = entMan.EnsureComponent<QualityComponent>(part);
            partQuality.Quality = 2;
            partQuality.QualityFactors = factors.ID;
            partQuality.Applied = true;

            var result = forging.FinishForgedItem(part, null);
            Assert.That(result, Is.Not.Null);
            var resultQuality = entMan.GetComponent<QualityComponent>(result!.Value);
            var price = entMan.GetComponent<StaticPriceComponent>(result.Value);
            var basePrice = metal.Price *
                            (item.Work * metal.WorkScale / Math.Max(1, item.Amount)).Double() *
                            item.Cost;
            var expected = basePrice * QualitySystem.QualityModifier(partQuality.Quality, factors.Price);

            Assert.Multiple(() =>
            {
                Assert.That(resultQuality.Applied, Is.True);
                Assert.That(resultQuality.Quality, Is.EqualTo(partQuality.Quality));
                Assert.That(price.Price, Is.EqualTo(expected).Within(0.001d));
            });

            entMan.DeleteEntity(result.Value);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EveryAllowedItemMetalCombinationCompletesThroughWorkablePipeline()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var prototypes = server.ProtoMan;
        var forging = server.System<ForgingSystem>();
        var metalSystem = server.System<SharedMetalSystem>();
        var temperature = server.System<TemperatureSystem>();
        var damageable = server.System<DamageableSystem>();
        var mapSystem = server.System<SharedMapSystem>();
        var blunt = prototypes.Index<DamageTypePrototype>("Blunt");

        await server.WaitAssertion(() =>
        {
            var mapUid = mapSystem.CreateMap(out _);
            try
            {
                var coordinates = new EntityCoordinates(mapUid, 0f, 0f);
                var user = entMan.SpawnEntity("Crowbar", coordinates);
                var concreteItems = prototypes.EnumeratePrototypes<ForgedItemPrototype>()
                    .Where(item => !item.Abstract)
                    .OrderBy(item => item.ID, StringComparer.Ordinal)
                    .ToArray();
                var metals = prototypes.EnumeratePrototypes<MetalPrototype>()
                    .OrderBy(metal => metal.ID, StringComparer.Ordinal)
                    .ToArray();
                var combinations = 0;

                foreach (var item in concreteItems)
                {
                    foreach (var metal in metals)
                    {
                        if (!forging.CanMakeFrom(item, metal.ID))
                            continue;

                    combinations++;
                    var context = $"{item.ID} made from {metal.ID}";
                    var expectedPrototype = item.Result ?? ForgingSystem.DefaultResult;
                    var baseline = entMan.SpawnEntity(
                        item.Result ?? item.Finished!.Value,
                        coordinates);
                    var unfinished = forging.SpawnUnfinished(
                        coordinates,
                        metal.ID,
                        item.ID,
                        1);

                    Assert.That(entMan.TryGetComponent<WorkableComponent>(unfinished, out var workable), Is.True,
                        $"{context}: missing Workable on unfinished item");
                    var expectedWork = item.Work * metal.WorkScale;
                    Assert.That(workable!.Remaining, Is.EqualTo(expectedWork),
                        $"{context}: wrong work amount");
                    Assert.That(workable.Amount, Is.EqualTo(item.Amount),
                        $"{context}: wrong output amount");
                    Assert.That(workable.Result, Is.EqualTo(expectedPrototype),
                        $"{context}: wrong intermediate result");

                    var unfinishedTemperature = entMan.GetComponent<TemperatureComponent>(unfinished);
                    Assert.That(unfinishedTemperature.CurrentTemperature, Is.EqualTo(metal.WorkingTemp).Within(0.001f),
                        $"{context}: unfinished item did not start at working temperature");
                    Assert.That(metalSystem.IsWorkable(unfinished), Is.True,
                        $"{context}: freshly started item is not workable");

                    var unfinishedBurn = entMan.GetComponent<BurnableForgedComponent>(unfinished);
                    Assert.That(unfinishedBurn.BurnTemp, Is.EqualTo(metal.MaxTemp).Within(0.001f),
                        $"{context}: unfinished overheat threshold is wrong");
                    var destructible = entMan.GetComponent<DestructibleComponent>(unfinished);
                    var damageThreshold = destructible.Thresholds
                        .Select(threshold => threshold.Trigger)
                        .OfType<DamageTrigger>()
                        .Single();
                    Assert.That(damageThreshold.Damage, Is.EqualTo(expectedWork * 2),
                        $"{context}: unfinished break threshold was not scaled with work");

                    var applied = damageable.TryChangeDamage(
                        unfinished,
                        new DamageSpecifier(blunt, expectedWork),
                        ignoreResistances: true,
                        origin: user,
                        canMiss: false);
                    Assert.That(applied, Is.Not.Null, $"{context}: hammer damage was not applied");

                    var results = FindForgedResults(entMan, metalSystem, item.ID, metal.ID);
                    Assert.That(results, Has.Count.EqualTo(item.Amount),
                        $"{context}: Workable produced the wrong number of entities");

                    var finalResults = new List<EntityUid>(results.Count);
                    foreach (var result in results)
                    {
                        Assert.That(EntityPrototypeId(entMan, result), Is.EqualTo(expectedPrototype.Id),
                            $"{context}: Workable spawned the wrong entity");
                        AssertForgedState(entMan, result, metal, item, context);

                        if (item.Result is not null)
                        {
                            finalResults.Add(result);
                            continue;
                        }

                        Assert.That(entMan.TryGetComponent<ConstructionComponent>(result, out var construction), Is.True,
                            $"{context}: procedural part has no construction state");
                        Assert.Multiple(() =>
                        {
                            Assert.That(construction!.Graph, Is.EqualTo(item.Construction!.Value.Id));
                            Assert.That(construction.Node, Is.EqualTo("start"));
                            Assert.That(construction.TargetNode, Is.EqualTo("finished"));
                            Assert.That(construction.EdgeIndex, Is.EqualTo(0));
                            Assert.That(construction.StepIndex, Is.EqualTo(0));
                        });

                        temperature.ForceChangeTemperature(result, 290f);
                        Assert.That(new QuenchMetal().Condition(result, entMan), Is.True,
                            $"{context}: cooled part did not satisfy QuenchMetal");

                        var finished = forging.FinishForgedItem(result, user);
                        Assert.That(finished, Is.Not.Null, $"{context}: finishing returned no entity");
                        Assert.That(EntityPrototypeId(entMan, finished!.Value), Is.EqualTo(item.Finished!.Value.Id),
                            $"{context}: finishing spawned the wrong entity");
                        finalResults.Add(finished.Value);
                    }

                    foreach (var result in finalResults)
                    {
                        AssertForgedState(entMan, result, metal, item, context);
                        AssertMetalStatModifiers(entMan, result, baseline, metal, context);

                        Assert.That(entMan.TryGetComponent<StaticPriceComponent>(result, out var price), Is.True,
                            $"{context}: finished item has no price");
                        var expectedPrice = metal.Price *
                                            (item.Work * metal.WorkScale / Math.Max(1, item.Amount)).Double() *
                                            item.Cost;
                        Assert.That(price!.Price, Is.EqualTo(expectedPrice).Within(0.001d),
                            $"{context}: finished item price is wrong");
                    }

                    foreach (var result in finalResults)
                        entMan.DeleteEntity(result);
                    foreach (var result in results)
                    {
                        if (!entMan.Deleted(result))
                            entMan.DeleteEntity(result);
                    }

                        entMan.DeleteEntity(baseline);
                    }
                }

                Assert.That(combinations, Is.EqualTo(111),
                    "The allowed item/metal matrix changed without updating its exhaustive runtime test.");
            }
            finally
            {
                entMan.DeleteEntity(mapUid);
            }
        });

        await server.WaitRunTicks(1);
        await pair.CleanReturnAsync();
    }

    private static List<EntityUid> FindForgedResults(
        IEntityManager entMan,
        SharedMetalSystem metalSystem,
        string itemId,
        string metalId)
    {
        var results = new List<EntityUid>();
        var query = entMan.AllEntityQueryEnumerator<ForgedItemComponent>();
        while (query.MoveNext(out var uid, out var forged))
        {
            if (entMan.Deleted(uid) || !forged.Completed || forged.Item.Id != itemId ||
                !metalSystem.TryGetMetal(uid, out var foundMetal) || foundMetal.Id != metalId)
            {
                continue;
            }

            results.Add(uid);
        }

        return results;
    }

    private static void AssertForgedState(
        IEntityManager entMan,
        EntityUid uid,
        MetalPrototype metal,
        ForgedItemPrototype item,
        string context)
    {
        Assert.That(entMan.TryGetComponent<MetallicComponent>(uid, out var metallic), Is.True,
            $"{context}: output is not metallic");
        Assert.That(metallic!.Metal?.Id, Is.EqualTo(metal.ID),
            $"{context}: output lost its metal");

        Assert.That(entMan.TryGetComponent<DurabilityComponent>(uid, out var durability), Is.True,
            $"{context}: output has no durability");
        Assert.That(durability!.DurabilityScale, Is.EqualTo(metal.Durability),
            $"{context}: metal durability scale was not applied");

        Assert.That(entMan.TryGetComponent<BurnableForgedComponent>(uid, out var burnable), Is.True,
            $"{context}: output cannot overheat or melt");
        Assert.That(burnable!.BurnTemp, Is.EqualTo(metal.MeltTemp).Within(0.001f),
            $"{context}: completed melt threshold is wrong");
        Assert.That(burnable.BurnedPrototype.Id, Is.EqualTo(metal.Overheated.Id),
            $"{context}: wrong overheated result");

        if (entMan.TryGetComponent<ForgedItemComponent>(uid, out var forged))
        {
            Assert.That(forged!.Item.Id, Is.EqualTo(item.ID), $"{context}: wrong ForgedItem id");
            Assert.That(forged.Completed, Is.True, $"{context}: result is not marked complete");
        }
    }

    private static void AssertMetalStatModifiers(
        IEntityManager entMan,
        EntityUid result,
        EntityUid baseline,
        MetalPrototype metal,
        string context)
    {
        if (entMan.TryGetComponent<MeleeWeaponComponent>(baseline, out var baseMelee))
        {
            Assert.That(entMan.TryGetComponent<MeleeWeaponComponent>(result, out var actual), Is.True);
            Assert.That(actual!.AttackRate, Is.EqualTo(baseMelee!.AttackRate * metal.Speed).Within(0.0001f),
                $"{context}: melee speed modifier is wrong");
            AssertModifiedDamage(actual.Damage.DamageDict, baseMelee.Damage.DamageDict, metal, context, "melee");
        }

        if (entMan.TryGetComponent<DamageOtherOnHitComponent>(baseline, out var baseThrown))
        {
            Assert.That(entMan.TryGetComponent<DamageOtherOnHitComponent>(result, out var actual), Is.True);
            AssertModifiedDamage(actual!.Damage.DamageDict, baseThrown!.Damage.DamageDict, metal, context, "thrown");
        }

        if (entMan.TryGetComponent<IncreaseDamageOnWieldComponent>(baseline, out var baseWield))
        {
            Assert.That(entMan.TryGetComponent<IncreaseDamageOnWieldComponent>(result, out var actual), Is.True);
            AssertModifiedDamage(actual!.BonusDamage.DamageDict, baseWield!.BonusDamage.DamageDict, metal, context, "wield");
        }

        if (entMan.TryGetComponent<ProjectileComponent>(baseline, out var baseProjectile))
        {
            Assert.That(entMan.TryGetComponent<ProjectileComponent>(result, out var actual), Is.True);
            AssertModifiedDamage(actual!.Damage.DamageDict, baseProjectile!.Damage.DamageDict, metal, context, "projectile");
        }

        if (entMan.TryGetComponent<ToolComponent>(baseline, out var baseTool))
        {
            Assert.That(entMan.TryGetComponent<ToolComponent>(result, out var actual), Is.True);
            Assert.That(actual!.SpeedModifier, Is.EqualTo(baseTool!.SpeedModifier * metal.Speed).Within(0.0001f),
                $"{context}: tool speed modifier is wrong");
        }
    }

    private static void AssertModifiedDamage(
        IReadOnlyDictionary<string, FixedPoint2> actual,
        IReadOnlyDictionary<string, FixedPoint2> baseline,
        MetalPrototype metal,
        string context,
        string component)
    {
        var expected = new Dictionary<string, FixedPoint2>(baseline);
        ForgingSystem.ModifyDamage(expected, metal);
        Assert.That(actual, Is.EquivalentTo(expected),
            $"{context}: {component} damage modifiers are wrong");
    }

    private static string EntityPrototypeId(IEntityManager entMan, EntityUid uid)
        => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID;
}
