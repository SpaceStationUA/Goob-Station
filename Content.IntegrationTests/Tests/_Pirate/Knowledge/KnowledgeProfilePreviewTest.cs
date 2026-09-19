// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Knowledge;

namespace Content.IntegrationTests.Tests._Pirate.Knowledge;

[TestFixture]
public sealed class KnowledgeProfilePreviewTest
{
    [TestCase(0, 0, 0, 0, 0, 0)]
    [TestCase(1, 0, 0, 25, 0, 25)]
    [TestCase(1, 28, 0, 25, 0, 53)]
    [TestCase(0, 28, 1, 0, 22, 50)]
    [TestCase(0, -15, 1, 0, 40, 25)]
    [TestCase(2, -25, 1, 50, 25, 50)]
    [TestCase(3, 28, 1, 75, 0, 100)]
    [TestCase(0, -1000, 0, 0, 0, 0)]
    [TestCase(0, -1000, 1, 0, 1025, 25)]
    public void PreviewMatchesSpawnArithmetic(
        int profileMastery,
        int jobLevel,
        int employerMastery,
        int expectedProfileLevel,
        int expectedEmployerLevel,
        int expectedFinalLevel)
    {
        var finalLevel = SharedKnowledgeSystem.GetProfilePreviewLevel(
            profileMastery,
            jobLevel,
            employerMastery,
            out var profileLevel,
            out var employerLevel);

        Assert.Multiple(() =>
        {
            Assert.That(profileLevel, Is.EqualTo(expectedProfileLevel));
            Assert.That(employerLevel, Is.EqualTo(expectedEmployerLevel));
            Assert.That(finalLevel, Is.EqualTo(expectedFinalLevel));
        });
    }
}
