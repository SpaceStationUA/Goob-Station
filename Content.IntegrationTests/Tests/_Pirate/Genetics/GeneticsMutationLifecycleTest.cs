// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Pirate.Shared.Genetics;
using Content.Pirate.Shared.Genetics.Mutations;
using Content.Server.Polymorph.Components;
using Content.Shared._Goobstation.Wizard.Mutate;
using Content.Shared.Clumsy;
using Content.Shared.Damage.Components;
using Content.Shared.Traits.Assorted;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Genetics;

[TestFixture]
public sealed class GeneticsMutationLifecycleTest
{
    [TestCase("MutationColdAdaptation", "MutationThermalAdaptation", "SpecialLowTempImmunity")]
    [TestCase("MutationHeatAdaptation", "MutationThermalAdaptation", "SpecialHighTempImmunity")]
    [TestCase("MutationNearSightedness", "MutationBlindness", "PermanentBlindness")]
    [TestCase("MutationHulk", "MutationOrk", "Hulk")]
    public async Task UpgradesKeepReplacementComponentsUntilCleansed(string oldMutation, string replacement, string component)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var mutations = server.System<MutationSystem>();
        var registration = server.ResolveDependency<IComponentFactory>().GetRegistration(component);
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var human = entities.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(mutations.AddMutation(human, oldMutation), Is.True);
            Assert.That(entities.HasComponent(human, registration), Is.True);
            Assert.That(mutations.AddMutation(human, replacement), Is.True);
            Assert.That(mutations.HasMutation(human, oldMutation), Is.False);
            Assert.That(mutations.HasMutation(human, replacement), Is.True);
            Assert.That(entities.HasComponent(human, registration), Is.True);
            if (replacement == "MutationBlindness")
                Assert.That(entities.GetComponent<PermanentBlindnessComponent>(human).Blindness, Is.Zero);
            if (replacement == "MutationOrk")
                Assert.That(entities.GetComponent<HulkComponent>(human).SkinColor, Is.EqualTo(Color.FromHex("#828163")));
            Assert.That(mutations.RemoveMutation(human, replacement), Is.True);
            Assert.That(entities.HasComponent(human, registration), Is.False);
            entities.DeleteEntity(human);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("MobHuman", "MutationClumsiness", "Clumsy")]
    [TestCase("MobMonkey", "MutationClumsiness", "Clumsy")]
    [TestCase("MobHuman", "MutationMute", "Muted")]
    [TestCase("MobHuman", "MutationPressureAdaptation", "SpecialPressureImmunity")]
    public async Task StatusMutationCleanupPreservesInnateComponents(string prototype, string mutation, string component)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var mutations = server.System<MutationSystem>();
        var factory = server.ResolveDependency<IComponentFactory>();
        var registration = factory.GetRegistration(component);
        var map = await pair.CreateTestMap();
        EntityUid human = default;
        IComponent original = null;
        await server.WaitAssertion(() =>
        {
            human = entities.SpawnEntity(prototype, map.GridCoords);
            if (!entities.TryGetComponent(human, registration.Type, out original))
            {
                original = factory.GetComponent(registration.Type);
                entities.AddComponent(human, original);
            }
            if (original is ClumsyComponent clumsy)
                clumsy.ClumsyDefaultCheck = 0.17f;

            Assert.That(mutations.AddMutation(human, mutation), Is.True);
            Assert.That(entities.TryGetComponent(human, registration.Type, out var active), Is.True);
            Assert.That(active, Is.SameAs(original));
            Assert.That(mutations.RemoveMutation(human, mutation), Is.True);
        });
        await pair.RunTicksSync(1);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.TryGetComponent(human, registration.Type, out var restored), Is.True);
            Assert.That(restored, Is.SameAs(original));
            if (restored is ClumsyComponent clumsy)
                Assert.That(clumsy.ClumsyDefaultCheck, Is.EqualTo(0.17f));
            entities.DeleteEntity(human);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ActivatingMonkifiedOnScannedSubjectsDoesNotRepopulateTheOldGenome(bool native)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var mutations = server.System<MutationSystem>();
        var genome = server.System<ScannedGenomeSystem>();
        var map = await pair.CreateTestMap();
        EntityUid human = default;
        await server.WaitAssertion(() =>
        {
            human = entities.SpawnEntity("MobHuman", map.GridCoords);
            mutations.ClearDormant(human);
#pragma warning disable RA0002 // Deterministic dormant sequences for both native and foreign polymorphs.
            var dormant = entities.GetComponent<MutatableComponent>(human).Dormant;
            dormant.Add("MutationDwarfism");
            if (native)
                dormant.Add("MutationMonkified");
#pragma warning restore RA0002
            genome.ScanGenome(human);
        });
        for (var cycle = 0; cycle < 2; cycle++)
        {
            await server.WaitAssertion(() =>
            {
                var original = entities.GetComponent<ScannedGenomeComponent>(human).Sequences
                    .Select(s => (s.Mutation, s.Bases, s.OriginalBases)).ToArray();
                Assert.That(mutations.AddMutation(human, "MutationMonkified"), Is.True);
                var monkey = entities.EntityQuery<PolymorphedEntityComponent>().Single(p => p.Parent == human).Owner;
                Assert.That(entities.GetComponent<ScannedGenomeComponent>(human).Sequences, Is.Empty);
                Assert.That(entities.GetComponent<ScannedGenomeComponent>(monkey).Sequences
                    .Select(s => (s.Mutation, s.Bases, s.OriginalBases)), Is.EquivalentTo(original));
                Assert.That(mutations.RemoveMutation(monkey, "MutationMonkified"), Is.True);
                Assert.That(entities.GetComponent<ScannedGenomeComponent>(human).Sequences
                    .Select(s => (s.Mutation, s.Bases, s.OriginalBases)), Is.EquivalentTo(original));
            });
            await pair.RunTicksSync(1);
        }
        await server.WaitPost(() => entities.DeleteEntity(human));
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task HeadlessPolymorphsRestoreEachBodiesAnatomyAndOriginalOrgans(bool cleanseWhileTransformed)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var mutations = server.System<MutationSystem>();
        var body = server.System<GeneticsBodySystem>();
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var human = entities.SpawnEntity("MobHuman", map.GridCoords);
            var brain = body.GetOrgan(human, "Brain");
            var eyes = body.GetOrgan(human, "Eyes");
            Assert.That(mutations.AddMutation(human, "MutationHeadless"), Is.True);
            Assert.That(body.GetOrgan(human, "Head"), Is.Null);
            Assert.That(mutations.AddMutation(human, "MutationMonkified"), Is.True);
            var monkey = entities.EntityQuery<PolymorphedEntityComponent>().Single(p => p.Parent == human).Owner;
            Assert.That(mutations.HasMutation(monkey, "MutationHeadless"), Is.True);
            Assert.That(body.GetOrgan(monkey, "Head"), Is.Null);
            Assert.That(body.GetOrgan(monkey, "Brain"), Is.Not.Null);
            Assert.That(body.GetOrgan(human, "Brain"), Is.EqualTo(brain));
            Assert.That(body.GetOrgan(human, "Eyes"), Is.EqualTo(eyes));
            var restoredHead = body.GetOrgan(human, "Head");
            Assert.That(restoredHead, Is.Not.Null, "The old body no longer owns Headless and must be restored.");
            Assert.That(entities.GetComponent<DamageableComponent>(restoredHead!.Value).TotalDamage.Float(), Is.Zero,
                "Transferring a mutation must not apply the manual cure's head damage.");

            if (cleanseWhileTransformed)
            {
                Assert.That(mutations.RemoveMutation(monkey, "MutationHeadless"), Is.True);
                Assert.That(body.GetOrgan(monkey, "Head"), Is.Not.Null);
            }
            Assert.That(mutations.RemoveMutation(monkey, "MutationMonkified"), Is.True);
            Assert.That(mutations.HasMutation(human, "MutationHeadless"), Is.EqualTo(!cleanseWhileTransformed));
            Assert.That(body.GetOrgan(human, "Head") != null, Is.EqualTo(cleanseWhileTransformed));
            Assert.That(body.GetOrgan(human, "Brain"), Is.EqualTo(brain));
            Assert.That(body.GetOrgan(human, "Eyes"), Is.EqualTo(eyes));
            if (!cleanseWhileTransformed)
                Assert.That(mutations.RemoveMutation(human, "MutationHeadless"), Is.True);
            Assert.That(body.GetOrgan(human, "Head"), Is.Not.Null);
            Assert.That(body.GetOrgan(human, "Brain"), Is.EqualTo(brain));
            entities.DeleteEntity(human);
        });
        await pair.RunTicksSync(1);
        await pair.CleanReturnAsync();
    }
}
