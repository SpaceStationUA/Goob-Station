// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Pirate.Shared.Genetics.Mutations;
using Content.Pirate.Shared.Genetics.Console;
using Content.Pirate.Shared.Genetics.Tools;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Genetics;

[TestFixture]
public sealed class GeneticsPrototypeIntegrityTest
{
    // Pinned Trauma catalog: additions/removals must update this exhaustive port contract.
    public static readonly string[] ExpectedMutations =
    [
        "MutationAcidicFlesh",
        "MutationAcromegaly",
        "MutationAdrenalineRush",
        "MutationAntenna",
        "MutationAntiGlow",
        "MutationAutotomy",
        "MutationBlindness",
        "MutationChameleon",
        "MutationChav",
        "MutationChemSpike",
        "MutationCindikinesis",
        "MutationClever",
        "MutationClumsiness",
        "MutationColdAdaptation",
        "MutationColdWeakness",
        "MutationCough",
        "MutationCryokinesis",
        "MutationDeafness",
        "MutationDogNose",
        "MutationDwarfism",
        "MutationElasticArms",
        "MutationElvis",
        "MutationEnhancedPerception",
        "MutationEpilepsy",
        "MutationFarsight",
        "MutationFelinized",
        "MutationFierySweat",
        "MutationFireBreath",
        "MutationGeladikinesis",
        "MutationGigantism",
        "MutationGlowy",
        "MutationHeadless",
        "MutationHeatAdaptation",
        "MutationHeatWeakness",
        "MutationHeckaciousLarincks",
        "MutationHulk",
        "MutationHypermetabolicBlood",
        "MutationIlliterate",
        "MutationInexorable",
        "MutationInsulated",
        "MutationInternalMartyrdom",
        "MutationLaserEyes",
        "MutationMedieval",
        "MutationMendingTouch",
        "MutationMindReader",
        "MutationMonkified",
        "MutationMouse",
        "MutationMute",
        "MutationNearSightedness",
        "MutationNervousness",
        "MutationOrk",
        "MutationParanoia",
        "MutationPigLatin",
        "MutationPressureAdaptation",
        "MutationPyrokinesis",
        "MutationRadioactivity",
        "MutationRockAbsorber",
        "MutationRockEater",
        "MutationShockTouch",
        "MutationSpastic",
        "MutationSpatialInstability",
        "MutationStimmed",
        "MutationStoner",
        "MutationStrength",
        "MutationSwedish",
        "MutationTelekinesis",
        "MutationTelepathy",
        "MutationThermalAdaptation",
        "MutationThermalVision",
        "MutationThermalWeakness",
        "MutationTongueSpike",
        "MutationTourettes",
        "MutationTwoLeftFeet",
        "MutationUnintelligible",
        "MutationUnstableDna",
        "MutationVoidMagnet",
        "MutationWacky",
        "MutationWeakPerception",
        "MutationWebbingProduction",
    ];

    [Test]
    public async Task EntireMutationCatalogAndEveryRecipeAreValid()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var mutations = server.System<MutationSystem>();
        var prototypes = server.ProtoMan;
        await server.WaitAssertion(() =>
        {
            Assert.That(mutations.AllMutations.Keys.Select(id => id.ToString()), Is.EquivalentTo(ExpectedMutations));
            var numbers = new HashSet<int>();
            foreach (var (id, mutation) in mutations.AllMutations)
            {
                Assert.That(mutation.Difficulty, Is.InRange(0, MutationData.BaseCount), id.ToString());
                foreach (var dependency in mutation.Required.Concat(mutation.Conflicts))
                    Assert.That(mutations.AllMutations.ContainsKey(dependency), Is.True, $"{id} references {dependency}");
                var data = mutations.GetRoundData(id)!;
                Assert.That(numbers.Add(data.Number), Is.True, $"Duplicate number for {id}");
                Assert.That(data.Bases, Has.Length.EqualTo(MutationData.BaseCount));
                for (var i = 0; i < MutationData.PairCount; i++)
                    Assert.That(data.Bases[i + MutationData.PairCount], Is.EqualTo(MutationData.GetMatching(data.Bases[i])));
                Assert.That(mutations.CombineMutations(id, id), Is.Null);
            }

            var combinations = new Dictionary<(string, string), string>();
            var recipes = prototypes.EnumeratePrototypes<MutationRecipePrototype>().ToArray();
            Assert.That(recipes, Is.Not.Empty);
            foreach (var recipe in recipes)
            {
                Assert.That(recipe.Required, Has.Count.EqualTo(2), recipe.ID);
                Assert.That(recipe.Required.Distinct().Count(), Is.EqualTo(2), recipe.ID);
                Assert.That(mutations.AllMutations.ContainsKey(recipe.Result), Is.True, recipe.ID);
                var a = recipe.Required[0];
                var b = recipe.Required[1];
                Assert.That(mutations.AllMutations.ContainsKey(a) && mutations.AllMutations.ContainsKey(b), Is.True);
                Assert.That(mutations.CombineMutations(a, b), Is.EqualTo(recipe.Result), recipe.ID);
                Assert.That(mutations.CombineMutations(b, a), Is.EqualTo(recipe.Result), recipe.ID);
                var sorted = new[] { a.ToString(), b.ToString() }.Order().ToArray();
                Assert.That(combinations.TryAdd((sorted[0], sorted[1]), recipe.Result.ToString()), Is.True,
                    $"Ambiguous combination: {recipe.ID}");
            }

            foreach (var a in mutations.AllMutations.Keys)
            foreach (var b in mutations.AllMutations.Keys)
            {
                var sorted = new[] { a.ToString(), b.ToString() }.Order().ToArray();
                combinations.TryGetValue((sorted[0], sorted[1]), out var expected);
                Assert.That(mutations.CombineMutations(a, b)?.ToString(), Is.EqualTo(expected), $"{a} + {b}");
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ConsolePrintsAndSpeciesDefaultsAreWired()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var mutations = server.System<MutationSystem>();
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var console = entities.SpawnEntity("ComputerGeneticsConsole", map.GridCoords);
            var prints = entities.GetComponent<GeneticsConsoleComponent>(console).Prints;
            Assert.That(prints.Select(print => (print.Proto.ToString(), print.Cost)), Is.EquivalentTo(new[]
            {
                ("GeneticsMutator", 20), ("GeneticsActivator", 10), ("GeneticsCleanser", 15),
            }));
            foreach (var print in prints)
            {
                var injector = entities.SpawnEntity(print.Proto, map.GridCoords);
                Assert.That(entities.HasComponent<MutatorComponent>(injector), Is.True);
                entities.DeleteEntity(injector);
            }
            foreach (var species in new[] { "MobHuman", "MobDwarf", "MobReptilian", "MobMonkey", "MobKobold" })
            {
                var mob = entities.SpawnEntity(species, map.GridCoords);
                Assert.That(mutations.IsMutatable(mob), Is.True, species);
                var comp = entities.GetComponent<MutatableComponent>(mob);
                Assert.That(comp.Dormant.Count, Is.EqualTo(comp.MaxDormant), species);
                Assert.That(comp.Dormant.Distinct().Count(), Is.EqualTo(comp.Dormant.Count));
                foreach (var id in comp.Dormant)
                    Assert.That(mutations.AllMutations.ContainsKey(id), Is.True);
                entities.DeleteEntity(mob);
            }
            entities.DeleteEntity(console);
        });
        await pair.CleanReturnAsync();
    }
}
