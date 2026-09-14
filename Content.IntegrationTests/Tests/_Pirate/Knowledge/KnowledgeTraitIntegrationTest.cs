// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Shared._Pirate.Traits.Assorted;
using Content.Shared._Pirate.Traits.Conditions;
using Content.Shared.Traits;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Knowledge;

[TestFixture]
public sealed class KnowledgeTraitIntegrationTest
{
    private static readonly ProtoId<TraitPrototype> Unchipped = new("Unchipped");
    private static readonly ProtoId<TraitPrototype> Experienced = new("Experienced");
    private static readonly ProtoId<TraitPrototype> ExperiencedAlt = new("ExperiencedAlt");

    [Test]
    public async Task KnowledgeTraitsHaveIntendedCostsBonusesAndCompatibility()
    {
        await using var pair = await PoolManager.GetServerClient();
        var prototypes = pair.Server.ProtoMan;

        await pair.Server.WaitAssertion(() =>
        {
            var unchipped = prototypes.Index(Unchipped);
            var experienced = prototypes.Index(Experienced);
            var experiencedAlt = prototypes.Index(ExperiencedAlt);
            var unchippedRequirement = experiencedAlt.Conditions.OfType<HasTraitsCondition>().Single();

            Assert.Multiple(() =>
            {
                Assert.That(unchipped.Cost, Is.EqualTo(-3));
                Assert.That(experienced.Cost, Is.EqualTo(2));
                Assert.That(experienced.Conflicts, Does.Contain(ExperiencedAlt));
                Assert.That(experiencedAlt.Conflicts, Does.Contain(Experienced));
                Assert.That(unchippedRequirement.Traits, Does.Contain(Unchipped));
                Assert.That(KnowledgeableComponent.GetBonusPoints(prototypes, Traits(Experienced)), Is.EqualTo(6));
                Assert.That(KnowledgeableComponent.GetBonusPoints(prototypes, Traits(ExperiencedAlt)), Is.Zero);
                Assert.That(
                    KnowledgeableComponent.GetBonusPoints(prototypes, Traits(Unchipped, ExperiencedAlt)),
                    Is.EqualTo(10));
            });
        });

        await pair.CleanReturnAsync();
    }

    private static HashSet<ProtoId<TraitPrototype>> Traits(params ProtoId<TraitPrototype>[] traits)
        => new(traits);
}
