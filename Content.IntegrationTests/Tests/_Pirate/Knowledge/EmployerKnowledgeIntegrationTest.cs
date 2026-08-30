// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable

using System.Collections.Generic;
using System.Linq;
using Content.Goobstation.Common.Cloning;
using Content.Shared._Pirate.CCVars;
using Content.Shared._Pirate.Contractors.Prototypes;
using Content.Shared._Pirate.Knowledge;
using Content.Shared.Body.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Polymorph;
using Content.Shared.Preferences;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Knowledge;

[TestFixture]
public sealed class EmployerKnowledgeIntegrationTest
{
    private static readonly ProtoId<KnowledgeProfilePrototype> HumanKnowledgeProfile = "Human";

    [TestPrototypes]
    private const string TestPrototypes = @"
- type: entity
  id: EmployerKnowledgeTestHolder
  components:
  - type: KnowledgeHolder
";

    [TestCase("NanoTrasen", "LiteracyKnowledge", "FabricationKnowledge")]
    [TestCase("IdrisIncorporated", "LiteracyKnowledge", "FirstAidKnowledge")]
    [TestCase("OrionExpress", "FabricationKnowledge", "MechanicsKnowledge")]
    [TestCase("ZengHuPharmaceuticals", "FirstAidKnowledge", "ChemistryKnowledge")]
    [TestCase("HephaestusIndustries", "MechanicsKnowledge", "MetalworkingKnowledge")]
    [TestCase("ZavodskiyInterstellar", "WeaponsKnowledge", "GunsmithingKnowledge")]
    [TestCase("PMCG", "ShootingKnowledge", "FirstAidKnowledge")]
    [TestCase("EinsteinEngines", "ElectronicsKnowledge", "InfrastructureKnowledge")]
    [TestCase("Unemployed", "LiteracyKnowledge", "JanitorKnowledge")]
    public async Task SpawnAddsOnlyTheExpectedBonusesWithoutSpendingProfilePoints(
        string employer, string first, string second)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var knowledge = server.System<SharedKnowledgeSystem>();
        var player = server.ResolveDependency<IPlayerManager>().Sessions.Single();

        await server.WaitAssertion(() =>
        {
            var profile = new HumanoidCharacterProfile
            {
                Species = "Human",
                Employer = employer,
                Knowledge = new KnowledgeProfile(new Dictionary<EntProtoId, int>
                {
                    ["LiteracyKnowledge"] = 1,
                    ["FirstAidKnowledge"] = 1,
                    ["FabricationKnowledge"] = 1,
                }),
            };
            var saved = new KnowledgeProfile(profile.Knowledge);
            var points = knowledge.ProfileCost(saved);
            var limit = server.ProtoMan.Index(HumanKnowledgeProfile).PointsLimit;
            var baseline = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            knowledge.ApplyProfile(baseline, "Human", new KnowledgeProfile(saved));
            var holder = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var spawned = new PlayerSpawnCompleteEvent(holder, player, null, false, true, 1, holder, profile);

            entMan.EventBus.RaiseLocalEvent(holder, spawned, broadcast: true);

            var skills = knowledge.GetAllKnowledge(holder)!;
            Assert.That(skills, Has.Count.EqualTo(knowledge.GetAllKnowledge(baseline)!.Count));
            foreach (var skill in skills)
            {
                var id = entMan.GetComponent<MetaDataComponent>(skill.Owner).EntityPrototype!.ID;
                var original = knowledge.GetKnowledge(baseline, id)!.Value.Comp;
                var bonus = id == first || id == second;
                Assert.Multiple(() =>
                {
                    Assert.That(skill.Comp.LearnedLevel, Is.EqualTo(original.LearnedLevel), id);
                    Assert.That(skill.Comp.Experience, Is.EqualTo(original.Experience), id);
                    Assert.That(SharedKnowledgeSystem.GetMastery(skill.Comp.NetLevel),
                        Is.EqualTo(SharedKnowledgeSystem.GetMastery(original.NetLevel) + (bonus ? 1 : 0)), id);
                    Assert.That(skill.Comp.TemporaryLevel,
                        bonus ? Is.GreaterThan(0) : Is.EqualTo(0), id);
                });
            }

            var temporary = skills.ToDictionary(skill => skill.Owner, skill => skill.Comp.TemporaryLevel);
            knowledge.ApplyEmployerBonuses(holder, employer);
            Assert.Multiple(() =>
            {
                Assert.That(knowledge.GetAllKnowledge(holder)!.ToDictionary(
                    skill => skill.Owner, skill => skill.Comp.TemporaryLevel), Is.EquivalentTo(temporary));
                Assert.That(profile.Knowledge.MemberwiseEquals(saved), Is.True);
                Assert.That(knowledge.ProfileCost(profile.Knowledge), Is.EqualTo(points));
                Assert.That(server.ProtoMan.Index(HumanKnowledgeProfile).PointsLimit, Is.EqualTo(limit));
            });

            entMan.DeleteEntity(holder);
            entMan.DeleteEntity(baseline);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase(0, 0, 25)]
    [TestCase(24, 0, 25)]
    [TestCase(25, 0, 50)]
    [TestCase(49, 0, 50)]
    [TestCase(50, 0, 75)]
    [TestCase(74, 0, 75)]
    [TestCase(75, 0, 88)]
    [TestCase(87, 0, 88)]
    [TestCase(88, 0, 88)]
    [TestCase(99, 0, 99)]
    [TestCase(100, 0, 100)]
    [TestCase(30, 5, 50)]
    [TestCase(0, -10, 25)]
    public async Task BonusReachesExactlyTheNextRankAndDoesNotStack(
        int learned, int otherTemporary, int expectedNet)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var knowledge = server.System<SharedKnowledgeSystem>();

        await server.WaitAssertion(() =>
        {
            var holder = entMan.SpawnEntity("EmployerKnowledgeTestHolder", MapCoordinates.Nullspace);
            var store = knowledge.EnsureKnowledgeContainer(holder);
            var skill = knowledge.EnsureKnowledge(store, "ShootingKnowledge", learned, popup: false)!.Value;
            skill.Comp.Experience = 3;
            skill.Comp.TemporaryLevel = otherTemporary;

            for (var i = 0; i < 2; i++)
            {
                knowledge.ApplyEmployerBonuses(holder, "PMCG");
                var bonus = entMan.GetComponent<EmployerKnowledgeBonusComponent>(skill.Owner);
                Assert.Multiple(() =>
                {
                    Assert.That(skill.Comp.LearnedLevel, Is.EqualTo(learned));
                    Assert.That(skill.Comp.Experience, Is.EqualTo(3));
                    Assert.That(skill.Comp.NetLevel, Is.EqualTo(expectedNet));
                    Assert.That(skill.Comp.TemporaryLevel, Is.EqualTo(expectedNet - learned));
                    Assert.That(skill.Comp.TemporaryLevel - bonus.Level, Is.EqualTo(otherTemporary));
                });
            }

            // First aid was absent: the employer must create it without permanently learning it.
            var firstAid = knowledge.GetKnowledge(holder, "FirstAidKnowledge")!.Value.Comp;
            Assert.That(firstAid.LearnedLevel, Is.Zero);
            Assert.That(firstAid.TemporaryLevel, Is.EqualTo(25));
            entMan.DeleteEntity(holder);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("MissingEmployerForKnowledgeTest")]
    [TestCase("Interdyne")]
    public async Task MissingOrUnconfiguredEmployerDoesNothing(string? employer)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var knowledge = server.System<SharedKnowledgeSystem>();
        await server.WaitAssertion(() =>
        {
            var holder = entMan.SpawnEntity("EmployerKnowledgeTestHolder", MapCoordinates.Nullspace);
            Assert.DoesNotThrow(() => knowledge.ApplyEmployerBonuses(holder, employer));
            Assert.That(knowledge.GetContainer(holder), Is.Null);
            entMan.DeleteEntity(holder);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DisabledSkillsDoNotCreateEmployerKnowledge()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var knowledge = server.System<SharedKnowledgeSystem>();
        await server.WaitAssertion(() =>
        {
            var holder = entMan.SpawnEntity("EmployerKnowledgeTestHolder", MapCoordinates.Nullspace);
            server.CfgMan.SetCVar(KnowledgeCVars.SkillsEnabled, false);
            knowledge.ApplyEmployerBonuses(holder, "NanoTrasen");
            Assert.That(knowledge.GetContainer(holder), Is.Null);
            server.CfgMan.SetCVar(KnowledgeCVars.SkillsEnabled, true);
            entMan.DeleteEntity(holder);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EveryEmployerBonusReferencesAnExistingCatalogKnowledge()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var knowledge = server.System<SharedKnowledgeSystem>();
        var factory = server.ResolveDependency<IComponentFactory>();
        await server.WaitAssertion(() =>
        {
            foreach (var employer in server.ProtoMan.EnumeratePrototypes<EmployerPrototype>())
            {
                foreach (var (id, mastery) in employer.KnowledgeBonuses)
                {
                    Assert.That(mastery, Is.InRange(1, SharedKnowledgeSystem.MasteryNames.Length - 1), employer.ID);
                    Assert.That(knowledge.AllKnowledges.ContainsKey(id), Is.True, $"{employer.ID}: {id}");
                    Assert.That(server.ProtoMan.TryIndex(id, out var prototype), Is.True, $"{employer.ID}: {id}");
                    Assert.That(prototype!.Abstract, Is.False, $"{employer.ID}: {id}");
                    Assert.That(prototype.TryGetComponent<KnowledgeComponent>(out _, factory), Is.True,
                        $"{employer.ID}: {id}");
                }
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BrainTransplantAndMmiKeepTheSameBonusEntities()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var knowledge = server.System<SharedKnowledgeSystem>();
        var body = server.System<SharedBodySystem>();
        var containers = server.System<SharedContainerSystem>();
        await server.WaitAssertion(() =>
        {
            var human = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            knowledge.ApplyEmployerBonuses(human, "PMCG");
            var brain = knowledge.GetContainer(human)!.Value.Owner;
            var skill = knowledge.GetKnowledge(human, "ShootingKnowledge")!.Value;
            var recipient = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var oldBrain = knowledge.GetContainer(recipient)!.Value.Owner;
            Assert.That(containers.TryGetContainingContainer(oldBrain, out var brainSlot), Is.True);
            Assert.That(body.RemoveOrgan(oldBrain), Is.True);
            Assert.That(body.RemoveOrgan(brain), Is.True);
            Assert.That(containers.Insert(brain, brainSlot!), Is.True);
            Assert.That(knowledge.GetKnowledge(recipient, "ShootingKnowledge")?.Owner, Is.EqualTo(skill.Owner));
            knowledge.ApplyEmployerBonuses(recipient, "PMCG");
            Assert.That(skill.Comp.TemporaryLevel, Is.EqualTo(25));

            Assert.That(body.RemoveOrgan(brain), Is.True);
            var borg = entMan.SpawnEntity("PlayerBorgGeneric", MapCoordinates.Nullspace);
            var mmi = entMan.SpawnEntity("MMI", MapCoordinates.Nullspace);
            Assert.That(containers.Insert(brain, containers.GetContainer(mmi, "brain_slot")), Is.True);
            Assert.That(containers.Insert(mmi, containers.GetContainer(borg, "borg_brain")), Is.True);
            Assert.That(knowledge.GetKnowledge(borg, "ShootingKnowledge")?.Owner, Is.EqualTo(skill.Owner));
            knowledge.ApplyEmployerBonuses(borg, "PMCG");
            Assert.That(skill.Comp.LearnedLevel, Is.Zero);
            Assert.That(skill.Comp.TemporaryLevel, Is.EqualTo(25));

            entMan.DeleteEntity(borg);
            entMan.DeleteEntity(oldBrain);
            entMan.DeleteEntity(recipient);
            entMan.DeleteEntity(human);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CloneAndPolymorphPreserveBonusWhenMergingDuplicateSkills(bool destinationAlreadyHasBonus)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var knowledge = server.System<SharedKnowledgeSystem>();
        await server.WaitAssertion(() =>
        {
            var source = entMan.SpawnEntity("EmployerKnowledgeTestHolder", MapCoordinates.Nullspace);
            knowledge.ApplyEmployerBonuses(source, "PMCG");
            var sourceStore = knowledge.GetContainer(source)!.Value;
            var originalFirstAid = knowledge.GetKnowledge(source, "FirstAidKnowledge")!.Value.Owner;
            var clone = entMan.SpawnEntity("EmployerKnowledgeTestHolder", MapCoordinates.Nullspace);
            var cloneStore = knowledge.EnsureKnowledgeContainer(clone);
            var shooting = knowledge.EnsureKnowledge(cloneStore, "ShootingKnowledge", 50, popup: false)!.Value;
            shooting.Comp.TemporaryLevel = 5;
            if (destinationAlreadyHasBonus)
                knowledge.ApplyEmployerBonuses(clone, "PMCG");
            var cloned = new TransferredToCloneEvent(clone);
            entMan.EventBus.RaiseLocalEvent(sourceStore.Owner, ref cloned);
            Assert.That(knowledge.GetAllKnowledge(source), Is.Empty);
            Assert.That(shooting.Comp.LearnedLevel, Is.EqualTo(50));
            Assert.That(shooting.Comp.NetLevel, Is.EqualTo(75));
            Assert.That(shooting.Comp.TemporaryLevel -
                entMan.GetComponent<EmployerKnowledgeBonusComponent>(shooting.Owner).Level, Is.EqualTo(5));
            if (!destinationAlreadyHasBonus)
                Assert.That(knowledge.GetKnowledge(clone, "FirstAidKnowledge")?.Owner, Is.EqualTo(originalFirstAid));
            knowledge.ApplyEmployerBonuses(clone, "PMCG");
            Assert.That(shooting.Comp.NetLevel, Is.EqualTo(75));

            var polymorph = entMan.SpawnEntity("EmployerKnowledgeTestHolder", MapCoordinates.Nullspace);
            var polymorphStore = knowledge.EnsureKnowledgeContainer(polymorph);
            knowledge.EnsureKnowledge(polymorphStore, "ShootingKnowledge", 75, popup: false);
            var morphed = new PolymorphedEvent(clone, polymorph, false);
            entMan.EventBus.RaiseLocalEvent(clone, ref morphed);
            Assert.That(knowledge.GetAllKnowledge(clone), Is.Empty);
            Assert.That(knowledge.GetKnowledge(polymorph, "ShootingKnowledge")?.Comp.LearnedLevel, Is.EqualTo(75));
            Assert.That(knowledge.GetKnowledgeLevel(polymorph, "ShootingKnowledge"), Is.EqualTo(88));
            knowledge.ApplyEmployerBonuses(polymorph, "PMCG");
            Assert.That(knowledge.GetKnowledgeLevel(polymorph, "ShootingKnowledge"), Is.EqualTo(88));

            entMan.DeleteEntity(polymorph);
            entMan.DeleteEntity(clone);
            entMan.DeleteEntity(source);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EmployerTemporaryLevelIsSynchronizedToClient()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        var knowledge = server.System<SharedKnowledgeSystem>();
        var containers = server.System<SharedContainerSystem>();
        EntityUid holder = default;
        EntityUid skillUid = default;
        await server.WaitPost(() =>
        {
            holder = server.EntMan.SpawnEntity("EmployerKnowledgeTestHolder", map.GridCoords);
            var store = knowledge.EnsureKnowledgeContainer(holder);
            var skill = knowledge.EnsureKnowledge(store, "ShootingKnowledge", 25, popup: false)!.Value;
            skillUid = skill.Owner;
            // Expose the hidden unit to PVS so this test isolates KnowledgeComponent state replication.
            Assert.That(containers.Remove(skillUid, store.Comp.Container!), Is.True);
        });
        await pair.RunTicksSync(5);
        await client.WaitAssertion(() =>
        {
            var skill = client.EntMan.GetComponent<KnowledgeComponent>(pair.ToClientUid(skillUid));
            Assert.That(skill.LearnedLevel, Is.EqualTo(25));
            Assert.That(skill.TemporaryLevel, Is.Zero);
        });
        await server.WaitPost(() => knowledge.ApplyEmployerBonuses(holder, "PMCG"));
        await pair.RunTicksSync(5);
        await client.WaitAssertion(() =>
        {
            var skill = client.EntMan.GetComponent<KnowledgeComponent>(pair.ToClientUid(skillUid));
            Assert.That(skill.LearnedLevel, Is.EqualTo(25));
            Assert.That(skill.TemporaryLevel, Is.EqualTo(25));
            Assert.That(skill.NetLevel, Is.EqualTo(50));
        });
        await server.WaitPost(() =>
        {
            server.EntMan.DeleteEntity(skillUid);
            server.EntMan.DeleteEntity(holder);
        });
        await pair.RunTicksSync(1);
        await pair.CleanReturnAsync();
    }
}
