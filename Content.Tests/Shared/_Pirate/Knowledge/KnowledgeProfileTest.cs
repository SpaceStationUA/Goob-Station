// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Shared._Pirate.Knowledge;
using Content.Shared.Preferences;
using NUnit.Framework;
using Robust.Shared.Prototypes;

namespace Content.Tests.Shared._Pirate.Knowledge;

[TestFixture]
[TestOf(typeof(KnowledgeProfile))]
public sealed class KnowledgeProfileTest
{
    [Test]
    public void ProfilesCopyCompareAndHashIndependently()
    {
        var original = new KnowledgeProfile(new Dictionary<EntProtoId, int>
        {
            ["FirstAidKnowledge"] = 2,
            ["DoorsKnowledge"] = 1,
        });
        var copy = new KnowledgeProfile(original);

        Assert.Multiple(() =>
        {
            Assert.That(copy.MemberwiseEquals(original), Is.True);
            Assert.That(copy.Mastery, Is.Not.SameAs(original.Mastery));
            Assert.That(default(KnowledgeProfile).MemberwiseEquals(default), Is.True);
            Assert.That(default(KnowledgeProfile).MemberwiseEquals(new KnowledgeProfile()), Is.False);
        });

        copy.Mastery["FirstAidKnowledge"] = 4;
        Assert.Multiple(() =>
        {
            Assert.That(original.Mastery["FirstAidKnowledge"], Is.EqualTo(2));
            Assert.That(copy.MemberwiseEquals(original), Is.False);
        });

        var reordered = new KnowledgeProfile(new Dictionary<EntProtoId, int>
        {
            ["DoorsKnowledge"] = 1,
            ["FirstAidKnowledge"] = 2,
        });
        Assert.That(original.MemberwiseEquals(reordered), Is.True,
            "Profile equality must not depend on dictionary insertion order.");

        var humanoid = new HumanoidCharacterProfile { Knowledge = original };
        var clone = humanoid.Clone();
        Assert.Multiple(() =>
        {
            Assert.That(clone.Knowledge.MemberwiseEquals(humanoid.Knowledge), Is.True);
            Assert.That(clone.Knowledge.Mastery, Is.Not.SameAs(humanoid.Knowledge.Mastery));
        });

        clone.Knowledge.Mastery["FirstAidKnowledge"] = 5;
        Assert.Multiple(() =>
        {
            Assert.That(humanoid.Knowledge.Mastery["FirstAidKnowledge"], Is.EqualTo(2));
            Assert.That(clone.Knowledge.MemberwiseEquals(humanoid.Knowledge), Is.False);
        });

        var sameHumanoid = new HumanoidCharacterProfile { Knowledge = new KnowledgeProfile(reordered) };
        Assert.That(humanoid.MemberwiseEquals(sameHumanoid), Is.True);
        sameHumanoid.Knowledge.Mastery["FirstAidKnowledge"] = 5;
        Assert.That(humanoid.MemberwiseEquals(sameHumanoid), Is.False,
            "Knowledge changes must participate in full character profile equality.");

        var replacement = new KnowledgeProfile(new Dictionary<EntProtoId, int>
        {
            ["FabricationKnowledge"] = 3,
        });
        var replaced = humanoid.WithKnowledge(replacement);
        replacement.Mastery["FabricationKnowledge"] = 5;
        Assert.Multiple(() =>
        {
            Assert.That(replaced.Knowledge.Mastery["FabricationKnowledge"], Is.EqualTo(3));
            Assert.That(replaced.Knowledge.Mastery, Is.Not.SameAs(replacement.Mastery));
            Assert.That(humanoid.Knowledge.MemberwiseEquals(original), Is.True);
        });

        humanoid.Knowledge = reordered;
        var reorderedHash = humanoid.GetHashCode();
        humanoid.Knowledge = original;
        Assert.That(humanoid.GetHashCode(), Is.EqualTo(reorderedHash),
            "Equivalent skill dictionaries must contribute the same hash regardless of insertion order.");

        humanoid.Knowledge = copy;
        Assert.That(humanoid.GetHashCode(), Is.Not.EqualTo(reorderedHash),
            "Changing a mastery value must contribute different profile hash data.");
    }
}
