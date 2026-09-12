// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Goobstation.Common.Cloning;
using Content.Goobstation.Common.Grab;
using Content.Goobstation.Shared.GrabIntent;
using Content.Shared._Pirate.Body.Chips;
using Content.Shared._Pirate.Knowledge;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Knowledge;

[TestFixture]
public sealed class SkillChipContainerIntegrationTest
{
    private const string Skill = "MeleeKnowledge";

    [TestPrototypes]
    private const string TestPrototypes = @"
- type: entity
  parent: BaseSkillChipMRAM
  id: PirateContainerChipA
  components:
  - type: KnowledgeGrantOnWear
    skills:
      MeleeKnowledge: 10

- type: entity
  parent: BaseSkillChipMRAM
  id: PirateContainerChipB
  components:
  - type: KnowledgeGrantOnWear
    skills:
      MeleeKnowledge: 20

- type: entity
  parent: BaseSkillChipMRAM
  id: PirateContainerChipC
  components:
  - type: KnowledgeGrantOnWear
    skills:
      ShootingKnowledge: 5

- type: entity
  parent: BaseSkillChipMRAM
  id: PirateContainerChipFused
  components:
  - type: OrganChip
    canRemove: false

- type: entity
  parent: BaseSkillChipMRAM
  id: PirateContainerChipNoSelfRemove
  components:
  - type: OrganChip
    canSelfRemove: false

- type: entity
  parent: BaseSkillChipMRAM
  id: PirateContainerChipD
  components:
  - type: KnowledgeGrantOnWear
    skills:
      ThrowingKnowledge: 5
";

    [Test]
    public async Task BrainsGetAThreeSlotChipContainer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            foreach (var brainProto in new[] { "OrganHumanBrain", "OrganDionaBrain", "PositronicBrain" })
            {
                var brain = entMan.SpawnEntity(brainProto, MapCoordinates.Nullspace);
                Assert.That(entMan.TryGetComponent<OrganChipContainerComponent>(brain, out var container), Is.True,
                    $"{brainProto} did not receive a chip container.");
                Assert.That(container!.Limit, Is.EqualTo(3));
                Assert.That(container.Container, Is.Not.Null);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonBrainOrgansRejectBrainChips()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var chips = server.System<OrganChipSystem>();
        var containers = server.System<SharedContainerSystem>();

        await server.WaitAssertion(() =>
        {
            var heart = entMan.SpawnEntity("OrganHumanHeart", MapCoordinates.Nullspace);
            var container = entMan.EnsureComponent<OrganChipContainerComponent>(heart);
            var chip = entMan.SpawnEntity("PirateContainerChipA", MapCoordinates.Nullspace);

            Assert.That(chips.CanInsertChip((heart, container), chip, out _), Is.False,
                "A heart is not a brain and must reject brain chips.");
            Assert.That(containers.Insert(chip, container.Container!), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FourthChipAndDuplicatesAreRejected()
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

            Assert.That(chips.InstallChip(human, "PirateContainerChipA"), Is.True);
            Assert.That(chips.InstallChip(human, "PirateContainerChipB"), Is.True);
            Assert.That(knowledge.GetKnowledge(store, Skill)!.Value.Comp.TemporaryLevel, Is.EqualTo(30));

            Assert.That(chips.InstallChip(human, "PirateContainerChipA"), Is.False,
                "A duplicate prototype must be rejected.");

            Assert.That(chips.InstallChip(human, "PirateContainerChipC"), Is.True);
            Assert.That(chips.InstallChip(human, "PirateContainerChipD"), Is.False,
                "A fourth chip must not fit.");

            var container = entMan.GetComponent<OrganChipContainerComponent>(store.Owner).Container!;
            Assert.That(container.Count, Is.EqualTo(3));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BrainRemovalDisablesChipsAndReinsertionEnablesThemOnce()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var body = server.System<SharedBodySystem>();
        var knowledge = server.System<SharedKnowledgeSystem>();
        var chips = server.System<OrganChipSystem>();

        await server.WaitAssertion(() =>
        {
            var human = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var store = knowledge.EnsureKnowledgeContainer(human);
            var brain = store.Owner;

            Assert.That(chips.InstallChip(human, "PirateContainerChipB"), Is.True);
            Assert.That(knowledge.GetKnowledge(store, Skill)!.Value.Comp.TemporaryLevel, Is.EqualTo(20));

            var slot = entMan.GetComponent<Content.Shared.Body.Organ.OrganComponent>(brain).SlotId;
            Assert.That(body.RemoveOrgan(brain), Is.True);
            Assert.That(knowledge.GetKnowledge(store, Skill)!.Value.Comp.TemporaryLevel, Is.Zero,
                "A brain outside a body must not keep feeding the old body's skills.");

            var head = body.GetBodyChildrenOfType(human, Content.Shared.Body.Part.BodyPartType.Head).FirstOrDefault();
            Assert.That(body.InsertOrgan(head.Id, brain, slot), Is.True);
            Assert.That(knowledge.GetKnowledge(store, Skill)!.Value.Comp.TemporaryLevel, Is.EqualTo(20),
                "Reinserting the brain must reapply the bonus exactly once.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BrainTransplantMovesTheBonusToTheNewHolder()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var body = server.System<SharedBodySystem>();
        var knowledge = server.System<SharedKnowledgeSystem>();
        var chips = server.System<OrganChipSystem>();

        await server.WaitAssertion(() =>
        {
            var donor = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var recipient = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);

            var donorStore = knowledge.EnsureKnowledgeContainer(donor);
            var donorBrain = donorStore.Owner;
            Assert.That(chips.InstallChip(donor, "PirateContainerChipB"), Is.True);

            var recipientBrain = knowledge.EnsureKnowledgeContainer(recipient).Owner;
            var slot = entMan.GetComponent<Content.Shared.Body.Organ.OrganComponent>(recipientBrain).SlotId;
            var head = body.GetBodyChildrenOfType(recipient, Content.Shared.Body.Part.BodyPartType.Head)
                .FirstOrDefault();

            Assert.That(body.RemoveOrgan(recipientBrain), Is.True);
            Assert.That(body.RemoveOrgan(donorBrain), Is.True);
            Assert.That(body.InsertOrgan(head.Id, donorBrain, slot), Is.True);

            var transplanted = knowledge.GetContainer(recipient);
            Assert.That(transplanted, Is.Not.Null);
            Assert.That(transplanted!.Value.Owner, Is.EqualTo(donorBrain));
            Assert.That(knowledge.GetKnowledge(transplanted.Value, Skill)!.Value.Comp.TemporaryLevel,
                Is.EqualTo(20), "The chip must follow the brain into its new body.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AHardGrabOnOneMobDoesNotAuthoriseOperatingOnAnother()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var knowledge = server.System<SharedKnowledgeSystem>();
        var chips = server.System<OrganChipSystem>();
        var pulling = server.System<PullingSystem>();

        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var surgeon = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var grabbed = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var bystander = entMan.SpawnEntity("MobHuman", map.GridCoords);

            var bystanderBrain = knowledge.EnsureKnowledgeContainer(bystander).Owner;
            var grabbedBrain = knowledge.EnsureKnowledgeContainer(grabbed).Owner;

            Assert.That(chips.HasOperatingAuthority(grabbedBrain, surgeon, grabbed), Is.False);
            Assert.That(chips.HasOperatingAuthority(bystanderBrain, surgeon, bystander), Is.False);

            Assert.That(pulling.TryStartPull(surgeon, grabbed, force: true), Is.True,
                "Could not set up the pull.");
            // Set the stage directly; grab escalation is outside this test.
            entMan.EnsureComponent<GrabIntentComponent>(surgeon).GrabStage = GrabStage.Hard;

            Assert.That(chips.HasOperatingAuthority(grabbedBrain, surgeon, grabbed), Is.True,
                "A hard grab must authorise operating on the person actually grabbed.");
            Assert.That(chips.HasOperatingAuthority(bystanderBrain, surgeon, bystander), Is.False,
                "A hard grab on one mob must never authorise operating on a different mob.");

            pulling.TryStopPull(grabbed, entMan.GetComponent<PullableComponent>(grabbed));
            Assert.That(chips.HasOperatingAuthority(grabbedBrain, surgeon, grabbed), Is.False,
                "Releasing the grab must withdraw operating authority.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OperatingOnYourselfNeedsNoGrabButStillNeedsReach()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var knowledge = server.System<SharedKnowledgeSystem>();
        var chips = server.System<OrganChipSystem>();
        var xform = server.System<SharedTransformSystem>();

        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var brain = knowledge.EnsureKnowledgeContainer(human).Owner;

            Assert.That(chips.HasOperatingAuthority(brain, human, human), Is.True);

            var loose = entMan.SpawnEntity("OrganHumanBrain", map.GridCoords);
            Assert.That(chips.HasOperatingAuthority(loose, human, null), Is.True);

            xform.SetWorldPosition(loose, xform.GetWorldPosition(human) + new Vector2(20f, 0f));
            Assert.That(chips.HasOperatingAuthority(loose, human, null), Is.False,
                "An operation site well out of reach must not be operable.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemovalPermissionsAreEnforced()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var chips = server.System<OrganChipSystem>();

        await server.WaitAssertion(() =>
        {
            var human = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var other = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var fused = entMan.SpawnEntity("PirateContainerChipFused", MapCoordinates.Nullspace);
            var noSelf = entMan.SpawnEntity("PirateContainerChipNoSelfRemove", MapCoordinates.Nullspace);

            Assert.That(chips.CanRemoveChip(fused, human, human), Is.False, "canRemove: false must block anyone.");
            Assert.That(chips.CanRemoveChip(fused, other, human), Is.False);

            Assert.That(chips.CanRemoveChip(noSelf, human, human), Is.False,
                "canSelfRemove: false must block the wearer.");
            Assert.That(chips.CanRemoveChip(noSelf, other, human), Is.True,
                "canSelfRemove: false must still allow somebody else to pull it.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StoreTransferKeepsCompetenceAndNamedPackagesOnCollidingSkills()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var knowledge = server.System<SharedKnowledgeSystem>();

        await server.WaitAssertion(() =>
        {
            var donor = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var recipient = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);

            var donorStore = knowledge.EnsureKnowledgeContainer(donor);
            var recipientStore = knowledge.EnsureKnowledgeContainer(recipient);

            knowledge.EnsureKnowledge(donorStore, Skill, 10, popup: false);
            knowledge.EnsureKnowledge(recipientStore, Skill, 10, popup: false);

            knowledge.GrantCompetency(donor, new Dictionary<EntProtoId, int> { [Skill] = 50 });
            knowledge.SetNamedTemporaryModifier(donorStore, Skill, "TestPositive", 40);
            knowledge.SetNamedTemporaryModifier(donorStore, Skill, "TestNegative", -10);

            knowledge.TransferKnowledge(donorStore.Owner, recipient);

            var merged = knowledge.GetContainer(recipient)!.Value;
            Assert.That(knowledge.GetKnowledge(merged, Skill)!.Value.Comp.TemporaryLevel, Is.EqualTo(30),
                "Named packages must survive a merge onto a colliding skill, exactly once.");

            var species = server.ProtoMan.Index<SpeciesPrototype>("Human");
            knowledge.ApplyProfile(recipient, species.Knowledge, new KnowledgeProfile());
            knowledge.ReplayCompetency(recipient);

            Assert.That(knowledge.GetKnowledge(merged, Skill)!.Value.Comp.LearnedLevel,
                Is.GreaterThanOrEqualTo(50),
                "Competence metadata was lost in the merge, so it could not be replayed.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClonePipelineKeepsCompetenceButNotPhysicalChips()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var knowledge = server.System<SharedKnowledgeSystem>();
        var chips = server.System<OrganChipSystem>();

        await server.WaitAssertion(() =>
        {
            var original = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var clone = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);

            var originalStore = knowledge.EnsureKnowledgeContainer(original);
            knowledge.GrantCompetency(original, new Dictionary<EntProtoId, int> { [Skill] = 50 });
            Assert.That(chips.InstallChip(original, "PirateContainerChipB"), Is.True);
            Assert.That(knowledge.GetKnowledge(originalStore, Skill)!.Value.Comp.TemporaryLevel, Is.EqualTo(20));

            var ev = new TransferredToCloneEvent(clone);
            entMan.EventBus.RaiseLocalEvent(originalStore.Owner, ref ev);

            var cloneStore = knowledge.GetContainer(clone)!.Value;
            Assert.That(knowledge.GetKnowledge(cloneStore, Skill)!.Value.Comp.TemporaryLevel, Is.Zero,
                "A chipless clone must not inherit the original's physical chip bonus.");
            Assert.That(knowledge.GetKnowledge(cloneStore, Skill)!.Value.Comp.LearnedLevel,
                Is.GreaterThanOrEqualTo(50),
                "The clone must keep permanent competence, which is not physically carried.");

            Assert.That(entMan.HasComponent<KnowledgeCompetencyComponent>(cloneStore.Owner), Is.True,
                "The clone's store must own the competence record so it can be replayed later.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CompetencyIsReplayedAfterAProfileRebuild()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var knowledge = server.System<SharedKnowledgeSystem>();
        var chips = server.System<OrganChipSystem>();

        await server.WaitAssertion(() =>
        {
            var human = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);

            knowledge.GrantCompetency(human, new System.Collections.Generic.Dictionary<EntProtoId, int>
            {
                [Skill] = 50,
            });
            Assert.That(chips.InstallChip(human, "PirateContainerChipB"), Is.True);

            var species = server.ProtoMan.Index<Content.Shared.Humanoid.Prototypes.SpeciesPrototype>("Human");
            knowledge.ApplyProfile(human, species.Knowledge, new KnowledgeProfile());
            knowledge.ReplayCompetency(human);
            chips.ReconcileInstalledChipModifiers(human);

            var store = knowledge.GetContainer(human)!.Value;
            var skill = knowledge.GetKnowledge(store, Skill)!.Value;
            Assert.That(skill.Comp.LearnedLevel, Is.GreaterThanOrEqualTo(50),
                "Permanent competence was erased by the profile rebuild.");
            Assert.That(skill.Comp.TemporaryLevel, Is.EqualTo(20),
                "The installed chip was not reconciled after the profile rebuild.");

            knowledge.GrantCompetency(human, new System.Collections.Generic.Dictionary<EntProtoId, int>
            {
                [Skill] = 25,
            });
            Assert.That(knowledge.GetKnowledge(store, Skill)!.Value.Comp.LearnedLevel,
                Is.GreaterThanOrEqualTo(50), "A weaker package must never lower a stronger skill.");
        });

        await pair.CleanReturnAsync();
    }
}
