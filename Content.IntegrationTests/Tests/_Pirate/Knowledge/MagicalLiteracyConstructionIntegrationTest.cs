// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Goobstation.Common.Construction;
using Content.Shared._Pirate.Knowledge;
using Content.Shared.Construction.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Knowledge;

/// <summary>
/// Temporary regression coverage for experience awarded by completed demon summoning circles.
/// </summary>
[TestFixture]
public sealed class MagicalLiteracyConstructionIntegrationTest
{
    [TestPrototypes]
    private const string TestPrototypes = @"
- type: entity
  id: PirateMagicalLiteracyTestHolder
  components:
  - type: KnowledgeHolder
";

    [Test]
    public async Task SummoningCircleConstructionAwardsExperienceBySize()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var prototypes = server.ProtoMan;
        var knowledge = server.System<SharedKnowledgeSystem>();
        var expected = new Dictionary<string, int>
        {
            ["MinorSummoning"] = 5,
            ["MediumSummoning"] = 10,
            ["MajorSummoning"] = 15,
        };

        await server.WaitAssertion(() =>
        {
            Assert.That(knowledge.SkillsEnabled, Is.True);
            Assert.That(knowledge.SkillGainEnabled, Is.True);

            foreach (var (recipeId, experience) in expected)
            {
                var recipe = prototypes.Index<ConstructionPrototype>(recipeId);
                Assert.That(recipe.Experience, Is.EquivalentTo(
                    new Dictionary<EntProtoId, int> { ["MagicalLiteracyKnowledge"] = experience }), recipeId);

                var holder = entMan.SpawnEntity("PirateMagicalLiteracyTestHolder", MapCoordinates.Nullspace);
                var store = knowledge.EnsureKnowledgeContainer(holder);
                var skill = knowledge.EnsureKnowledge(store, "MagicalLiteracyKnowledge", popup: false);
                Assert.That(skill, Is.Not.Null, recipeId);
                skill!.Value.Comp.TimeToNextExperience = TimeSpan.Zero;

                var result = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
                var constructed = new ConstructedEvent(result, recipeId);
                entMan.EventBus.RaiseLocalEvent(holder, ref constructed);

                Assert.That(skill.Value.Comp.Experience, Is.EqualTo(experience), recipeId);
                entMan.DeleteEntity(result);
                entMan.DeleteEntity(holder);
            }
        });

        await pair.CleanReturnAsync();
    }
}
