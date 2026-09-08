// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Server.Silicons.Borgs;

namespace Content.IntegrationTests.Tests._Pirate.Silicons;

[TestFixture]
[TestOf(typeof(SecurityBorgAlertLevelPolicy))]
public sealed class SecurityBorgAlertLevelPolicyTest
{
    [TestCase("green", SecurityBorgAlertLevelTier.Green)]
    [TestCase("red", SecurityBorgAlertLevelTier.Full)]
    [TestCase("violet", SecurityBorgAlertLevelTier.Full)]
    [TestCase("gamma", SecurityBorgAlertLevelTier.Full)]
    [TestCase("delta", SecurityBorgAlertLevelTier.Full)]
    [TestCase("epsilon", SecurityBorgAlertLevelTier.Full)]
    [TestCase("amber", SecurityBorgAlertLevelTier.Full)]
    [TestCase("octarine", SecurityBorgAlertLevelTier.Full)]
    [TestCase("blue", SecurityBorgAlertLevelTier.Officer)]
    [TestCase("yellow", SecurityBorgAlertLevelTier.Officer)]
    [TestCase("orange", SecurityBorgAlertLevelTier.Officer)]
    [TestCase("omicron", SecurityBorgAlertLevelTier.Officer)]
    [TestCase("honk", SecurityBorgAlertLevelTier.Officer)]
    [TestCase("white", SecurityBorgAlertLevelTier.Officer)]
    [TestCase("future-alert", SecurityBorgAlertLevelTier.Officer)]
    [TestCase(null, SecurityBorgAlertLevelTier.Green)]
    public void GetsExpectedTier(string? alertLevel, SecurityBorgAlertLevelTier expected)
    {
        Assert.That(SecurityBorgAlertLevelPolicy.GetTier(alertLevel), Is.EqualTo(expected));
    }
}
