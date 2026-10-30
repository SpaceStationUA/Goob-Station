using System.Collections.Generic;
using System.Linq;
using Content.Shared._Pirate.Knowledge;
using Content.Shared._Pirate.Traits.Assorted;
using Content.Shared._Pirate.Traits.Conditions;
using Content.Shared.Traits;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Knowledge;

[TestFixture]
public sealed class KnowledgeTraitIntegrationTest
{
    private static readonly ProtoId<TraitPrototype> Unchipped = new("Unchipped");
    private static readonly ProtoId<TraitPrototype> Experienced = new("Experienced");
    private static readonly ProtoId<TraitPrototype> ExperiencedAlt = new("ExperiencedAlt");

    [Test]
    public async Task KnowledgeTraitsHaveCostsBonusesAndTraitConditions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var prototypes = pair.Server.ProtoMan;

        await pair.Server.WaitAssertion(() =>
        {
            var unchipped = prototypes.Index(Unchipped);
            var experienced = prototypes.Index(Experienced);
            var experiencedAlt = prototypes.Index(ExperiencedAlt);
            var experiencedCondition = experienced.Conditions.OfType<HasTraitsCondition>().Single();
            var alternativeConditions = experiencedAlt.Conditions.OfType<HasTraitsCondition>().ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(unchipped.Cost, Is.EqualTo(-3));
                Assert.That(experienced.Cost, Is.EqualTo(2));
                Assert.That(experiencedAlt.Cost, Is.EqualTo(3));
                Assert.That(experiencedCondition.Invert, Is.True);
                Assert.That(experiencedCondition.Traits, Does.Contain(ExperiencedAlt));
                Assert.That(alternativeConditions, Has.Length.EqualTo(2));
                Assert.That(alternativeConditions.Single(condition => condition.Invert).Traits,
                    Does.Contain(Experienced));
                Assert.That(alternativeConditions.Single(condition => !condition.Invert).Traits,
                    Does.Contain(Unchipped));
                Assert.That(KnowledgeableComponent.GetBonusPoints(prototypes, Traits(Experienced)), Is.EqualTo(6));
                Assert.That(
                    KnowledgeableComponent.GetBonusPoints(prototypes, Traits(Unchipped, ExperiencedAlt)),
                    Is.EqualTo(10));

                Assert.That(KnowledgeableComponent.GetMasteryGrants(prototypes, Traits(Experienced)),
                    Is.EquivalentTo(new Dictionary<EntProtoId, int> { ["LiteracyKnowledge"] = 2 }));
                Assert.That(KnowledgeableComponent.GetMasteryGrants(prototypes, Traits(Unchipped, ExperiencedAlt)),
                    Is.EquivalentTo(new Dictionary<EntProtoId, int> { ["LiteracyKnowledge"] = 3 }));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LiteracySpeedsUpLearning()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var knowledge = server.System<SharedKnowledgeSystem>();

        await server.WaitAssertion(() =>
        {
            Assert.That(SharedKnowledgeSystem.LearningMultiplier(0), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(SharedKnowledgeSystem.LearningMultiplier(50), Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(SharedKnowledgeSystem.LearningMultiplier(100), Is.EqualTo(2f).Within(0.0001f));

            var human = server.EntMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var store = knowledge.EnsureKnowledgeContainer(human);
            knowledge.EnsureKnowledge(store, SharedKnowledgeSystem.LiteracyKnowledge, 0, popup: false);
            Assert.That(knowledge.GetLearningMultiplier(human), Is.EqualTo(1f).Within(0.0001f));

            var literacy = knowledge.GetKnowledge(store, SharedKnowledgeSystem.LiteracyKnowledge)!.Value;
            literacy.Comp.LearnedLevel = 100;
            Assert.That(knowledge.GetLearningMultiplier(human), Is.EqualTo(2f).Within(0.0001f));
        });

        await pair.CleanReturnAsync();
    }

    private static HashSet<ProtoId<TraitPrototype>> Traits(params ProtoId<TraitPrototype>[] traits)
        => new(traits);
}
