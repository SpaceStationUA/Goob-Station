// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Shared._Pirate.Body.Chips;
using Content.Shared._Pirate.Knowledge;
using Content.Shared._Pirate.Roles;
using Content.Shared.Roles;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Knowledge;

[TestFixture]
public sealed class SkillChipPrototypeIntegrityTest
{
    private static readonly (string Job, string[] Chips)[] ExpectedJobChips =
    [
        ("Captain", ["SkillChipEchelonEducation", "SkillChipLaser", "SkillChipLongBlade"]),
        ("HeadOfPersonnel", ["SkillChipEchelonEducation", "SkillChipLaser"]),
        ("Quartermaster", ["SkillChipEchelonEducation", "SkillChipUnarmed", "SkillChipMining"]),
        ("CargoTechnician", ["SkillChipEducation"]),
        ("SalvageSpecialist", ["SkillChipCombatEducation", "SkillChipMining", "SkillChipCloseQuarters"]),
        ("ChiefEngineer", ["SkillChipEchelonEducation", "SkillChipDatabase", "SkillChipTool"]),
        ("StationEngineer", ["SkillChipEducation", "SkillChipDatabase", "SkillChipTool"]),
        ("AtmosphericTechnician", ["SkillChipEducation", "SkillChipDatabase", "SkillChipBludgeon"]),
        ("TechnicalAssistant", ["SkillChipEducation", "SkillChipDatabaseBasic"]),
        ("ChiefMedicalOfficer", ["SkillChipCMO", "SkillChipPistol", "SkillChipEchelonEducation"]),
        ("MedicalDoctor", ["SkillChipDoctor", "SkillChipEducation", "SkillChipSurgeon"]),
        ("MedicalIntern", ["SkillChipDoctor", "SkillChipEducation", "SkillChipSurgeon"]),
        ("Paramedic", ["SkillChipDoctor", "SkillChipEducation", "SkillChipSurgeon"]),
        ("Chemist", ["SkillChipEducation", "SkillChipChemist"]),
        ("Psychologist", ["SkillChipEducation", "SkillChipDoctor"]),
        ("Virologist", ["SkillChipEducation", "SkillChipDoctor"]),
        ("ResearchDirector", ["SkillChipEchelonEducation"]),
        ("Scientist", ["SkillChipEducation", "SkillChipDatabase"]),
        ("ResearchAssistant", ["SkillChipEducation", "SkillChipDatabaseBasic"]),
        ("Roboticist", ["SkillChipEducation", "SkillChipDatabase"]),
        ("HeadOfSecurity", ["SkillChipCombatEducation", "SkillChipCombatHOS"]),
        ("Warden", ["SkillChipCombatEducation", "SkillChipSidearms", "SkillChipShotgun"]),
        ("SecurityOfficer", ["SkillChipCombatEducation", "SkillChipSidearms", "SkillChipNonLethal"]),
        ("SecurityCadet", ["SkillChipCombatEducation", "SkillChipSidearms", "SkillChipNonLethal"]),
        ("Detective", ["SkillChipCombatEducation", "SkillChipSidearms", "SkillChipBludgeon"]),
        ("Brigmedic", ["SkillChipCombatEducation", "SkillChipDoctor", "SkillChipSurgeon"]),
        ("SecurityClown", ["SkillChipCombatEducation", "SkillChipClown", "SkillChipUnarmed"]),
        ("Passenger", ["SkillChipTiderDampener", "SkillChipEducation"]),
        ("Bartender", ["SkillChipEducation", "SkillChipShotgun", "SkillChipUnarmed"]),
        ("Botanist", ["SkillChipEducation", "SkillChipTool", "SkillChipStoner"]),
        ("Chaplain", ["SkillChipEducation", "SkillChipCloseQuarters", "SkillChipMagLit"]),
        ("Chef", ["SkillChipEducation", "SkillChipShortBlade"]),
        ("Clown", ["SkillChipEducation", "SkillChipNonLethal", "SkillChipClown"]),
        ("Janitor", ["SkillChipEducation", "SkillChipJanitor", "SkillChipThrowing"]),
        ("Lawyer", ["SkillChipEducation"]),
        ("Librarian", ["SkillChipGunsmith"]),
        ("Mime", ["SkillChipEducation"]),
        ("Musician", ["SkillChipEducation"]),
        ("ServiceWorker", ["SkillChipEducation"]),
        ("Reporter", ["SkillChipEducation"]),
        ("RadioHost", ["SkillChipEducation"]),
        ("NanotrasenRepresentative", ["SkillChipEchelonEducation", "SkillChipLaser"]),
        ("CentralCommandOfficial", ["SkillChipERT"]),
        ("CBURN", ["SkillChipERT"]),
        ("ERTLeader", ["SkillChipERT"]),
        ("ERTChaplain", ["SkillChipERT"]),
        ("ERTEngineer", ["SkillChipERT", "SkillChipDatabase"]),
        ("ERTSecurity", ["SkillChipERT"]),
        ("ERTMedical", ["SkillChipERT", "SkillChipCMO"]),
        ("ERTJanitor", ["SkillChipERT"]),
        ("DeathSquad", ["SkillChipDeathSquad"]),
        ("CaptainInterdyne", ["SkillChipEchelonEducation", "SkillChipPistol", "SkillChipSurgeon"]),
        ("InterdyneDeputy", ["SkillChipEchelonEducation", "SkillChipPistol"]),
        ("InterdyneSecure", ["SkillChipCombatEducation", "SkillChipRifle", "SkillChipNonLethal"]),
        ("InterdyneMed", ["SkillChipDoctor", "SkillChipEducation", "SkillChipSurgeon"]),
        ("InterdyneEngineer", ["SkillChipEducation", "SkillChipDatabase", "SkillChipTool"]),
        ("Interdyne", ["SkillChipEducation", "SkillChipDatabase"]),
        ("InterdynePilot", ["SkillChipEducation", "SkillChipPistol"]),
        ("InterdyneService", ["SkillChipEducation", "SkillChipShortBlade"]),
        ("InterdyneShaftMiners", ["SkillChipCombatEducation", "SkillChipMining", "SkillChipShortBlade"]),
        ("SquidGameGuardCircle", ["SkillChipCombatEducation", "SkillChipPistol", "SkillChipNonLethal"]),
        ("SquidGameGuardTriangle", ["SkillChipCombatEducation", "SkillChipPistol", "SkillChipNonLethal"]),
        ("SquidGameGuardSquare", ["SkillChipCombatEducation", "SkillChipPistol", "SkillChipNonLethal"]),
        ("SquidGameGuardUwU", ["SkillChipCombatEducation", "SkillChipPistol", "SkillChipNonLethal"]),
        ("SquidGamePlayer", ["SkillChipTiderDampener", "SkillChipEducation"]),
        ("NavyOfficer", ["SkillChipERT"]),
        ("NavyCaptain", ["SkillChipERT"]),
        ("SpecialOperationsOfficer", ["SkillChipERT"]),
        ("MercenaryCaptain", ["SkillChipERT"]),
        ("SyndicateHighCommander", ["SkillChipERT"]),
        ("HecuOperative", ["SkillChipERT"]),
        ("NavyOfficerUndercover", ["SkillChipEchelonEducation"]),
        ("Diplomat", ["SkillChipEchelonEducation"]),
        ("Inspector", ["SkillChipEchelonEducation"]),
        ("OuterCommander", ["SkillChipEchelonEducation"]),
        ("GovernmentMan", ["SkillChipEchelonEducation"]),
        ("Conquest", ["SkillChipEducation"]),
        ("BlueshieldOfficer", ["SkillChipCombatEducation", "SkillChipSidearmsAdvanced", "SkillChipFieldMedicine"]),
        ("SecurityInstructor", ["SkillChipCombatEducation", "SkillChipSidearmsAdvanced", "SkillChipNonLethal"]),
        ("ShaftMiner", ["SkillChipCombatEducation", "SkillChipMining", "SkillChipShortBlade"]),
        ("ForensicMantis", ["SkillChipEducation", "SkillChipDatabase", "SkillChipCloseQuarters"]),
        ("AdministrativeAssistant", ["SkillChipEchelonEducation"]),
        ("NanotrasenCareerTrainer", ["SkillChipEducation"]),
        ("CommandMaid", ["SkillChipEducation"]),
        ("PartyMaker", ["SkillChipEducation"]),
        ("Visitor", ["SkillChipTiderDampener", "SkillChipEducation"]),
    ];

    private static readonly string[] ExpectedChips =
    [
        "SkillChipHeavy", "SkillChipLaser", "SkillChipMarksmanship", "SkillChipMining",
        "SkillChipTool", "SkillChipPistol", "SkillChipRifle", "SkillChipSMG", "SkillChipShotgun",
        "SkillChipSniper", "SkillChipEnergy", "SkillChipBludgeon", "SkillChipShortBlade",
        "SkillChipSidearms", "SkillChipSidearmsAdvanced",
        "SkillChipLongBlade", "SkillChipNonLethal", "SkillChipPolearm", "SkillChipUnarmed",
        "SkillChipShield", "SkillChipCombatHOS", "SkillChipCloseQuarters",
        "SkillChipArmorsmithing", "SkillChipArmorsmithing2", "SkillChipWeaponsmithing",
        "SkillChipWeaponsmithing2", "SkillChipBlacksmith", "SkillChipBlacksmith2",
        "SkillChipWoodworker", "SkillChipWoodworker2", "SkillChipGunsmith", "SkillChipGunsmith2",
        "SkillChipMechanic", "SkillChipMechanic2", "SkillChipElectronics", "SkillChipElectronics2",
        "SkillChipTailor", "SkillChipTailor2", "SkillChipStoner", "SkillChipDatabase",
        "SkillChipDatabaseBasic", "SkillChipFieldMedicine",
        "SkillChipEducation", "SkillChipCombatEducation", "SkillChipEchelonEducation",
        "SkillChipMagLit", "SkillChipJanitor", "SkillChipClown", "SkillChipDoctor",
        "SkillChipChemist", "SkillChipSurgeon", "SkillChipCMO",
        "SkillChipMagicalDampener", "SkillChipCombatDampener", "SkillChipMindPurge",
        "SkillChipTiderDampener",
        "SkillChipThrowing", "SkillChipThrowingTampered",
        "SkillChipDeathSquad", "SkillChipERT", "SkillChipFreelancer",
        "SkillChipNukie", "SkillChipSyndieSoldierTeamLeader", "SkillChipSyndieSoldier",
        "SkillChipSyndieMarshal", "SkillChipSyndieVisitor", "SkillChipPirateCaptainScooner",
        "SkillChipPirateScooner", "SkillChipBlackmarketeer", "SkillChipCossack",
    ];

    private static readonly string[] ExcludedTraumaChips = ["SkillChipChef", "SkillChipLibrarian"];

    private static readonly string[] AbsentJobs = ["DClass", "Geneticist"];

    [Test]
    public async Task EveryChipGrantsOnlyRealSkills()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypes = server.ProtoMan;
        var knowledge = server.System<SharedKnowledgeSystem>();

        await server.WaitAssertion(() =>
        {
            var chips = ChipPrototypes(prototypes, server.EntMan.ComponentFactory).ToArray();
            Assert.That(chips, Is.Not.Empty, "No brain chip prototypes were loaded.");

            foreach (var chip in chips)
            {
                if (!chip.TryGetComponent<KnowledgeGrantOnWearComponent>(out var grant, server.EntMan.ComponentFactory))
                    continue;

                foreach (var id in grant.Skills.Keys)
                {
                    Assert.That(knowledge.AllKnowledges.ContainsKey(id), Is.True,
                        $"Chip {chip.ID} grants {id}, which is not in the Pirate skill catalogue.");
                    Assert.That(id.Id.StartsWith("Language") || id.Id.StartsWith("MartialArt"), Is.False,
                        $"Chip {chip.ID} still carries the unsupported payload {id}.");
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ChipInventoryMatchesTheDeclaredSet()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var found = ChipPrototypes(server.ProtoMan, server.EntMan.ComponentFactory)
                .Select(p => p.ID)
                .Where(id => id.StartsWith("SkillChip"))
                .ToArray();

            Assert.That(found, Is.EquivalentTo(ExpectedChips),
                "The enabled chip inventory drifted from the declared set.");

            foreach (var excluded in ExcludedTraumaChips)
            {
                Assert.That(server.ProtoMan.HasIndex<EntityPrototype>(excluded), Is.False,
                    $"{excluded} is deliberately unported until its unsupported payload has a target-native design.");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EveryChipFitsABrain()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            foreach (var chip in ChipPrototypes(server.ProtoMan, server.EntMan.ComponentFactory))
            {
                Assert.That(chip.TryGetComponent<OrganChipComponent>(out var comp, server.EntMan.ComponentFactory),
                    Is.True, $"{chip.ID} lost its OrganChip component.");
                Assert.That(comp!.Whitelist?.Components, Does.Contain("Brain"),
                    $"{chip.ID} does not whitelist brains, so nothing can install it.");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MappedJobsUseRealChipsAndFitInABrain()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypes = server.ProtoMan;

        await server.WaitAssertion(() =>
        {
            var mapped = 0;
            foreach (var job in prototypes.EnumeratePrototypes<JobPrototype>())
            {
                foreach (var special in job.Special)
                {
                    if (special is not OrganChipsSpecial chips)
                        continue;

                    mapped++;
                    Assert.That(chips.Chips, Has.Count.LessThanOrEqualTo(3),
                        $"Job {job.ID} asks for {chips.Chips.Count} chips, but a brain holds three.");
                    Assert.That(chips.Chips, Is.Unique, $"Job {job.ID} lists the same chip twice.");

                    foreach (var id in chips.Chips)
                    {
                        Assert.That(prototypes.TryIndex<EntityPrototype>(id.Id, out var proto), Is.True,
                            $"Job {job.ID} references missing chip {id}.");
                        Assert.That(
                            proto!.TryGetComponent<OrganChipComponent>(out _, server.EntMan.ComponentFactory),
                            Is.True,
                            $"Job {job.ID} references {id}, which is not a chip.");
                    }
                }
            }

            Assert.That(mapped, Is.EqualTo(ExpectedJobChips.Length),
                "The set of jobs carrying chips drifted from the declared mapping.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RepresentativeJobsReceiveTheExpectedChips()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            foreach (var (jobId, expected) in ExpectedJobChips)
            {
                Assert.That(server.ProtoMan.TryIndex<JobPrototype>(jobId, out var job), Is.True,
                    $"Job {jobId} is missing.");

                var chips = job!.Special.OfType<OrganChipsSpecial>().SelectMany(s => s.Chips).Select(c => c.Id);
                Assert.That(chips, Is.EquivalentTo(expected), $"Job {jobId} has the wrong chips.");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AbsentTraumaJobsAreNotInvented()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            foreach (var jobId in AbsentJobs)
            {
                Assert.That(server.ProtoMan.HasIndex<JobPrototype>(jobId), Is.False,
                    $"{jobId} only exists in Trauma and must not be created by this port.");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UplinkAndCargoReferencesResolve()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypes = server.ProtoMan;

        await server.WaitAssertion(() =>
        {
            Assert.That(prototypes.HasIndex<StoreCategoryPrototype>("UplinkSkills"), Is.True);

            foreach (var uplink in new[] { "UplinkImplant", "BaseUplinkRadio" })
            {
                Assert.That(prototypes.TryIndex<EntityPrototype>(uplink, out var proto), Is.True,
                    $"Uplink {uplink} is missing.");
                Assert.That(
                    proto!.TryGetComponent<StoreComponent>(out var store, server.EntMan.ComponentFactory),
                    Is.True, $"{uplink} is not a store.");
                Assert.That(store!.Categories.Select(c => c.Id), Does.Contain("UplinkSkills"),
                    $"{uplink} inherits the uplink preset but cannot reach the skills category.");
            }

            foreach (var listing in prototypes.EnumeratePrototypes<ListingPrototype>())
            {
                if (!listing.Categories.Any(c => c.Id == "UplinkSkills") || listing.ProductEntity is not { } product)
                    continue;

                Assert.That(prototypes.HasIndex<EntityPrototype>(product.Id), Is.True,
                    $"Uplink listing {listing.ID} sells missing entity {product}.");
            }

            foreach (var crate in new[]
                     {
                         "CrateSkillChipsCargo", "CrateSkillChipsEngi", "CrateSkillChipsMed",
                         "CrateSkillChipsSci", "CrateSkillChipsSec", "CrateSyndicateCombatSkillChips",
                     })
            {
                Assert.That(prototypes.HasIndex<EntityPrototype>(crate), Is.True, $"Missing crate {crate}.");
            }

            foreach (var box in new[]
                     {
                         "BoxSkillChipsService", "BoxSkillChipsCargo", "BoxSkillChipsEngi",
                         "BoxSkillChipsMed", "BoxSkillChipsSci",
                     })
            {
                Assert.That(prototypes.HasIndex<EntityPrototype>(box), Is.True, $"Missing box {box}.");
            }
        });

        await pair.CleanReturnAsync();
    }

    private static IEnumerable<EntityPrototype> ChipPrototypes(IPrototypeManager prototypes, IComponentFactory factory)
    {
        foreach (var proto in prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (!proto.Abstract && proto.TryGetComponent<OrganChipComponent>(out _, factory))
                yield return proto;
        }
    }
}
