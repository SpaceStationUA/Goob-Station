// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Goobstation.Common.Construction;
using Content.Shared._Pirate.Construction;
using Content.Shared._Pirate.Knowledge;
using Content.Shared._Pirate.Knowledge.Quality;
using Content.Shared.Construction.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Knowledge;

/// <summary>
/// Exercises the skill-aware construction event path with a clean holder. This catches both
/// missing subscriptions and recipes whose theory metadata cannot be enforced at runtime.
/// </summary>
[TestFixture]
public sealed class ConstructionKnowledgeIntegrationTest
{
    [TestPrototypes]
    private const string TestPrototypes = @"
- type: entity
  id: PirateKnowledgeTestHolder
  components:
  - type: KnowledgeHolder
";

    [Test]
    public async Task ConstructionTheoryBlocksAndAllowsWithRequiredMastery()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var prototypes = server.ProtoMan;
        var knowledge = server.System<SharedKnowledgeSystem>();

        await server.WaitAssertion(() =>
        {
            Assert.That(knowledge.SkillsEnabled, Is.True,
                "Skill-aware construction tests require the SkillsEnabled CVar to be enabled.");

            var recipe = prototypes.EnumeratePrototypes<ConstructionPrototype>()
                .FirstOrDefault(candidate => candidate.Theory.Values.Any(required => required > 0));
            Assert.That(recipe, Is.Not.Null,
                "No construction recipe with a non-zero theory requirement was found.");

            var holder = entMan.SpawnEntity("PirateKnowledgeTestHolder", MapCoordinates.Nullspace);
            var store = knowledge.EnsureKnowledgeContainer(holder);

            var requirements = recipe!.Theory.ToArray();
            var gated = requirements.First(requirement => requirement.Value > 0);

            // Give every requirement enough mastery except one. The selected missing mastery
            // must cancel the attempt.
            foreach (var (id, required) in requirements)
            {
                var level = SharedKnowledgeSystem.GetInverseMastery(required);
                Assert.That(knowledge.EnsureKnowledge(store, id, level, popup: false), Is.Not.Null,
                    $"Could not create required skill {id.Id}.");
            }

            var gatedSkill = knowledge.GetKnowledge(store, gated.Key);
            Assert.That(gatedSkill, Is.Not.Null);
            gatedSkill!.Value.Comp.LearnedLevel = Math.Max(0, gatedSkill.Value.Comp.LearnedLevel - 1);

            var blocked = new ConstructAttemptEvent(recipe.ID, LogError: false);
            entMan.EventBus.RaiseLocalEvent(holder, ref blocked);
            Assert.That(blocked.Cancelled, Is.True,
                $"{recipe.ID} was allowed with {gated.Key.Id} below mastery {gated.Value}.");

            gatedSkill.Value.Comp.LearnedLevel = SharedKnowledgeSystem.GetInverseMastery(gated.Value);
            var allowed = new ConstructAttemptEvent(recipe.ID, LogError: false);
            entMan.EventBus.RaiseLocalEvent(holder, ref allowed);
            Assert.That(allowed.Cancelled, Is.False,
                $"{recipe.ID} stayed blocked after all theory requirements were met.");

            entMan.DeleteEntity(holder);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ConstructedRecipeReceivesQualityExactlyOnce()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var prototypes = server.ProtoMan;
        var knowledge = server.System<SharedKnowledgeSystem>();

        await server.WaitAssertion(() =>
        {
            Assert.That(knowledge.SkillsEnabled, Is.True);
            var recipe = prototypes.EnumeratePrototypes<ConstructionPrototype>()
                .FirstOrDefault(candidate => candidate.UseQuality && candidate.Theory.Count > 0);
            Assert.That(recipe, Is.Not.Null,
                "No quality-enabled skill-aware construction recipe was found.");

            var holder = entMan.SpawnEntity("PirateKnowledgeTestHolder", MapCoordinates.Nullspace);
            var store = knowledge.EnsureKnowledgeContainer(holder);

            foreach (var (id, required) in recipe!.Theory)
            {
                Assert.That(knowledge.EnsureKnowledge(
                    store,
                    id,
                    SharedKnowledgeSystem.GetInverseMastery(required),
                    popup: false), Is.Not.Null);
            }

            var result = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var constructed = new ConstructedEvent(result, recipe.ID);
            entMan.EventBus.RaiseLocalEvent(holder, ref constructed);

            Assert.That(entMan.TryGetComponent<QualityComponent>(result, out var quality),
                $"{recipe.ID} did not add QualityComponent to its result.");
            Assert.That(quality!.Applied, Is.True,
                $"{recipe.ID} left its quality effects unapplied.");
            Assert.That(quality.LevelDeltas, Is.EquivalentTo(
                ConstructionKnowledgeSystem.CombineRequirements(recipe)),
                $"{recipe.ID} did not preserve theory/practical quality requirements.");

            var firstQuality = quality.Quality;
            var second = new ConstructedEvent(result, recipe.ID);
            entMan.EventBus.RaiseLocalEvent(holder, ref second);
            Assert.That(quality.Quality, Is.EqualTo(firstQuality),
                $"{recipe.ID} rerolled quality when ConstructedEvent was raised twice.");

            entMan.DeleteEntity(result);
            entMan.DeleteEntity(holder);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UnannotatedConstructionRecipeDoesNotReceiveQuality()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var recipe = server.ProtoMan.Index<ConstructionPrototype>("Tsword");
            Assert.Multiple(() =>
            {
                Assert.That(recipe.Theory, Is.Empty);
                Assert.That(recipe.Practical, Is.Null.Or.Empty);
            });

            var holder = entMan.SpawnEntity("PirateKnowledgeTestHolder", MapCoordinates.Nullspace);
            var result = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var constructed = new ConstructedEvent(result, recipe.ID);
            entMan.EventBus.RaiseLocalEvent(holder, ref constructed);

            Assert.That(entMan.HasComponent<QualityComponent>(result), Is.False,
                "Recipes without theory or practical skill annotations must retain their original stats.");

            entMan.DeleteEntity(result);
            entMan.DeleteEntity(holder);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public void CombineRequirementsKeepsTheHighestTheoryRequirement()
    {
        var prototype = new ConstructionPrototype
        {
            Theory = new Dictionary<EntProtoId, int>
            {
                ["FabricationKnowledge"] = 1,
                ["MetalworkingKnowledge"] = 3,
            },
            Practical = new Dictionary<EntProtoId, int>
            {
                ["FabricationKnowledge"] = 2,
                ["WeaponsKnowledge"] = 1,
            },
        };

        var combined = ConstructionKnowledgeSystem.CombineRequirements(prototype);
        Assert.That(combined, Is.EquivalentTo(new Dictionary<EntProtoId, int>
        {
            ["FabricationKnowledge"] = 2,
            ["MetalworkingKnowledge"] = 3,
            ["WeaponsKnowledge"] = 1,
        }));
    }
}
