// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Shared._Pirate.Durability;
using Content.Shared._Pirate.Forging;
using Content.Shared._Pirate.Knowledge;
using Content.Shared._Pirate.Knowledge.Quality;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Forging;

[TestFixture]
public sealed class ForgingPrototypeIntegrityTest
{
    private static readonly string[] ExpectedMetals =
    [
        "Adamantine",
        "Brass",
        "Gold",
        "Plasteel",
        "Silver",
        "Steel",
    ];

    private static readonly string[] ExpectedCategories =
    [
        "Armor",
        "Blades",
        "Bludgeons",
        "Polearms",
        "Projectiles",
        "Tools",
    ];

    private static readonly string[] ExpectedConcreteItems =
    [
        "ArrowHeads",
        "CleaverBlade",
        "CoilgunBearings",
        "CoilgunDisk",
        "CoilgunShard",
        "CombatKnifeBlade",
        "Crowbar",
        "HalberdHead",
        "HammerHead",
        "KnifeBlade",
        "KukriBlade",
        "LongswordBlade",
        "MorningstarHead",
        "PickaxeHead",
        "ScrewdriverShank",
        "SpearHead",
        "SurvivalKnife",
        "SwordBlade",
        "ThinBlade",
        "WarhammerHead",
        "Wrench",
    ];

    [Test]
    public async Task EveryForgingPrototypeReferenceAndMenuEntryIsValid()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypes = server.ProtoMan;
        var factory = server.ResolveDependency<IComponentFactory>();
        var forging = server.System<ForgingSystem>();
        var metalSystem = server.System<SharedMetalSystem>();

        await server.WaitAssertion(() =>
        {
            var failures = new List<string>();
            var catalog = prototypes.Index<KnowledgeCatalogPrototype>("PirateSkills");
            var categories = prototypes.EnumeratePrototypes<ForgingCategoryPrototype>().ToArray();
            var metals = prototypes.EnumeratePrototypes<MetalPrototype>().ToArray();
            var allItems = prototypes.EnumeratePrototypes<ForgedItemPrototype>().ToArray();
            var concreteItems = allItems.Where(item => !item.Abstract).ToArray();

            Assert.That(categories.Select(category => category.ID), Is.EquivalentTo(ExpectedCategories),
                "The forging category catalog changed without updating its exhaustive test.");
            Assert.That(metals.Select(metal => metal.ID), Is.EquivalentTo(ExpectedMetals),
                "The metal catalog changed without updating its exhaustive test.");
            Assert.That(concreteItems.Select(item => item.ID), Is.EquivalentTo(ExpectedConcreteItems),
                "The usable forged item catalog changed without updating its exhaustive test.");

            ValidateMetalCatalog(prototypes, factory, metalSystem, metals, failures);
            ValidateItemCatalog(prototypes, factory, forging, catalog, metals, allItems, concreteItems, failures);

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        });

        await pair.CleanReturnAsync();
    }

    private static void ValidateMetalCatalog(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        SharedMetalSystem metalSystem,
        IReadOnlyCollection<MetalPrototype> metals,
        ICollection<string> failures)
    {
        Assert.That(metalSystem.AllMetals.Select(metal => metal.ID),
            Is.EqualTo(metalSystem.AllMetals
                .OrderBy(metal => metal.Name, StringComparer.Ordinal)
                .Select(metal => metal.ID)),
            "The metal menu must remain deterministic.");

        foreach (var metal in metals)
        {
            if (string.IsNullOrWhiteSpace(metal.Name))
                failures.Add($"{metal.ID}: has no display name");
            if (string.IsNullOrWhiteSpace(metal.IngotSprite))
                failures.Add($"{metal.ID}: has no ingot sprite state");
            if (metal.Density <= 0f || metal.WorkingRange <= 0f || metal.WorkingTemp <= 0f || metal.MeltTemp <= 0f)
                failures.Add($"{metal.ID}: temperature or density values must be positive");
            if (metal.MinTemp >= metal.WorkingTemp || metal.WorkingTemp >= metal.MaxTemp || metal.MaxTemp >= metal.MeltTemp)
                failures.Add($"{metal.ID}: expected min < working < max < melt temperatures");
            if (metal.WorkScale <= 0 || metal.Durability <= 0 || metal.Speed <= 0f || metal.Price < 0d)
                failures.Add($"{metal.ID}: work, durability, speed, and price modifiers are invalid");
            if (!prototypes.TryIndex<EntityPrototype>(metal.Overheated, out var overheated) || overheated!.Abstract)
                failures.Add($"{metal.ID}: overheated result {metal.Overheated.Id} is missing or abstract");

            foreach (var damageType in metal.Damage.Keys.Concat(metal.DamageBonus.Keys))
            {
                if (!prototypes.HasIndex<DamageTypePrototype>(damageType))
                    failures.Add($"{metal.ID}: unknown damage type {damageType}");
            }

            var ingotId = new EntProtoId($"{metal.ID}Ingot");
            if (!prototypes.TryIndex<EntityPrototype>(ingotId, out var ingot))
            {
                failures.Add($"{metal.ID}: missing ingot entity {ingotId.Id}");
                continue;
            }

            if (!ingot!.TryGetComponent<MetallicComponent>(out var metallic, factory))
            {
                failures.Add($"{ingotId.Id}: missing Metallic component");
                continue;
            }

            if (metallic!.Metal != metal.ID)
                failures.Add($"{ingotId.Id}: resolves to metal {metallic.Metal} instead of {metal.ID}");
            if (Math.Abs(metallic.MinTemp - metal.MinTemp) > 0.001f ||
                Math.Abs(metallic.IdealTemp - metal.WorkingTemp) > 0.001f)
            {
                failures.Add(
                    $"{ingotId.Id}: workable range {metallic.MinTemp}-{metallic.IdealTemp} does not match {metal.MinTemp}-{metal.WorkingTemp}");
            }

            if (!ingot.TryGetComponent<MetalIngotComponent>(out _, factory))
                failures.Add($"{ingotId.Id}: missing MetalIngot component required by the anvil");
        }
    }

    private static void ValidateItemCatalog(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        ForgingSystem forging,
        KnowledgeCatalogPrototype catalog,
        IReadOnlyCollection<MetalPrototype> metals,
        IReadOnlyCollection<ForgedItemPrototype> allItems,
        IReadOnlyCollection<ForgedItemPrototype> concreteItems,
        ICollection<string> failures)
    {
        var cachedItems = forging.AllItems.Values.SelectMany(items => items).ToArray();
        Assert.That(cachedItems.Select(item => item.ID), Is.EquivalentTo(concreteItems.Select(item => item.ID)),
            "The anvil menu must contain every concrete item exactly once.");
        Assert.That(cachedItems.Any(item => item.Abstract), Is.False,
            "Abstract or TODO forged items must never appear in the anvil menu.");

        foreach (var (category, items) in forging.AllItems)
        {
            Assert.That(items.Select(item => item.DisplayName(prototypes)),
                Is.EqualTo(items.Select(item => item.DisplayName(prototypes)).OrderBy(name => name, StringComparer.Ordinal)),
                $"Forging category {category.ID} must have deterministic item ordering.");
        }

        var emptyCategories = forging.AllItems
            .Where(entry => entry.Value.Count == 0)
            .Select(entry => entry.Key.ID)
            .ToArray();
        Assert.That(emptyCategories, Is.EquivalentTo(new[] { "Armor" }),
            "Only the explicitly disabled TODO armor category may be empty.");

        AssertEntityHasMetallic(prototypes, factory, ForgingSystem.DefaultResult, "default procedural result", failures);
        AssertEntityHasMetallic(prototypes, factory, ForgingSystem.UnfinishedItem, "unfinished forging result", failures);

        foreach (var item in allItems)
        {
            if (!prototypes.HasIndex<ForgingCategoryPrototype>(item.Category))
                failures.Add($"{item.ID}: unknown category {item.Category.Id}");

            foreach (var (skillId, mastery) in item.Skills)
            {
                if (mastery < 0 || mastery > 5)
                    failures.Add($"{item.ID}: invalid mastery {mastery} for {skillId.Id}");
                if (!catalog.Entries.Contains(skillId))
                    failures.Add($"{item.ID}: skill {skillId.Id} is absent from PirateSkills");
                if (!prototypes.TryIndex<EntityPrototype>(skillId, out var skill) ||
                    !skill!.TryGetComponent<KnowledgeComponent>(out _, factory))
                {
                    failures.Add($"{item.ID}: skill {skillId.Id} is not a valid knowledge entity");
                }
            }

            if (item.Whitelist is { } whitelist)
            {
                foreach (var metal in whitelist)
                {
                    if (!prototypes.HasIndex<MetalPrototype>(metal))
                        failures.Add($"{item.ID}: unknown whitelisted metal {metal.Id}");
                    if (item.Blacklist?.Contains(metal) == true)
                        failures.Add($"{item.ID}: metal {metal.Id} is both whitelisted and blacklisted");
                }
            }

            if (item.Blacklist is { } blacklist)
            {
                foreach (var metal in blacklist)
                {
                    if (!prototypes.HasIndex<MetalPrototype>(metal))
                        failures.Add($"{item.ID}: unknown blacklisted metal {metal.Id}");
                }
            }

            if (item.QualityPrototype is { } quality && !prototypes.HasIndex<QualityPrototype>(quality))
                failures.Add($"{item.ID}: unknown quality prototype {quality.Id}");

            if (item.Abstract)
                continue;

            if (item.Work <= 0 || item.Amount <= 0 || item.Cost <= 0)
                failures.Add($"{item.ID}: work, amount, and cost must all be positive");
            if (!metals.Any(metal => forging.CanMakeFrom(item, metal.ID)))
                failures.Add($"{item.ID}: no configured metal can be used to forge it");

            if (item.Result is { } directResult)
            {
                AssertEntityHasMetallic(prototypes, factory, directResult, item.ID, failures);
                if (item.Construction is not null || item.Finished is not null)
                    failures.Add($"{item.ID}: direct result must not also configure procedural construction");
                continue;
            }

            if (item.Sprite is null || string.IsNullOrWhiteSpace(item.Name))
                failures.Add($"{item.ID}: procedural item requires both sprite and name");
            if (item.Construction is not { } graphId || item.Finished is not { } finished)
            {
                failures.Add($"{item.ID}: procedural item requires construction graph and finished entity");
                continue;
            }

            AssertEntityHasMetallic(prototypes, factory, finished, item.ID, failures);
            if (!prototypes.TryIndex<ConstructionGraphPrototype>(graphId, out var graph))
            {
                failures.Add($"{item.ID}: missing construction graph {graphId.Id}");
                continue;
            }

            if (!graph!.Nodes.ContainsKey("start") || !graph.Nodes.ContainsKey("finished") ||
                graph.Path("start", "finished") is not { Length: > 0 })
            {
                failures.Add($"{item.ID}: graph {graphId.Id} needs a path from start to finished");
                continue;
            }

            var edge = graph.Edge("start", "finished");
            if (edge is null)
            {
                failures.Add($"{item.ID}: graph {graphId.Id} needs a direct start-to-finished edge");
                continue;
            }

            if (!edge.Conditions.Any(condition => condition is QuenchMetal))
                failures.Add($"{item.ID}: graph {graphId.Id} does not require quenching");
            if (!edge.Completed.Any(action => action is FinishForgedItem))
                failures.Add($"{item.ID}: graph {graphId.Id} does not finish the forged item");
        }
    }

    private static void AssertEntityHasMetallic(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        EntProtoId entityId,
        string owner,
        ICollection<string> failures)
    {
        if (!prototypes.TryIndex<EntityPrototype>(entityId, out var entity) || entity!.Abstract)
        {
            failures.Add($"{owner}: entity {entityId.Id} is missing or abstract");
            return;
        }

        if (!entity.TryGetComponent<MetallicComponent>(out _, factory))
            failures.Add($"{owner}: entity {entityId.Id} must inherit Metallic");
        if (!entity.TryGetComponent<DurabilityComponent>(out _, factory))
            failures.Add($"{owner}: entity {entityId.Id} must inherit Durability so metal strength is applied");
    }
}
