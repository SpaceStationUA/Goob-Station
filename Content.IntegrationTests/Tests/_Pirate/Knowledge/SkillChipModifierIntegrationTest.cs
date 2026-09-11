// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Pirate.Body.Chips;
using Content.Shared._Pirate.Knowledge;
using Content.Shared.Body.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests._Pirate.Knowledge;

[TestFixture]
public sealed class SkillChipModifierIntegrationTest
{
    private const string Skill = "MeleeKnowledge";

    [TestPrototypes]
    private const string TestPrototypes = @"
- type: entity
  parent: BaseSkillChipMRAM
  id: PirateTestChipPositive
  components:
  - type: KnowledgeGrantOnWear
    skills:
      MeleeKnowledge: 28

- type: entity
  parent: BaseSkillChipMRAM
  id: PirateTestChipPositiveOther
  components:
  - type: KnowledgeGrantOnWear
    skills:
      MeleeKnowledge: 15

- type: entity
  parent: BaseSkillChipMRAM
  id: PirateTestChipNegativeSmall
  components:
  - type: KnowledgeGrantOnWear
    skills:
      MeleeKnowledge: -15

- type: entity
  parent: BaseSkillChipMRAM
  id: PirateTestChipNegativeMedium
  components:
  - type: KnowledgeGrantOnWear
    skills:
      MeleeKnowledge: -25

- type: entity
  parent: BaseSkillChipMRAM
  id: PirateTestChipNegativeHuge
  components:
  - type: KnowledgeGrantOnWear
    skills:
      MeleeKnowledge: -1000

- type: entity
  parent: BaseSkillChipMRAM
  id: PirateTestChipFirstAid
  components:
  - type: KnowledgeGrantOnWear
    skills:
      FirstAidKnowledge: 28

- type: entity
  parent: BaseSkillChipMRAM
  id: PirateTestChipUnremovable
  components:
  - type: OrganChip
    canRemove: false
  - type: KnowledgeGrantOnWear
    skills:
      MeleeKnowledge: 10

- type: entity
  parent: BaseSkillChipMRAM
  id: PirateTestChipNoSelfRemove
  components:
  - type: OrganChip
    canSelfRemove: false
  - type: KnowledgeGrantOnWear
    skills:
      MeleeKnowledge: 10
";

    [Test]
    public async Task PositiveAndNegativeChipsApplyAndRemoveExactly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var knowledge = server.System<SharedKnowledgeSystem>();
        var chips = server.System<OrganChipSystem>();

        await server.WaitAssertion(() =>
        {
            var human = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var store = knowledge.EnsureKnowledgeContainer(human);
            knowledge.EnsureKnowledge(store, Skill, 50, popup: false);

            foreach (var (proto, offset) in new[]
                     {
                         ("PirateTestChipPositive", 28),
                         ("PirateTestChipNegativeSmall", -15),
                         ("PirateTestChipNegativeMedium", -25),
                         ("PirateTestChipNegativeHuge", -1000),
                     })
            {
                Assert.That(chips.InstallChip(human, proto), Is.True, $"Could not install {proto}.");

                var knowledgeEnt = knowledge.GetKnowledge(store, Skill)!.Value;
                Assert.That(knowledgeEnt.Comp.TemporaryLevel, Is.EqualTo(offset),
                    $"{proto} did not apply exactly {offset}.");

                RemoveChip(server, human, proto);

                knowledgeEnt = knowledge.GetKnowledge(store, Skill)!.Value;
                Assert.That(knowledgeEnt.Comp.TemporaryLevel, Is.Zero, $"{proto} left a residue behind.");
                Assert.That(knowledgeEnt.Comp.LearnedLevel, Is.EqualTo(50), $"{proto} damaged the learned level.");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TwoChipsStackAndUnstackInEitherOrder()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var knowledge = server.System<SharedKnowledgeSystem>();
        var chips = server.System<OrganChipSystem>();

        await server.WaitAssertion(() =>
        {
            foreach (var reverse in new[] { false, true })
            {
                var human = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
                var store = knowledge.EnsureKnowledgeContainer(human);
                knowledge.EnsureKnowledge(store, Skill, 20, popup: false);

                var order = new[] { "PirateTestChipPositive", "PirateTestChipNegativeMedium" };
                foreach (var proto in order)
                    Assert.That(chips.InstallChip(human, proto), Is.True);

                Assert.That(knowledge.GetKnowledge(store, Skill)!.Value.Comp.TemporaryLevel, Is.EqualTo(3),
                    "A positive and a negative chip must sum, not cancel each other out.");

                foreach (var proto in reverse ? order.Reverse() : order)
                    RemoveChip(server, human, proto);

                var final = knowledge.GetKnowledge(store, Skill)!.Value;
                Assert.That(final.Comp.TemporaryLevel, Is.Zero,
                    $"Removal order {(reverse ? "reversed" : "forward")} left a residue.");
                Assert.That(final.Comp.LearnedLevel, Is.EqualTo(20));
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ReapplyingTheSameChipIsIdempotent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var knowledge = server.System<SharedKnowledgeSystem>();
        var chips = server.System<OrganChipSystem>();

        await server.WaitAssertion(() =>
        {
            var human = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var store = knowledge.EnsureKnowledgeContainer(human);

            Assert.That(chips.InstallChip(human, "PirateTestChipPositive"), Is.True);
            Assert.That(knowledge.GetKnowledge(store, Skill)!.Value.Comp.TemporaryLevel, Is.EqualTo(28));

            for (var i = 0; i < 3; i++)
                chips.ReconcileInstalledChipModifiers(human);

            Assert.That(knowledge.GetKnowledge(store, Skill)!.Value.Comp.TemporaryLevel, Is.EqualTo(28),
                "Reconciliation doubled the chip's bonus.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemovalPreservesTheEmployerBonus()
    {
        const string employerId = "IdrisIncorporated";
        const string employerSkill = "FirstAidKnowledge";

        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var knowledge = server.System<SharedKnowledgeSystem>();
        var chips = server.System<OrganChipSystem>();

        await server.WaitAssertion(() =>
        {
            var employer =
                server.ProtoMan.Index<Content.Shared._Pirate.Contractors.Prototypes.EmployerPrototype>(employerId);
            Assert.That(employer.KnowledgeBonuses.ContainsKey(employerSkill), Is.True,
                $"{employerId} no longer grants {employerSkill}; pick another employer for this test.");

            var human = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var store = knowledge.EnsureKnowledgeContainer(human);
            knowledge.EnsureKnowledge(store, employerSkill, 10, popup: false);

            knowledge.ApplyEmployerBonuses(human, employerId);
            var withEmployer = knowledge.GetKnowledge(store, employerSkill)!.Value.Comp.TemporaryLevel;
            Assert.That(withEmployer, Is.Not.Zero, "The employer bonus did not apply at all.");

            Assert.That(chips.InstallChip(human, "PirateTestChipFirstAid"), Is.True);
            Assert.That(knowledge.GetKnowledge(store, employerSkill)!.Value.Comp.TemporaryLevel,
                Is.EqualTo(withEmployer + 28), "The chip did not stack on top of the employer bonus.");

            RemoveChip(server, human, "PirateTestChipFirstAid");
            var final = knowledge.GetKnowledge(store, employerSkill)!.Value;
            Assert.That(final.Comp.TemporaryLevel, Is.EqualTo(withEmployer),
                "Removing the chip ate the employer bonus.");
            Assert.That(final.Comp.LearnedLevel, Is.EqualTo(10));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeletedChipIsCleanedDuringReconciliation()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var knowledge = server.System<SharedKnowledgeSystem>();
        var chips = server.System<OrganChipSystem>();
        var containers = server.System<SharedContainerSystem>();

        await server.WaitAssertion(() =>
        {
            var human = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var store = knowledge.EnsureKnowledgeContainer(human);
            Assert.That(chips.InstallChip(human, "PirateTestChipPositive"), Is.True);

            var brain = store.Owner;
            var container = entMan.GetComponent<OrganChipContainerComponent>(brain).Container!;
            var chip = container.ContainedEntities[0];

            containers.Remove(chip, container, force: true);
            entMan.DeleteEntity(chip);

            chips.ReconcileInstalledChipModifiers(human);

            Assert.That(knowledge.GetKnowledge(store, Skill)!.Value.Comp.TemporaryLevel, Is.Zero,
                "A deleted chip left a stale bonus behind.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NamedPackagesApplyAndRemoveExactly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var knowledge = server.System<SharedKnowledgeSystem>();

        await server.WaitAssertion(() =>
        {
            var human = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var store = knowledge.EnsureKnowledgeContainer(human);
            knowledge.EnsureKnowledge(store, Skill, 30, popup: false);

            knowledge.SetNamedTemporaryModifier(store, Skill, "TestPackage", 40);
            knowledge.SetNamedTemporaryModifier(store, Skill, "OtherPackage", -10);
            Assert.That(knowledge.GetKnowledge(store, Skill)!.Value.Comp.TemporaryLevel, Is.EqualTo(30));

            knowledge.SetNamedTemporaryModifier(store, Skill, "TestPackage", 40);
            Assert.That(knowledge.GetKnowledge(store, Skill)!.Value.Comp.TemporaryLevel, Is.EqualTo(30));

            knowledge.RemoveNamedTemporaryModifiers(store, "TestPackage");
            Assert.That(knowledge.GetKnowledge(store, Skill)!.Value.Comp.TemporaryLevel, Is.EqualTo(-10));
            Assert.That(knowledge.GetKnowledge(store, Skill)!.Value.Comp.LearnedLevel, Is.EqualTo(30));
        });

        await pair.CleanReturnAsync();
    }

    internal static void RemoveChip(RobustIntegrationTest.ServerIntegrationInstance server, EntityUid mob, string prototype)
    {
        var entMan = server.EntMan;
        var body = server.System<SharedBodySystem>();
        var containers = server.System<SharedContainerSystem>();

        foreach (var brain in body.GetBodyOrganEntityComps<OrganChipContainerComponent>(mob))
        {
            if (brain.Comp1.Container is not { } container)
                continue;

            foreach (var chip in container.ContainedEntities.ToArray())
            {
                if (entMan.GetComponent<MetaDataComponent>(chip).EntityPrototype?.ID != prototype)
                    continue;

                Assert.That(containers.Remove(chip, container), Is.True, $"Could not remove {prototype}.");
                return;
            }
        }

        Assert.Fail($"{prototype} was not installed.");
    }
}
