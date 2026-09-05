// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Shared._Pirate.Knowledge;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Knowledge;

/// <summary>
/// Guards the explicit Pirate skill catalog and all prototype references which feed it.
/// The runtime skill system intentionally consumes the catalog instead of enumerating every
/// entity prototype, so a missing catalog entry must be a hard test failure.
/// </summary>
[TestFixture]
public sealed class KnowledgePrototypeIntegrityTest
{
    private static readonly string[] ExpectedSkills =
    [
        "MeleeKnowledge",
        "KnowledgeWeaponsBludgeon",
        "KnowledgeWeaponsShortBlade",
        "KnowledgeWeaponsLongBlade",
        "KnowledgeWeaponsPolearm",
        "KnowledgeWeaponsNonLethal",
        "KnowledgeWeaponsTool",
        "KnowledgeWeaponsShield",
        "KnowledgeWeaponsEnergy",
        "ShootingKnowledge",
        "KnowledgeWeaponsPistol",
        "KnowledgeWeaponsRifle",
        "KnowledgeWeaponsSMG",
        "KnowledgeWeaponsShotgun",
        "KnowledgeWeaponsSniper",
        "KnowledgeWeaponsHeavy",
        "KnowledgeWeaponsLaser",
        "KnowledgeWeaponsMining",
        "ThrowingKnowledge",
        "SurgeryKnowledge",
        "FirstAidKnowledge",
        "ChemistryKnowledge",
        "FabricationKnowledge",
        "WeaponsKnowledge",
        "ArmorKnowledge",
        "MetalworkingKnowledge",
        "CarvingKnowledge",
        "GunsmithingKnowledge",
        "MechanicsKnowledge",
        "TailoringKnowledge",
        "MagicalLiteracyKnowledge",
        "LiteracyKnowledge",
        "JanitorKnowledge",
        "CookingKnowledge",
        "DoorsKnowledge",
        "AirlocksKnowledge",
        "BananiumKnowledge",
        "BotsKnowledge",
        "FurnitureKnowledge",
        "InfrastructureKnowledge",
        "ElectronicsKnowledge",
        "WallsKnowledge",
        "WindowsKnowledge",
        "SpiderCraftKnowledge",
        "SmokeablesKnowledge",
        "RevolutionaryKnowledge",
    ];

    private static readonly IReadOnlyDictionary<string, string> ExpectedSpeciesProfiles =
        new Dictionary<string, string>
        {
            ["Abductor"] = "Abductor",
            ["Arachnid"] = "Arachnid",
            ["Asakim"] = "Human",
            ["Avali"] = "Human",
            ["BananaMen"] = "Human",
            ["Chitinid"] = "Human",
            ["Cyborg"] = "Human",
            ["Diona"] = "Diona",
            ["Dwarf"] = "Dwarf",
            ["Felinid"] = "Human",
            ["Feroxi"] = "Human",
            ["Gingerbread"] = "Human",
            ["Harpy"] = "Human",
            ["Human"] = "Human",
            ["Hydrakin"] = "Human",
            ["IPC"] = "IPC",
            ["Kobold"] = "Human",
            ["Monkey"] = "Human",
            ["Moth"] = "Moth",
            ["Oni"] = "Human",
            ["Plasmaman"] = "Plasmaman",
            ["Reptilian"] = "Reptilian",
            ["Resomi"] = "Human",
            ["Rodentia"] = "Human",
            ["Scurret"] = "Human",
            ["Shadow"] = "Human",
            ["Shadowkin"] = "Human",
            ["Shadowling"] = "Human",
            ["Shattered"] = "Human",
            ["Skeleton"] = "Plasmaman",
            ["SlimePerson"] = "Slimeperson",
            ["Tajaran"] = "Human",
            ["Thaven"] = "Human",
            ["Vox"] = "Vox",
            ["Vulpkanin"] = "Human",
            ["Yautja"] = "Human",
            ["Yowie"] = "Human",
        };

    [Test]
    public async Task PirateSkillCatalogIsCompleteAndResolvable()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypes = server.ProtoMan;
        var factory = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            Assert.That(prototypes.TryIndex<KnowledgeCatalogPrototype>("PirateSkills", out var catalog), Is.True,
                "The explicit PirateSkills catalog prototype is missing.");

            var actual = catalog!.Entries.Select(id => id.Id).ToArray();
            Assert.That(actual, Is.EquivalentTo(ExpectedSkills),
                "PirateSkills no longer matches the intended complete skill catalog.");
            Assert.That(actual.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(actual.Length),
                "PirateSkills contains duplicate skill IDs.");

            var missing = new List<string>();
            var malformed = new List<string>();

            foreach (var id in catalog.Entries)
            {
                if (!prototypes.TryIndex<EntityPrototype>(id, out var entity))
                {
                    missing.Add($"{id.Id}: entity prototype is missing");
                    continue;
                }

                if (entity!.Abstract)
                {
                    malformed.Add($"{id.Id}: catalog entries must be concrete entity prototypes");
                    continue;
                }

                if (!entity.TryGetComponent<KnowledgeComponent>(out var knowledge, factory))
                {
                    malformed.Add($"{id.Id}: missing Knowledge component");
                    continue;
                }

                if (!prototypes.HasIndex<KnowledgeCategoryPrototype>(knowledge!.Category))
                    malformed.Add($"{id.Id}: unknown category {knowledge.Category.Id}");

                if (knowledge.Costs is { Length: 0 })
                    malformed.Add($"{id.Id}: Costs must be null or contain at least one mastery cost");

                if (knowledge.Costs is { } costs && costs.Any(cost => cost < 0))
                    malformed.Add($"{id.Id}: mastery costs cannot be negative");
            }

            Assert.That(missing, Is.Empty, string.Join("\n", missing));
            Assert.That(malformed, Is.Empty, string.Join("\n", malformed));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EveryWeaponClassReferencesACatalogSkill()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypes = server.ProtoMan;
        var factory = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            var catalog = prototypes.Index<KnowledgeCatalogPrototype>("PirateSkills");
            var catalogSkills = catalog.Entries.ToHashSet();
            var failures = new List<string>();

            foreach (var weaponClass in prototypes.EnumeratePrototypes<WeaponClassPrototype>())
            {
                if (!catalogSkills.Contains(weaponClass.Knowledge))
                    failures.Add($"{weaponClass.ID}: unknown skill {weaponClass.Knowledge.Id}");

                if (!prototypes.TryIndex<EntityPrototype>(weaponClass.Knowledge, out var skill) ||
                    !skill!.TryGetComponent<KnowledgeComponent>(out var knowledge, factory))
                {
                    failures.Add($"{weaponClass.ID}: referenced skill is not a Knowledge entity");
                    continue;
                }

                if (!prototypes.HasIndex<KnowledgeCategoryPrototype>(knowledge!.Category))
                    failures.Add($"{weaponClass.ID}: referenced skill has an invalid category");
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EverySpeciesHasTheExpectedValidKnowledgeProfile()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypes = server.ProtoMan;
        var factory = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            var catalog = prototypes.Index<KnowledgeCatalogPrototype>("PirateSkills");
            var actualSpecies = prototypes.EnumeratePrototypes<SpeciesPrototype>()
                .ToDictionary(species => species.ID, StringComparer.Ordinal);
            var failures = new List<string>();

            Assert.That(actualSpecies.Keys, Is.EquivalentTo(ExpectedSpeciesProfiles.Keys),
                "Every local species must be deliberately assigned and covered here.");

            foreach (var (speciesId, expectedProfile) in ExpectedSpeciesProfiles)
            {
                var species = actualSpecies[speciesId];
                if (species.Knowledge.Id != expectedProfile)
                    failures.Add($"{speciesId}: expected {expectedProfile}, got {species.Knowledge.Id}");

                if (!prototypes.TryIndex<KnowledgeProfilePrototype>(species.Knowledge, out var profile))
                {
                    failures.Add($"{speciesId}: unknown profile {species.Knowledge.Id}");
                    continue;
                }

                if (profile!.PointsLimit != 10)
                    failures.Add($"{speciesId}: profile {profile.ID} must have the shared 10-point limit");

                if (!profile.Profile.Mastery.ContainsKey(KnowledgeGameplaySystem.ShootingKnowledge))
                    failures.Add($"{speciesId}: profile {profile.ID} must expose Marksmanship at level zero or higher");

                foreach (var (skillId, mastery) in profile.Profile.Mastery)
                {
                    if (mastery < 0)
                        failures.Add($"{speciesId}: profile {profile.ID} grants negative mastery for {skillId.Id}");

                    if (!catalog.Entries.Contains(skillId))
                    {
                        failures.Add($"{speciesId}: profile {profile.ID} references non-catalog skill {skillId.Id}");
                        continue;
                    }

                    if (!prototypes.TryIndex<EntityPrototype>(skillId, out var skill) ||
                        !skill!.TryGetComponent<KnowledgeComponent>(out _, factory))
                    {
                        failures.Add($"{speciesId}: profile {profile.ID} references invalid skill {skillId.Id}");
                    }
                }
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EveryConstructionKnowledgeReferenceIsResolvable()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypes = server.ProtoMan;
        var factory = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            var catalog = prototypes.Index<KnowledgeCatalogPrototype>("PirateSkills");
            var catalogSkills = catalog.Entries.ToHashSet();
            var failures = new List<string>();

            foreach (var recipe in prototypes.EnumeratePrototypes<ConstructionPrototype>())
            {
                foreach (var id in recipe.Theory.Keys.Concat(recipe.Practical?.Keys ?? Enumerable.Empty<EntProtoId>()))
                {
                    if (!catalogSkills.Contains(id))
                    {
                        failures.Add($"{recipe.ID}: references non-catalog skill {id.Id}");
                        continue;
                    }

                    if (!prototypes.TryIndex<EntityPrototype>(id, out var skill) ||
                        !skill!.TryGetComponent<KnowledgeComponent>(out _, factory))
                    {
                        failures.Add($"{recipe.ID}: references invalid skill entity {id.Id}");
                    }
                }

                if (recipe.QualityPrototype is { } quality &&
                    !prototypes.HasIndex<Content.Shared._Pirate.Knowledge.Quality.QualityPrototype>(quality))
                {
                    failures.Add($"{recipe.ID}: references unknown quality prototype {quality.Id}");
                }
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        });

        await pair.CleanReturnAsync();
    }
}
