// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Goobstation.Common.Cloning;
using Content.Server._Pirate.Knowledge;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared._Pirate.CCVars;
using Content.Shared._Pirate.Knowledge;
using Content.Shared.Administration;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Body.Systems;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Polymorph;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Knowledge;

[TestFixture]
public sealed class KnowledgeLifecycleIntegrationTest
{
    [TestPrototypes]
    private const string TestPrototypes = @"
- type: knowledgeCatalog
  id: PirateKnowledgeLifecycleCatalog
  entries:
  - PirateKnowledgeConflictSkill

- type: entity
  id: PirateKnowledgeLifecycleHolder
  components:
  - type: KnowledgeHolder

- type: entity
  id: PirateKnowledgeMapGrantHolder
  components:
  - type: KnowledgeHolder
  - type: KnowledgeGrant
    skills:
      FabricationKnowledge: 25

- type: entity
  parent: BasePirateKnowledge
  id: PirateKnowledgeConflictSkill
  components:
  - type: KnowledgeConflict
    conflicts:
    - KnowledgeWeaponsEnergy
";

    [Test]
    public async Task BrainAndBorgPhysicalStoresFollowInsertionAndRemoval()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var body = server.System<SharedBodySystem>();
        var containers = server.System<SharedContainerSystem>();
        var knowledge = server.System<SharedKnowledgeSystem>();

        await server.WaitAssertion(() =>
        {
            var human = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            Assert.That(entMan.HasComponent<KnowledgeHolderComponent>(human));

            var humanStore = knowledge.GetContainer(human);
            Assert.That(humanStore, Is.Not.Null, "A human must use its brain as its knowledge store.");
            var brain = humanStore!.Value.Owner;
            Assert.That(brain, Is.Not.EqualTo(human));
            Assert.That(humanStore.Value.Comp.Holder, Is.EqualTo(human));
            Assert.That(containers.TryGetContainingContainer(brain, out var brainContainer), Is.True);

            Assert.That(body.RemoveOrgan(brain), Is.True);
            Assert.That(humanStore.Value.Comp.Holder, Is.Null);
            Assert.That(knowledge.GetContainer(human), Is.Null);

            Assert.That(containers.Insert(brain, brainContainer!), Is.True);
            Assert.That(humanStore.Value.Comp.Holder, Is.EqualTo(human));
            Assert.That(knowledge.GetContainer(human)?.Owner, Is.EqualTo(brain));

            entMan.DeleteEntity(human);

            var borg = entMan.SpawnEntity("PlayerBorgGeneric", MapCoordinates.Nullspace);
            var mmi = entMan.SpawnEntity("MMI", MapCoordinates.Nullspace);
            var borgBrain = entMan.SpawnEntity("OrganHumanBrain", MapCoordinates.Nullspace);
            var mmiBrainSlot = containers.GetContainer(mmi, "brain_slot");
            var borgBrainSlot = containers.GetContainer(borg, "borg_brain");

            Assert.That(containers.Insert(borgBrain, mmiBrainSlot), Is.True);
            Assert.That(containers.Insert(mmi, borgBrainSlot), Is.True);
            Assert.That(knowledge.GetContainer(borg)?.Owner, Is.EqualTo(borgBrain),
                "A borg must relay knowledge ownership through its inserted MMI to the physical brain.");
            Assert.That(entMan.GetComponent<KnowledgeContainerComponent>(borgBrain).Holder, Is.EqualTo(borg));

            Assert.That(containers.Remove(mmi, borgBrainSlot), Is.True);
            Assert.That(knowledge.GetContainer(borg), Is.Null);
            Assert.That(entMan.GetComponent<KnowledgeContainerComponent>(borgBrain).Holder, Is.Null);

            entMan.DeleteEntity(borg);
            entMan.DeleteEntity(mmi);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task KnowledgeLifecycleGrantsConflictsRemovesAndMergesWithoutLoss()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var knowledge = server.System<SharedKnowledgeSystem>();

        EntityUid grantedHolder = default;
        await server.WaitPost(() =>
        {
            Assert.That(knowledge.SkillsEnabled, Is.True);

            grantedHolder = entMan.SpawnEntity("PirateKnowledgeMapGrantHolder", MapCoordinates.Nullspace);
            var granted = knowledge.GetKnowledge(grantedHolder, "FabricationKnowledge");
            Assert.That(granted, Is.Not.Null);
            Assert.That(granted!.Value.Comp.LearnedLevel, Is.EqualTo(25));

            var source = entMan.SpawnEntity("PirateKnowledgeLifecycleHolder", MapCoordinates.Nullspace);
            var destination = entMan.SpawnEntity("PirateKnowledgeLifecycleHolder", MapCoordinates.Nullspace);
            var sourceStore = knowledge.EnsureKnowledgeContainer(source);
            var destinationStore = knowledge.EnsureKnowledgeContainer(destination);

            var sourceSkill = knowledge.EnsureKnowledge(sourceStore, "FabricationKnowledge", 25, popup: false);
            var destinationSkill = knowledge.EnsureKnowledge(destinationStore, "FabricationKnowledge", 50, popup: false);
            Assert.That(sourceSkill, Is.Not.Null);
            Assert.That(destinationSkill, Is.Not.Null);
            sourceSkill!.Value.Comp.Experience = 11;
            destinationSkill!.Value.Comp.Experience = 4;

            Assert.That(knowledge.EnsureKnowledge(sourceStore, "KnowledgeWeaponsEnergy", 25, popup: false), Is.Not.Null);
            Assert.That(knowledge.EnsureKnowledge(sourceStore, "PirateKnowledgeConflictSkill", 0, popup: false), Is.Not.Null);
            Assert.That(knowledge.GetKnowledge(sourceStore, "KnowledgeWeaponsEnergy"), Is.Null,
                "Learning a conflicting skill must forcibly remove the existing one.");

            var shooting = knowledge.EnsureKnowledge(sourceStore, "ShootingKnowledge", 25, popup: false);
            Assert.That(shooting, Is.Not.Null);
            Assert.That(knowledge.GetKnowledgeWith<AimSpeedKnowledgeComponent>(source), Has.Count.EqualTo(1));
            Assert.That(knowledge.RemoveKnowledge(source, "ShootingKnowledge"), Is.Null,
                "Unremoveable knowledge must reject normal removal.");
            Assert.That(knowledge.RemoveKnowledge(source, "ShootingKnowledge", force: true), Is.EqualTo(source));
            Assert.That(knowledge.GetKnowledge(source, "ShootingKnowledge"), Is.Null);

            knowledge.TransferKnowledge(source, destination);
            var merged = knowledge.GetKnowledge(destination, "FabricationKnowledge");
            Assert.That(merged, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(merged!.Value.Comp.LearnedLevel, Is.EqualTo(50));
                Assert.That(merged.Value.Comp.Experience, Is.EqualTo(11));
                Assert.That(knowledge.GetAllKnowledge(source), Is.Empty);
                Assert.That(knowledge.GetKnowledge(destination, "PirateKnowledgeConflictSkill"), Is.Not.Null);
            });

            entMan.DeleteEntity(source);
            entMan.DeleteEntity(destination);
        });

        await pair.RunTicksSync(1);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<KnowledgeGrantComponent>(grantedHolder), Is.False,
                "A map-init grant must remove itself after granting exactly once.");
            entMan.DeleteEntity(grantedHolder);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ProfilesExperienceCooldownAndCapsAreEnforced()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var knowledge = server.System<SharedKnowledgeSystem>();

        await server.WaitAssertion(() =>
        {
            var invalid = new KnowledgeProfile(new Dictionary<EntProtoId, int>
            {
                ["MeleeKnowledge"] = 2,
                ["FirstAidKnowledge"] = 3,
                ["ChemistryKnowledge"] = 2,
                ["FabricationKnowledge"] = 1,
                ["KnowledgeWeaponsEnergy"] = 1,
                ["DoorsKnowledge"] = 3,
                ["CookingKnowledge"] = -1,
            });

            knowledge.EnsureProfileValid("Human", ref invalid);
            Assert.That(invalid.Mastery, Is.EquivalentTo(new Dictionary<EntProtoId, int>
            {
                ["FirstAidKnowledge"] = 3,
                ["ChemistryKnowledge"] = 2,
                ["FabricationKnowledge"] = 1,
            }), "Invalid, unavailable, over-racial, and over-budget profile entries must be removed deterministically.");
            Assert.That(knowledge.ProfileCost(invalid), Is.EqualTo(10));

            var holder = entMan.SpawnEntity("PirateKnowledgeLifecycleHolder", MapCoordinates.Nullspace);
            knowledge.ApplyProfile(holder, "Human", invalid);
            Assert.That(SharedKnowledgeSystem.GetMastery(
                knowledge.GetKnowledge(holder, "FirstAidKnowledge")!.Value.Comp.NetLevel), Is.EqualTo(3));
            Assert.That(SharedKnowledgeSystem.GetMastery(
                knowledge.GetKnowledge(holder, "ChemistryKnowledge")!.Value.Comp.NetLevel), Is.EqualTo(2));
            Assert.That(SharedKnowledgeSystem.GetMastery(
                knowledge.GetKnowledge(holder, "FabricationKnowledge")!.Value.Comp.NetLevel), Is.EqualTo(1));
            Assert.That(SharedKnowledgeSystem.GetMastery(
                knowledge.GetKnowledge(holder, "DoorsKnowledge")!.Value.Comp.NetLevel), Is.EqualTo(1));

            var store = knowledge.GetContainer(holder)!.Value;
            var skill = knowledge.GetKnowledge(store, "FabricationKnowledge")!.Value;
            skill.Comp.LearnedLevel = 24;
            skill.Comp.Experience = skill.Comp.ExperienceCost - 1;
            skill.Comp.BonusExperience = 0;
            skill.Comp.TimeToNextExperience = TimeSpan.Zero;

            knowledge.AddExperience(skill, holder, 1, levelCap: 25);
            Assert.That(skill.Comp.LearnedLevel, Is.EqualTo(25), "A level roll must clamp to its event cap.");
            Assert.That(skill.Comp.Experience, Is.Zero);
            var cooldown = skill.Comp.TimeToNextExperience;

            knowledge.AddExperience(skill, holder, 100, levelCap: 100);
            Assert.That(skill.Comp.LearnedLevel, Is.EqualTo(25), "XP inside the cooldown must be ignored.");
            Assert.That(skill.Comp.TimeToNextExperience, Is.EqualTo(cooldown));

            skill.Comp.TimeToNextExperience = TimeSpan.Zero;
            skill.Comp.BonusExperience = 1;
            knowledge.AddExperience(skill, holder, skill.Comp.ExperienceCost - 1, levelCap: 25);
            Assert.That(skill.Comp.LearnedLevel, Is.EqualTo(25), "XP at the requested cap must not accumulate or level.");
            Assert.That(skill.Comp.Experience, Is.Zero);

            Assert.That(knowledge.RaiseMastery(store, "FabricationKnowledge", 99, popup: false), Is.Not.Null);
            Assert.That(skill.Comp.LearnedLevel, Is.EqualTo(100));
            Assert.That(knowledge.GetKnowledgeLevel(holder, "FabricationKnowledge"), Is.EqualTo(100));
            Assert.That(knowledge.GetSkillMasteries(holder)["FabricationKnowledge"], Is.EqualTo(5));

            entMan.DeleteEntity(holder);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlayerSpawnAppliesSpeciesAndSavedKnowledgeToTheFinalMob()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var knowledge = server.System<SharedKnowledgeSystem>();
        var player = server.ResolveDependency<IPlayerManager>().Sessions.Single();

        await server.WaitAssertion(() =>
        {
            var holder = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var oldStore = knowledge.EnsureKnowledgeContainer(holder);
            Assert.That(knowledge.EnsureKnowledge(oldStore, "KnowledgeWeaponsEnergy", 100, popup: false), Is.Not.Null);

            var profile = new HumanoidCharacterProfile
            {
                Species = "Human",
                Knowledge = new KnowledgeProfile(new Dictionary<EntProtoId, int>
                {
                    ["FirstAidKnowledge"] = 3,
                }),
            };
            var spawned = new PlayerSpawnCompleteEvent(
                holder,
                player,
                jobId: null,
                lateJoin: false,
                silent: true,
                joinOrder: 1,
                station: holder,
                profile);

            entMan.EventBus.RaiseLocalEvent(holder, spawned, broadcast: true);

            Assert.Multiple(() =>
            {
                Assert.That(knowledge.GetKnowledge(holder, "KnowledgeWeaponsEnergy"), Is.Null,
                    "Skills present before final spawning were not cleared.");
                Assert.That(Mastery("DoorsKnowledge"), Is.EqualTo(1),
                    "The Human species baseline was not applied.");
                Assert.That(Mastery("FirstAidKnowledge"), Is.EqualTo(3),
                    "The character's saved mastery increase was not applied.");
            });

            entMan.DeleteEntity(holder);
            return;

            int Mastery(EntProtoId id)
            {
                var skill = knowledge.GetKnowledge(holder, id);
                Assert.That(skill, Is.Not.Null, $"Missing expected spawned skill {id.Id}.");
                return SharedKnowledgeSystem.GetMastery(skill!.Value.Comp.NetLevel);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CoreApisHandleDisabledSkillsProfilesTransfersAndCleanup()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var knowledge = server.System<SharedKnowledgeSystem>();
        EntityUid deletedByClear = default;

        await server.WaitAssertion(() =>
        {
            var noStore = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var unusedDestination = entMan.SpawnEntity("PirateKnowledgeLifecycleHolder", MapCoordinates.Nullspace);
            Assert.Multiple(() =>
            {
                Assert.That(knowledge.GetContainer(noStore), Is.Null);
                Assert.That(knowledge.GetAllKnowledge(noStore), Is.Null);
                Assert.That(knowledge.GetKnowledgeWith<AimSpeedKnowledgeComponent>(noStore), Is.Null);
                Assert.That(knowledge.GetKnowledgeLevel(noStore, "FabricationKnowledge"), Is.Zero);
                Assert.That(knowledge.GetSkillMasteries(noStore), Is.Empty);
                Assert.That(knowledge.RemoveKnowledge(noStore, "FabricationKnowledge"), Is.Null);
            });
            knowledge.ClearKnowledge(noStore);
            knowledge.TransferKnowledge(noStore, unusedDestination);
            Assert.That(knowledge.GetContainer(unusedDestination), Is.Null,
                "A missing source store must not create a destination store as a side effect.");

            var holder = entMan.SpawnEntity("PirateKnowledgeLifecycleHolder", MapCoordinates.Nullspace);
            Assert.That(knowledge.GetContainer(holder), Is.Null);

            var mind = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var mindComponent = entMan.EnsureComponent<MindComponent>(mind);
            var mindContainer = entMan.EnsureComponent<MindContainerComponent>(holder);
            entMan.EventBus.RaiseLocalEvent(holder,
                new MindAddedMessage((mind, mindComponent), (holder, mindContainer)));

            var store = knowledge.GetContainer(holder);
            Assert.That(store, Is.Not.Null, "Adding a mind must initialize a knowledge store.");
            Assert.That(store!.Value.Owner, Is.EqualTo(holder));
            Assert.That(knowledge.EnsureKnowledgeContainer(holder).Owner, Is.EqualTo(store.Value.Owner),
                "Ensuring an existing store must reuse it.");

            server.CfgMan.SetCVar(KnowledgeCVars.SkillsEnabled, false);
            Assert.That(knowledge.EnsureKnowledge(store.Value, "FabricationKnowledge", popup: false), Is.Null);
            server.CfgMan.SetCVar(KnowledgeCVars.SkillsEnabled, true);

            var skill = knowledge.EnsureKnowledge(store.Value, "FabricationKnowledge", -50, popup: false);
            Assert.That(skill, Is.Not.Null);
            Assert.That(skill!.Value.Comp.LearnedLevel, Is.Zero);
            var sameSkill = knowledge.EnsureKnowledge(store.Value, "FabricationKnowledge", 500, popup: false);
            Assert.Multiple(() =>
            {
                Assert.That(sameSkill?.Owner, Is.EqualTo(skill.Value.Owner));
                Assert.That(skill.Value.Comp.LearnedLevel, Is.EqualTo(100));
                Assert.That(knowledge.GetAllKnowledge(holder), Has.Count.EqualTo(1));
                Assert.That(knowledge.GetKnowledgeWith<KnowledgeComponent>(holder), Has.Count.EqualTo(1));
            });

            skill.Value.Comp.TemporaryLevel = -500;
            Assert.That(skill.Value.Comp.NetLevel, Is.Zero);
            skill.Value.Comp.TemporaryLevel = 500;
            Assert.That(skill.Value.Comp.NetLevel, Is.EqualTo(100));
            skill.Value.Comp.TemporaryLevel = 0;

            var info = knowledge.GetKnowledgeInfo(skill.Value);
            Assert.Multiple(() =>
            {
                Assert.That(info.Name, Is.Not.Empty);
                Assert.That(info.Description, Is.Not.Null);
                Assert.That(info.Level, Does.Contain("100"));
                Assert.That(info.LearnedLevel, Is.EqualTo(100));
                Assert.That(info.NetLevel, Is.EqualTo(100));
                Assert.That(info.ExperienceCost, Is.EqualTo(skill.Value.Comp.ExperienceCost));
                Assert.That(knowledge.SkillCosts("FabricationKnowledge"), Is.Not.Null);
                Assert.That(knowledge.SkillCosts("KnowledgeWeaponsEnergy"), Is.Null);
                Assert.That(knowledge.SkillCosts("PirateMissingKnowledge"), Is.Null);
                Assert.That(knowledge.SkillCost("FabricationKnowledge", -1), Is.Null);
                Assert.That(knowledge.SkillCost("FabricationKnowledge", 999), Is.Null);
                Assert.That(knowledge.SkillCost("PirateMissingKnowledge", 0), Is.Null);
                Assert.That(knowledge.ProfileCost(default), Is.Zero);
                Assert.That(SharedKnowledgeSystem.SharpCurve(50), Is.EqualTo(0.25f));
            });

            skill.Value.Comp.LearnedLevel = 0;
            skill.Value.Comp.ExperienceCost = 1;
            skill.Value.Comp.Experience = 3;
            Assert.That(knowledge.RollForLevelUp(skill.Value, holder), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(skill.Value.Comp.Experience, Is.Zero);
                Assert.That(skill.Value.Comp.LearnedLevel, Is.GreaterThan(0).And.LessThanOrEqualTo(100));
            });
            Assert.That(knowledge.RollForLevelUp(skill.Value, holder), Is.False,
                "A roll without enough experience must be rejected.");
            skill.Value.Comp.ExperienceCost = 0;
            skill.Value.Comp.Experience = 100;
            Assert.That(knowledge.RollForLevelUp(skill.Value, holder), Is.False,
                "A non-positive experience cost must not roll or divide by zero.");

            var detached = skill.Value.Owner;
            knowledge.ClearKnowledge(holder, deleteEntities: false);
            Assert.Multiple(() =>
            {
                Assert.That(knowledge.GetAllKnowledge(holder), Is.Empty);
                Assert.That(entMan.EntityExists(detached), Is.True,
                    "A transfer-oriented clear must leave detached units alive.");
            });
            entMan.DeleteEntity(detached);

            var limited = new KnowledgeProfile(new Dictionary<EntProtoId, int>
            {
                ["FirstAidKnowledge"] = 1,
                ["FabricationKnowledge"] = 1,
            });
            knowledge.ApplyProfile(store.Value, limited, points: 1);
            Assert.Multiple(() =>
            {
                Assert.That(knowledge.GetKnowledge(holder, "FabricationKnowledge"), Is.Not.Null,
                    "Profiles must consume point-limited entries in deterministic ID order.");
                Assert.That(knowledge.GetKnowledge(holder, "FirstAidKnowledge"), Is.Null);
            });

            var clone = entMan.SpawnEntity("PirateKnowledgeLifecycleHolder", MapCoordinates.Nullspace);
            var cloneEvent = new TransferredToCloneEvent(clone);
            entMan.EventBus.RaiseLocalEvent(store.Value.Owner, ref cloneEvent);
            Assert.Multiple(() =>
            {
                Assert.That(knowledge.GetKnowledge(clone, "FabricationKnowledge"), Is.Not.Null);
                Assert.That(knowledge.GetAllKnowledge(holder), Is.Empty);
            });

            var polymorph = entMan.SpawnEntity("PirateKnowledgeLifecycleHolder", MapCoordinates.Nullspace);
            var ignoredPolymorph = new PolymorphedEvent(noStore, polymorph, false);
            entMan.EventBus.RaiseLocalEvent(clone, ref ignoredPolymorph);
            Assert.That(knowledge.GetContainer(polymorph), Is.Null,
                "An event raised on an entity other than OldEntity must not transfer knowledge.");
            var appliedPolymorph = new PolymorphedEvent(clone, polymorph, false);
            entMan.EventBus.RaiseLocalEvent(clone, ref appliedPolymorph);
            Assert.That(knowledge.GetKnowledge(polymorph, "FabricationKnowledge"), Is.Not.Null);
            Assert.That(knowledge.GetAllKnowledge(clone), Is.Empty);

            var cleanup = knowledge.EnsureKnowledgeContainer(polymorph);
            var cleanupSkill = knowledge.EnsureKnowledge(cleanup, "FirstAidKnowledge", popup: false);
            Assert.That(cleanupSkill, Is.Not.Null);
            deletedByClear = cleanupSkill!.Value.Owner;
            knowledge.ClearKnowledge(polymorph);
            Assert.That(knowledge.GetAllKnowledge(polymorph), Is.Empty);

            entMan.DeleteEntity(polymorph);
            entMan.DeleteEntity(clone);
            entMan.DeleteEntity(holder);
            entMan.DeleteEntity(mind);
            entMan.DeleteEntity(unusedDestination);
            entMan.DeleteEntity(noStore);
        });

        await pair.RunTicksSync(1);
        await server.WaitAssertion(() => Assert.That(entMan.EntityExists(deletedByClear), Is.False,
            "A normal clear must queue all knowledge entities for deletion."));

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UntrustedKnowledgeProfileOnlyKeepsResolvablePrototypeIds()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var empty = KnowledgeProfile.Verify(null, server.ProtoMan);
            Assert.That(empty.Mastery, Is.Empty);

            var verified = KnowledgeProfile.Verify(new Dictionary<string, int>
            {
                ["FirstAidKnowledge"] = 3,
                ["PirateDefinitelyMissingKnowledge"] = 4,
            }, server.ProtoMan);

            Assert.That(verified.Mastery, Is.EquivalentTo(new Dictionary<EntProtoId, int>
            {
                ["FirstAidKnowledge"] = 3,
            }));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdminEuiAppliesValidatedProgressWithoutTouchingTemporaryLevels()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var knowledge = server.System<SharedKnowledgeSystem>();
        var players = server.ResolveDependency<IPlayerManager>();
        var admins = server.ResolveDependency<IAdminManager>();
        var euiManager = server.ResolveDependency<EuiManager>();
        KnowledgeAdminEui adminEui = null!;
        EntityUid holder = default;

        await server.WaitAssertion(() =>
        {
            var player = players.Sessions.Single();
            Assert.That(admins.HasAdminFlag(player, AdminFlags.Debug), Is.True,
                "The local integration-test host must have debug permission.");

            holder = entMan.SpawnEntity("PirateKnowledgeLifecycleHolder", MapCoordinates.Nullspace);
            var firstAid = knowledge.SetKnowledgeProgress(holder, "FirstAidKnowledge", 10, 3);
            Assert.That(firstAid, Is.Not.Null);
            firstAid!.Value.Comp.TemporaryLevel = 7;
            firstAid.Value.Comp.TimeToNextExperience = TimeSpan.FromMinutes(1);

            adminEui = new KnowledgeAdminEui(holder);
            euiManager.OpenEui(adminEui, player);

            var initial = (KnowledgeAdminEuiState) adminEui.GetNewState();
            Assert.Multiple(() =>
            {
                Assert.That(initial.Target, Is.Not.Null);
                Assert.That(initial.Skills, Has.Count.EqualTo(knowledge.AllKnowledges.Count));
                Assert.That(initial.Skills.Single(entry => entry.Prototype == "FirstAidKnowledge").Exists, Is.True);
                Assert.That(initial.Skills.Single(entry => entry.Prototype == "FabricationKnowledge").Exists, Is.False);
            });

            adminEui.HandleMessage(new KnowledgeAdminEuiMsg.Apply(new Dictionary<string, KnowledgeAdminEdit>
            {
                ["FirstAidKnowledge"] = new(500, 500),
                ["FabricationKnowledge"] = new(42, 500),
                ["PirateMissingKnowledge"] = new(75, 10),
            }));

            firstAid = knowledge.GetKnowledge(holder, "FirstAidKnowledge");
            var fabrication = knowledge.GetKnowledge(holder, "FabricationKnowledge");
            Assert.That(firstAid, Is.Not.Null);
            Assert.That(fabrication, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(firstAid!.Value.Comp.LearnedLevel, Is.EqualTo(100));
                Assert.That(firstAid.Value.Comp.TemporaryLevel, Is.EqualTo(7));
                Assert.That(firstAid.Value.Comp.Experience, Is.Zero,
                    "A max-level skill cannot retain progress toward another level.");
                Assert.That(firstAid.Value.Comp.TimeToNextExperience, Is.EqualTo(TimeSpan.Zero),
                    "Administrative progress changes must allow an immediate gameplay XP check.");
                Assert.That(fabrication!.Value.Comp.LearnedLevel, Is.EqualTo(42));
                Assert.That(fabrication.Value.Comp.Experience,
                    Is.EqualTo(fabrication.Value.Comp.ExperienceCost - 1));
                Assert.That(knowledge.GetKnowledge(holder, "PirateMissingKnowledge"), Is.Null);
            });

            var state = (KnowledgeAdminEuiState) adminEui.GetNewState();
            var fabricationState = state.Skills.Single(entry => entry.Prototype == "FabricationKnowledge");
            Assert.Multiple(() =>
            {
                Assert.That(fabricationState.Exists, Is.True);
                Assert.That(fabricationState.LearnedLevel, Is.EqualTo(42));
                Assert.That(fabricationState.Experience, Is.EqualTo(fabrication.Value.Comp.ExperienceCost - 1));
            });

            var oversized = Enumerable.Range(0, knowledge.AllKnowledges.Count + 1)
                .ToDictionary(index => $"PirateInvalidKnowledge{index}", _ => new KnowledgeAdminEdit(1, 1));
            adminEui.HandleMessage(new KnowledgeAdminEuiMsg.Apply(oversized));
            Assert.That(knowledge.GetKnowledge(holder, "FabricationKnowledge")?.Comp.LearnedLevel, Is.EqualTo(42),
                "Oversized client payloads must be rejected as a whole.");

            entMan.DeleteEntity(holder);
        });

        await pair.RunTicksSync(3);
        await pair.CleanReturnAsync();
    }
}
