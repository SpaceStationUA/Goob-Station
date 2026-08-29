// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Content.Shared.Construction.Components;
using Content.Shared.Construction.NodeEntities;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Construction.Steps;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Knowledge;

[TestFixture]
public sealed class ConstructionResourceIntegrityTest
{
    private static readonly HashSet<string> PortedGraphIds =
    [
        "AccessScanner",
        "BarbCuffs",
        "BarbedWire",
        "BarbedWireMaterial",
        "BoogieBot",
        "Bracing",
        "Crucifix",
        "DefibrillatorCabinet",
        "DuckBot",
        "GaussImprovAmmoGraph",
        "GaussImprovMagazineGraph",
        "GaussPistolGraph",
        "GaussRifleGraph",
        "IEDSpear",
        "Killdozer",
        "NailBoard",
        "NailSword",
        "PipeBombTrap",
        "PipeShank",
        "PipebombSpear",
        "PowerArmorFrame",
        "PowerArmorStand",
        "RevBoots",
        "RevFlamethrowerAmmo",
        "RevGuns",
        "RevIED",
        "RevHelmet",
        "RevKnife",
        "RevKunai",
        "RevMace",
        "RevMask",
        "RevMaul",
        "RevMachine",
        "RevParts",
        "RevPitchfork",
        "RevRockets",
        "RevSword",
        "RevTorch",
        "RevTurret",
        "RevVest",
        "ScrapSword",
        "SecBot",
        "SheetPaper",
        "SignalScreen",
        "TilePlastic",
        "T45Armor",
        "TripWire",
        "WallInsulation",
        "Wandsky",
        "WeldedSword",
        "Whetstone",
        "WoodenSword",
    ];

    private static readonly FieldInfo TagField = typeof(TagConstructionGraphStep)
        .GetField("Tag", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo AllTagsField = typeof(MultipleTagsConstructionGraphStep)
        .GetField("_allTags", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo AnyTagsField = typeof(MultipleTagsConstructionGraphStep)
        .GetField("_anyTags", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Test]
    public async Task EveryPortedConstructionInputHasAConcreteSourceAndEveryRecipeHasAPath()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypes = server.ProtoMan;
        var factory = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            var failures = new List<string>();
            var entities = prototypes.EnumeratePrototypes<EntityPrototype>()
                .Where(entity => !entity.Abstract)
                .ToArray();

            Assert.That(PortedGraphIds, Has.Count.EqualTo(52),
                "The exhaustive ported construction graph list changed without updating its test.");

            foreach (var graphId in PortedGraphIds.Order())
            {
                if (!prototypes.TryIndex<ConstructionGraphPrototype>(graphId, out var graph))
                {
                    failures.Add($"{graphId}: construction graph is missing");
                    continue;
                }

                ValidateGraph(prototypes, factory, entities, graph!, failures);
            }

            var recipes = prototypes.EnumeratePrototypes<ConstructionPrototype>()
                .Where(recipe => !recipe.Abstract && PortedGraphIds.Contains(recipe.Graph.Id))
                .ToArray();

            foreach (var recipe in recipes)
            {
                if (!prototypes.TryIndex<ConstructionGraphPrototype>(recipe.Graph, out var graph))
                {
                    failures.Add($"{recipe.ID}: graph {recipe.Graph.Id} is missing");
                    continue;
                }

                if (!graph!.Nodes.ContainsKey(recipe.StartNode))
                {
                    failures.Add($"{recipe.ID}: start node {recipe.StartNode} is missing from {graph.ID}");
                    continue;
                }

                if (!graph.Nodes.ContainsKey(recipe.TargetNode))
                {
                    failures.Add($"{recipe.ID}: target node {recipe.TargetNode} is missing from {graph.ID}");
                    continue;
                }

                if (!graph.TryPath(recipe.StartNode, recipe.TargetNode, out _))
                    failures.Add($"{recipe.ID}: no path from {recipe.StartNode} to {recipe.TargetNode} in {graph.ID}");
            }

            Assert.That(recipes, Is.Not.Empty, "No concrete recipe uses the ported construction graphs.");
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        });

        await pair.CleanReturnAsync();
    }

    private static void ValidateGraph(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        IReadOnlyCollection<EntityPrototype> entities,
        ConstructionGraphPrototype graph,
        ICollection<string> failures)
    {
        if (graph.Start is null || !graph.Nodes.ContainsKey(graph.Start))
            failures.Add($"{graph.ID}: start node {graph.Start ?? "<null>"} is missing");

        foreach (var node in graph.Nodes.Values)
        {
            if (node.Entity is StaticNodeEntity { Id: { } resultId })
            {
                if (!prototypes.TryIndex<EntityPrototype>(resultId, out var result) || result!.Abstract)
                    failures.Add($"{graph.ID}/{node.Name}: result {resultId} is missing or abstract");
            }

            foreach (var edge in node.Edges)
            {
                var location = $"{graph.ID}/{node.Name}->{edge.Target}";
                if (!graph.Nodes.ContainsKey(edge.Target))
                {
                    failures.Add($"{location}: target node is missing");
                    continue;
                }

                foreach (var step in edge.Steps)
                    ValidateStep(prototypes, factory, entities, step, location, failures);
            }
        }
    }

    private static void ValidateStep(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        IReadOnlyCollection<EntityPrototype> entities,
        ConstructionGraphStep step,
        string location,
        ICollection<string> failures)
    {
        switch (step)
        {
            case MaterialConstructionGraphStep material:
                ValidateMaterial(prototypes, factory, material, location, failures);
                break;
            case TagConstructionGraphStep tag:
                ValidateTag(prototypes, factory, entities, tag, location, failures);
                break;
            case MultipleTagsConstructionGraphStep tags:
                ValidateTags(prototypes, factory, entities, tags, location, failures);
                break;
            case ComponentConstructionGraphStep component:
                if (string.IsNullOrWhiteSpace(component.Component) ||
                    !entities.Any(entity => entity.Components.ContainsKey(component.Component)))
                {
                    failures.Add($"{location}: no concrete entity supplies component {component.Component}");
                }
                break;
            case ToolConstructionGraphStep tool:
                ValidateTool(prototypes, factory, tool, location, failures);
                break;
            case TemperatureConstructionGraphStep temperature:
                if (temperature.MinTemperature is null && temperature.MaxTemperature is null)
                    failures.Add($"{location}: temperature step has no limit");
                if (temperature.MinTemperature is { } min && !float.IsFinite(min))
                    failures.Add($"{location}: minimum temperature is not finite");
                if (temperature.MaxTemperature is { } max && !float.IsFinite(max))
                    failures.Add($"{location}: maximum temperature is not finite");
                if (temperature.MinTemperature is { } lower &&
                    temperature.MaxTemperature is { } upper &&
                    lower > upper)
                {
                    failures.Add($"{location}: minimum temperature {lower} exceeds maximum {upper}");
                }
                break;
            case PartAssemblyConstructionGraphStep assembly:
                if (string.IsNullOrWhiteSpace(assembly.AssemblyId) ||
                    !entities.Any(entity =>
                        entity.TryGetComponent<PartAssemblyComponent>(out var partAssembly, factory) &&
                        partAssembly.Parts.ContainsKey(assembly.AssemblyId)))
                {
                    failures.Add($"{location}: no concrete part assembly supplies {assembly.AssemblyId}");
                }
                break;
            default:
                failures.Add($"{location}: unsupported construction step {step.GetType().Name}");
                break;
        }
    }

    private static void ValidateMaterial(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        MaterialConstructionGraphStep material,
        string location,
        ICollection<string> failures)
    {
        if (material.Amount <= 0)
        {
            failures.Add($"{location}: material {material.MaterialPrototypeId.Id} has invalid amount {material.Amount}");
            return;
        }

        if (!prototypes.TryIndex<StackPrototype>(material.MaterialPrototypeId, out var stackPrototype) ||
            stackPrototype!.Abstract)
        {
            failures.Add($"{location}: stack {material.MaterialPrototypeId.Id} is missing or abstract");
            return;
        }

        if (!prototypes.TryIndex<EntityPrototype>(stackPrototype.Spawn, out var spawn) || spawn!.Abstract)
        {
            failures.Add($"{location}: stack {stackPrototype.ID} has no concrete spawn {stackPrototype.Spawn.Id}");
            return;
        }

        if (!spawn.TryGetComponent<StackComponent>(out var stack, factory) ||
            stack.StackTypeId != material.MaterialPrototypeId)
        {
            failures.Add($"{location}: {stackPrototype.Spawn.Id} does not supply stack {stackPrototype.ID}");
            return;
        }

        var maxCount = stack.MaxCountOverride ?? stackPrototype.MaxCount;
        if (maxCount is { } maximum && maximum < material.Amount)
        {
            failures.Add(
                $"{location}: requires {material.Amount} {stackPrototype.ID}, but its largest stack holds {maximum}");
        }
    }

    private static void ValidateTag(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        IReadOnlyCollection<EntityPrototype> entities,
        TagConstructionGraphStep step,
        string location,
        ICollection<string> failures)
    {
        var tag = (ProtoId<TagPrototype>) TagField.GetValue(step)!;
        if (!prototypes.HasIndex<TagPrototype>(tag))
        {
            failures.Add($"{location}: tag {tag.Id} is missing");
            return;
        }

        if (!entities.Any(entity => HasTags(entity, factory, [tag], [])))
            failures.Add($"{location}: no concrete entity supplies tag {tag.Id}");
    }

    private static void ValidateTags(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        IReadOnlyCollection<EntityPrototype> entities,
        MultipleTagsConstructionGraphStep step,
        string location,
        ICollection<string> failures)
    {
        var all = (List<ProtoId<TagPrototype>>?) AllTagsField.GetValue(step) ?? [];
        var any = (List<ProtoId<TagPrototype>>?) AnyTagsField.GetValue(step) ?? [];
        if (all.Count == 0 && any.Count == 0)
        {
            failures.Add($"{location}: multiple-tags step has no tags");
            return;
        }

        foreach (var tag in all.Concat(any))
        {
            if (!prototypes.HasIndex<TagPrototype>(tag))
                failures.Add($"{location}: tag {tag.Id} is missing");
        }

        if (!entities.Any(entity => HasTags(entity, factory, all, any)))
        {
            failures.Add(
                $"{location}: no concrete entity satisfies all [{string.Join(", ", all)}] and any [{string.Join(", ", any)}]");
        }
    }

    private static bool HasTags(
        EntityPrototype entity,
        IComponentFactory factory,
        IReadOnlyCollection<ProtoId<TagPrototype>> all,
        IReadOnlyCollection<ProtoId<TagPrototype>> any)
    {
        if (!entity.TryGetComponent<TagComponent>(out var tags, factory))
            return false;

        return all.All(tags.Tags.Contains) && (any.Count == 0 || any.Any(tags.Tags.Contains));
    }

    private static void ValidateTool(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        ToolConstructionGraphStep step,
        string location,
        ICollection<string> failures)
    {
        if (!prototypes.TryIndex<ToolQualityPrototype>(step.Tool, out var quality))
        {
            failures.Add($"{location}: tool quality {step.Tool} is missing");
            return;
        }

        if (!prototypes.TryIndex<EntityPrototype>(quality!.Spawn, out var spawn) || spawn!.Abstract)
        {
            failures.Add($"{location}: tool quality {step.Tool} has no concrete spawn {quality.Spawn}");
            return;
        }

        if (!spawn.TryGetComponent<ToolComponent>(out var tool, factory))
        {
            failures.Add($"{location}: default tool {quality.Spawn} does not provide quality {step.Tool}");
            return;
        }

        var qualities = tool.Qualities;
        if (!qualities.Contains(step.Tool))
            failures.Add($"{location}: default tool {quality.Spawn} does not provide quality {step.Tool}");
    }
}
