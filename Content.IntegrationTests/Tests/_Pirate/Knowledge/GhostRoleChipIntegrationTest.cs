// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Server.Humanoid.Systems;
using Content.Shared._Pirate.Body.Chips;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared._Pirate.Knowledge;
using Content.Shared.Mind;
using Content.Pirate.Server.CharacterPods;
using Content.Shared.Preferences;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Pirate.Knowledge;

[TestFixture]
public sealed class GhostRoleChipIntegrationTest
{
    private static readonly (string Settings, string[] Chips)[] Expected =
    [
        ("Mercenary", ["SkillChipFreelancer"]),
        ("ERTLeader", ["SkillChipERT"]),
        ("ERTEngineer", ["SkillChipERT", "SkillChipDatabase"]),
        ("DeathSquad", ["SkillChipDeathSquad"]),
        ("SyndieSoldier", ["SkillChipSyndieSoldier"]),
        ("LavalandSyndieMarshal",
            ["SkillChipSyndieMarshal", "SkillChipFieldMedicine", "SkillChipDatabase"]),
        ("VisitorSecurityOfficer",
            ["SkillChipCombatEducation", "SkillChipSidearms", "SkillChipNonLethal"]),
    ];

    [Test]
    public async Task SpawnedGhostRolesReceiveTheirChips()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var random = server.System<RandomHumanoidSystem>();
            var bodies = server.System<SharedBodySystem>();

            foreach (var (settings, expected) in Expected)
            {
                var mob = random.SpawnRandomHumanoid(settings, map.GridCoords, settings);

                Assert.That(entMan.HasComponent<BodyComponent>(mob), Is.True,
                    $"{settings} did not spawn with a body at all.");

                var found = new List<string>();
                foreach (var organ in bodies.GetBodyOrgans(mob))
                {
                    if (!entMan.TryGetComponent<OrganChipContainerComponent>(organ.Id, out var box) ||
                        box.Container is not { } container)
                    {
                        continue;
                    }

                    found.AddRange(container.ContainedEntities
                        .Select(e => entMan.GetComponent<MetaDataComponent>(e).EntityPrototype?.ID)
                        .Where(id => id != null)
                        .Cast<string>());
                }

                Assert.That(found, Is.EquivalentTo(expected),
                    $"{settings} spawned through RandomHumanoidSystem did not end up with its " +
                    "chips. A chip that cannot find a brain is refused and deleted, so check that " +
                    "OrganChipsOnSpawnSystem still runs after SharedBodySystem builds the body.");

                entMan.DeleteEntity(mob);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ChipsSurviveAPlayerTakingTheRoleOver()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;

        EntityUid mob = default;

        await server.WaitAssertion(() =>
        {
            mob = server.System<RandomHumanoidSystem>()
                .SpawnRandomHumanoid("Mercenary", map.GridCoords, "merc");
        });

        await server.WaitPost(() =>
        {
            var minds = server.System<SharedMindSystem>();
            var mind = minds.CreateMind(null);
            minds.TransferTo(mind, mob, mind: mind.Comp);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var knowledge = server.System<SharedKnowledgeSystem>();
            var store = knowledge.GetContainer(mob);
            Assert.That(store, Is.Not.Null);
            Assert.That(store!.Value.Comp.ProfileApplied, Is.True,
                "Taking the role over should have applied the species baseline.");

            var rifle = knowledge.GetKnowledge(store.Value, "KnowledgeWeaponsRifle");
            Assert.That(rifle, Is.Not.Null,
                "The chip bonus did not survive the profile rebuild on takeover.");
            Assert.That(rifle!.Value.Comp.NetLevel, Is.EqualTo(50),
                "The chip is installed but its bonus was not reapplied after the rebuild.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AProfileAppliedAfterSpawnDoesNotWipeChipBonuses()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid mob = default;

        await server.WaitAssertion(() =>
        {
            mob = server.System<RandomHumanoidSystem>()
                .SpawnRandomHumanoid("Mercenary", map.GridCoords, "merc");
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var knowledge = server.System<SharedKnowledgeSystem>();
            var before = knowledge.GetKnowledge(knowledge.GetContainer(mob)!.Value, "KnowledgeWeaponsRifle");
            Assert.That(before?.Comp.NetLevel, Is.EqualTo(50), "The chip did not install on spawn.");

            server.System<CharacterProfileSpawnSystem>()
                .ApplySkillsForTest(mob, new HumanoidCharacterProfile());

        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var knowledge = server.System<SharedKnowledgeSystem>();

            var after = knowledge.GetKnowledge(knowledge.GetContainer(mob)!.Value, "KnowledgeWeaponsRifle");
            Assert.That(after, Is.Not.Null,
                "Applying a character profile wiped the chip bonus entirely.");
            Assert.That(after!.Value.Comp.NetLevel, Is.EqualTo(50),
                "The chip is still installed but its bonus was not reapplied after the profile " +
                "rebuilt the store, so the player only keeps what they bought in the editor.");
        });

        await pair.CleanReturnAsync();
    }
}
