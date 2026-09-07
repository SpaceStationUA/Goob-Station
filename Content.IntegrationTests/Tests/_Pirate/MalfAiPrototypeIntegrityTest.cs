// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Antag.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Roles.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate;

[TestFixture]
public sealed class MalfAiPrototypeIntegrityTest
{
    [Test]
    public async Task MalfAiRuleHasCompleteSelectionAndObjectiveWiring()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypes = server.ProtoMan;
        var factory = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            Assert.That(prototypes.TryIndex<EntityPrototype>("MalfAi", out var rule), Is.True);
            Assert.That(rule!.TryGetComponent<GameRuleComponent>(out var gameRule, factory), Is.True);
            Assert.That(gameRule!.MinPlayers, Is.EqualTo(20));
            Assert.That(rule.TryGetComponent<AntagSelectionComponent>(out var selection, factory), Is.True);
            Assert.That(selection!.Definitions, Has.Count.EqualTo(1));
            Assert.That(selection.Definitions[0].Max, Is.EqualTo(1));
            Assert.That(selection.Definitions[0].Whitelist.Components, Does.Contain("StationAiHeld"));
            Assert.That(selection.Definitions[0].MindRoles, Does.Contain("MindRoleMalfAi"));

            Assert.That(prototypes.TryIndex<EntityPrototype>("MindRoleMalfAi", out var mindRole), Is.True);
            Assert.That(mindRole!.TryGetComponent<MindRoleComponent>(out var role, factory), Is.True);
            Assert.That(role!.AntagPrototype, Is.EqualTo("MalfunctioningAI"));
            Assert.That(role.RoleType, Is.EqualTo("MalfunctioningSilicon"));
        });
    }
}