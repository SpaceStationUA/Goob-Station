// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Shared._Pirate.EntityEffects.Knowledge;
using Content.Shared._Pirate.Knowledge;
using Content.Shared._Pirate.Revolutionary;
using Content.Shared.EntityEffects;
using Content.Shared.Revolutionary.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Knowledge;

[TestFixture]
public sealed class RevolutionaryKnowledgeIntegrationTest
{
    private static readonly Dictionary<string, string> CodeGranted = new()
    {
        ["RevolutionaryKnowledge"] = nameof(RevolutionaryKnowledgeSystem),
    };

    [Test]
    public async Task RevolutionariesGetAndLoseTheirCraftingKnowledge()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var knowledge = server.System<SharedKnowledgeSystem>();

        await server.WaitAssertion(() =>
        {
            var human = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var store = knowledge.GetContainer(human);
            Assert.That(store, Is.Not.Null, "A human should have a knowledge store.");

            Assert.That(knowledge.GetKnowledge(store!.Value, RevolutionaryKnowledgeSystem.RevolutionaryKnowledge),
                Is.Null, "A non-revolutionary must not know how to build revolutionary gear.");

            entMan.AddComponent<RevolutionaryComponent>(human);

            var learned = knowledge.GetKnowledge(store.Value, RevolutionaryKnowledgeSystem.RevolutionaryKnowledge);
            Assert.That(learned, Is.Not.Null,
                "Revolutionaries must receive RevolutionaryKnowledge, or every rev recipe is uncraftable.");
            Assert.That(learned!.Value.Comp.LearnedLevel, Is.EqualTo(100),
                "Rev recipes ask for mastery well above zero.");

            entMan.RemoveComponent<RevolutionaryComponent>(human);
            Assert.That(knowledge.GetKnowledge(store.Value, RevolutionaryKnowledgeSystem.RevolutionaryKnowledge),
                Is.Null, "A deconverted revolutionary must forget how to build revolutionary gear.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EveryUnbuyableKnowledgeHasSomeWayToBeObtained()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var proto = server.ProtoMan;
        var factory = server.EntMan.ComponentFactory;

        await server.WaitAssertion(() =>
        {
            var granted = new HashSet<string>();

            void Collect(IReadOnlyDictionary<EntProtoId, int>? skills)
            {
                if (skills == null)
                    return;

                foreach (var (id, level) in skills)
                {
                    if (level > 0)
                        granted.Add(id.Id);
                }
            }

            foreach (var entity in proto.EnumeratePrototypes<EntityPrototype>())
            {
                if (entity.TryGetComponent<KnowledgeGrantOnWearComponent>(out var onWear, factory))
                    Collect(onWear.Skills);

                if (entity.TryGetComponent<KnowledgeGrantOnUseComponent>(out var onUse, factory))
                    Collect(onUse.Skills);
            }

            foreach (var effect in proto.EnumeratePrototypes<EntityEffectPrototype>())
            {
                foreach (var entry in effect.Effects)
                {
                    switch (entry)
                    {
                        case GrantSkills grant:
                            Collect(grant.Skills);
                            break;
                        case GrantTemporarySkills temporary:
                            Collect(temporary.Skills);
                            break;
                    }
                }
            }

            foreach (var profile in proto.EnumeratePrototypes<KnowledgeProfilePrototype>())
            {
                if (profile.Profile.Mastery == null)
                    continue;

                foreach (var (id, mastery) in profile.Profile.Mastery)
                {
                    if (mastery > 0)
                        granted.Add(id.Id);
                }
            }

            foreach (var entity in proto.EnumeratePrototypes<EntityPrototype>())
            {
                if (entity.Abstract ||
                    !entity.TryGetComponent<KnowledgeComponent>(out var unit, factory) ||
                    unit.Costs != null)
                {
                    continue;
                }

                if (granted.Contains(entity.ID))
                    continue;

                Assert.That(CodeGranted.ContainsKey(entity.ID), Is.True,
                    $"{entity.ID} declares no mastery costs and nothing grants it, so no character can " +
                    "ever obtain it and anything gated behind it is unreachable. Give it costs, grant " +
                    "it from a chip, package or profile, or add it to CodeGranted with the system " +
                    "that hands it out.");
            }
        });

        await pair.CleanReturnAsync();
    }
}
