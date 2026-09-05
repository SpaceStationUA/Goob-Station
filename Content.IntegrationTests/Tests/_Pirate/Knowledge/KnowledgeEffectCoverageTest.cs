// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Content.Shared._Pirate.Knowledge;
using Content.Shared._Pirate.Knowledge.Quality;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Knowledge;

/// <summary>
/// Verifies that every skill effect component is present on the intended skill and that the
/// event-driven systems remain free of an accidental world-wide update loop.
/// </summary>
[TestFixture]
public sealed class KnowledgeEffectCoverageTest
{
    [Test]
    public async Task EverySkillEffectComponentIsWiredToACatalogSkill()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypes = server.ProtoMan;
        var factory = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            var catalog = prototypes.Index<KnowledgeCatalogPrototype>("PirateSkills");
            var expected = new Dictionary<string, Type>
            {
                ["KnowledgeWeaponsShield"] = typeof(BlockFractionKnowledgeComponent),
                ["ShootingKnowledge"] = typeof(AimSpeedKnowledgeComponent),
                ["FirstAidKnowledge"] = typeof(InjectTimeKnowledgeComponent),
                ["ThrowingKnowledge"] = typeof(ThrowInsertKnowledgeComponent),
                ["JanitorKnowledge"] = typeof(ThrowInsertKnowledgeComponent),
                ["CookingKnowledge"] = typeof(ExperienceOnCookingComponent),
            };

            var found = new HashSet<Type>();
            var failures = new List<string>();

            foreach (var (skillId, effectType) in expected)
            {
                if (!prototypes.TryIndex<EntityPrototype>(skillId, out var skill))
                {
                    failures.Add($"{skillId}: skill entity prototype is missing");
                    continue;
                }

                if (!skill!.TryGetComponent<KnowledgeComponent>(out _, factory))
                    failures.Add($"{skillId}: missing Knowledge component");

                var present = effectType switch
                {
                    _ when effectType == typeof(BlockFractionKnowledgeComponent) =>
                        skill.TryGetComponent<BlockFractionKnowledgeComponent>(out _, factory),
                    _ when effectType == typeof(AimSpeedKnowledgeComponent) =>
                        skill.TryGetComponent<AimSpeedKnowledgeComponent>(out _, factory),
                    _ when effectType == typeof(InjectTimeKnowledgeComponent) =>
                        skill.TryGetComponent<InjectTimeKnowledgeComponent>(out _, factory),
                    _ when effectType == typeof(ThrowInsertKnowledgeComponent) =>
                        skill.TryGetComponent<ThrowInsertKnowledgeComponent>(out _, factory),
                    _ when effectType == typeof(ExperienceOnCookingComponent) =>
                        skill.TryGetComponent<ExperienceOnCookingComponent>(out _, factory),
                    _ => false,
                };

                if (!present)
                    failures.Add($"{skillId}: missing {effectType.Name}");
                else
                    found.Add(effectType);

                if (!catalog.Entries.Contains(skillId))
                    failures.Add($"{skillId}: effect skill is not in PirateSkills catalog");
            }

            Assert.That(found, Is.EquivalentTo(
                new[]
                {
                    typeof(BlockFractionKnowledgeComponent),
                    typeof(AimSpeedKnowledgeComponent),
                    typeof(InjectTimeKnowledgeComponent),
                    typeof(ThrowInsertKnowledgeComponent),
                    typeof(ExperienceOnCookingComponent),
                }), "One or more effect component types are not represented by a skill.");
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EveryConfiguredSkillCurveProducesFiniteValues()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypes = server.ProtoMan;
        var factory = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            var catalog = prototypes.Index<KnowledgeCatalogPrototype>("PirateSkills");
            var failures = new List<string>();

            foreach (var id in catalog.Entries)
            {
                if (!prototypes.TryIndex<EntityPrototype>(id, out var skill))
                {
                    failures.Add($"{id.Id}: missing entity prototype");
                    continue;
                }

                if (skill!.TryGetComponent<AimSpeedKnowledgeComponent>(out var aim, factory))
                    CheckCurve(id, nameof(AimSpeedKnowledgeComponent), aim!.Curve, failures);

                if (skill.TryGetComponent<BlockFractionKnowledgeComponent>(out var block, factory))
                    CheckCurve(id, nameof(BlockFractionKnowledgeComponent), block!.Curve, failures);

                if (skill.TryGetComponent<InjectTimeKnowledgeComponent>(out var inject, factory))
                    CheckCurve(id, nameof(InjectTimeKnowledgeComponent), inject!.Curve, failures);

                if (skill.TryGetComponent<ThrowInsertKnowledgeComponent>(out var insert, factory))
                    CheckCurve(id, nameof(ThrowInsertKnowledgeComponent), insert!.Curve, failures);
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public void MasteryBoundariesAndQualityBucketsAreStable()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SharedKnowledgeSystem.GetMastery(0), Is.EqualTo(0));
            Assert.That(SharedKnowledgeSystem.GetMastery(24), Is.EqualTo(0));
            Assert.That(SharedKnowledgeSystem.GetMastery(25), Is.EqualTo(1));
            Assert.That(SharedKnowledgeSystem.GetMastery(49), Is.EqualTo(1));
            Assert.That(SharedKnowledgeSystem.GetMastery(50), Is.EqualTo(2));
            Assert.That(SharedKnowledgeSystem.GetMastery(74), Is.EqualTo(2));
            Assert.That(SharedKnowledgeSystem.GetMastery(75), Is.EqualTo(3));
            Assert.That(SharedKnowledgeSystem.GetMastery(87), Is.EqualTo(3));
            Assert.That(SharedKnowledgeSystem.GetMastery(88), Is.EqualTo(4));
            Assert.That(SharedKnowledgeSystem.GetMastery(99), Is.EqualTo(4));
            Assert.That(SharedKnowledgeSystem.GetMastery(100), Is.EqualTo(5));

            for (var mastery = 0; mastery <= 5; mastery++)
                Assert.That(
                    SharedKnowledgeSystem.GetMastery(SharedKnowledgeSystem.GetInverseMastery(mastery)),
                    Is.EqualTo(mastery));

            Assert.That(QualitySystem.QualityFromModifier(88), Is.EqualTo(5));
            Assert.That(QualitySystem.QualityFromModifier(44), Is.EqualTo(4));
            Assert.That(QualitySystem.QualityFromModifier(20), Is.EqualTo(3));
            Assert.That(QualitySystem.QualityFromModifier(10), Is.EqualTo(2));
            Assert.That(QualitySystem.QualityFromModifier(5), Is.EqualTo(1));
            Assert.That(QualitySystem.QualityFromModifier(0), Is.EqualTo(0));
            Assert.That(QualitySystem.QualityFromModifier(-5), Is.EqualTo(-1));
            Assert.That(QualitySystem.QualityFromModifier(-10), Is.EqualTo(-2));
            Assert.That(QualitySystem.QualityFromModifier(-20), Is.EqualTo(-3));
            Assert.That(QualitySystem.QualityFromModifier(-44), Is.EqualTo(-4));
            Assert.That(QualitySystem.QualityFromModifier(-45), Is.EqualTo(-5));
        });
    }

    [Test]
    public void KnowledgeAndQualitySystemsHaveNoPerFrameUpdateLoop()
    {
        var types = new[]
        {
            typeof(SharedKnowledgeSystem),
            typeof(KnowledgeGameplaySystem),
            typeof(KnowledgeGrantSystem),
            typeof(ConstructionKnowledgeSystem),
            typeof(QualitySystem),
            typeof(WeaponClassSystem),
        };

        foreach (var type in types)
        {
            var updates = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method => method.DeclaringType == type && method.Name == "Update")
                .ToArray();
            Assert.That(updates, Is.Empty, $"{type.Name} must stay event-driven and not scan the world every tick.");
        }
    }

    private static void CheckCurve(
        EntProtoId skill,
        string component,
        SkillCurve curve,
        ICollection<string> failures)
    {
        if (curve is null)
        {
            failures.Add($"{skill.Id}: {component} curve is null");
            return;
        }

        foreach (var level in new[] { 0, 1, 25, 50, 75, 99, 100 })
        {
            var value = curve.GetCurve(level);
            if (float.IsNaN(value) || float.IsInfinity(value))
                failures.Add($"{skill.Id}: {component} curve returned {value} at level {level}");
        }
    }
}
