// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Pirate.Shared.Genetics;
using Content.Pirate.Shared.Genetics.Mutations;
using Content.Server.Polymorph.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Humanoid;
using Content.Shared.Temperature.Components;
using Content.Shared.StatusEffectNew;
using Content.Pirate.Common.Bloodstream;
using Content.Server.Body.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Genetics;

[TestFixture]
public sealed class GeneticsMutationIntegrationTest
{
    [TestCase("MutationColdWeakness")]
    [TestCase("MutationHeatWeakness")]
    [TestCase("MutationThermalWeakness")]
    public async Task RemovingThermalMutationsRestoresNormalRegulation(string mutation)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var mutations = server.System<MutationSystem>();
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var human = entities.SpawnEntity("MobHuman", map.GridCoords);
            var regulator = entities.GetComponent<ThermalRegulatorComponent>(human);
            var original = Values();
            Assert.That(mutations.AddMutation(human, mutation), Is.True);
            Assert.That(Values(), Is.Not.EqualTo(original));
            Assert.That(mutations.RemoveMutation(human, mutation), Is.True);
            var restored = Values();
            for (var i = 0; i < original.Length; i++)
                Assert.That(restored[i], Is.EqualTo(original[i]).Within(0.001f));
            entities.DeleteEntity(human);

            float[] Values() => [regulator.ShiveringHeatRegulation, regulator.SweatHeatRegulation,
                regulator.MetabolismHeat, regulator.ImplicitHeatRegulation];
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HypermetabolicBloodModifiersApplyOnlyWhileTheMutationIsPresent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var mutations = server.System<MutationSystem>();
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var human = entities.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(mutations.AddMutation(human, "MutationHypermetabolicBlood"), Is.True);
            var regeneration = new BloodRegenerationModifierEvent(2);
            entities.EventBus.RaiseLocalEvent(human, ref regeneration);
            Assert.That(regeneration.Amount, Is.EqualTo(12));
            Assert.That(mutations.RemoveMutation(human, "MutationHypermetabolicBlood"), Is.True);
            regeneration = new BloodRegenerationModifierEvent(2);
            entities.EventBus.RaiseLocalEvent(human, ref regeneration);
            Assert.That(regeneration.Amount, Is.EqualTo(2));
            entities.DeleteEntity(human);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InstabilityStartsMeltingAtTheThresholdAndStabilizingPreventsTheOutcome()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var mutations = server.System<MutationSystem>();
        var status = server.System<StatusEffectsSystem>();
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var human = entities.SpawnEntity("MobHuman", map.GridCoords);
            var comp = entities.GetComponent<MutatableComponent>(human);
            Assert.That(comp.MaxInstability, Is.EqualTo(100));
            Assert.That(comp.MeltDuration, Is.EqualTo(TimeSpan.FromMinutes(3)));
            mutations.AddInstability((human, comp), 99, automatic: true, predicted: false);
            Assert.That(status.HasStatusEffect(human, comp.MeltingEffect), Is.False);
            mutations.AddInstability((human, comp), 1, automatic: true, predicted: false);
            Assert.That(status.HasStatusEffect(human, comp.MeltingEffect), Is.True);
            mutations.AddInstability((human, comp), -1, automatic: true, predicted: false);
            Assert.That(status.HasStatusEffect(human, comp.MeltingEffect), Is.False);
            Assert.That(entities.EntityExists(human), Is.True, "Stabilization must prevent the lethal mutation outcome.");
            Assert.That(entities.GetComponent<MetaDataComponent>(human).EntityPrototype!.ID, Is.EqualTo("MobHuman"));
            entities.DeleteEntity(human);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NativeMutationsAreFreeForeignMutationsCostInstabilityAndConflictsAreRejected()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var mutations = server.System<MutationSystem>();
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var human = entities.SpawnEntity("MobHuman", map.GridCoords);
            var comp = entities.GetComponent<MutatableComponent>(human);
            mutations.ClearDormant(human);
            comp.Dormant.Add("MutationColdAdaptation");
            Assert.That(mutations.AddMutation(human, "MutationColdAdaptation"), Is.True);
            Assert.That(comp.TotalInstability, Is.Zero);
            Assert.That(mutations.AddMutation(human, "MutationHeatAdaptation"), Is.True);
            Assert.That(comp.TotalInstability, Is.EqualTo(mutations.AllMutations["MutationHeatAdaptation"].Instability));
            Assert.That(mutations.RemoveMutation(human, "MutationHeatAdaptation"), Is.True);
            Assert.That(comp.TotalInstability, Is.Zero);
            mutations.ClearMutations(human);
            Assert.That(mutations.AddMutation(human, "MutationEnhancedPerception"), Is.True);
            var before = comp.TotalInstability;
            Assert.That(mutations.AddMutation(human, "MutationWeakPerception"), Is.False);
            Assert.That(comp.TotalInstability, Is.EqualTo(before));
            Assert.That(mutations.HasMutation(human, "MutationWeakPerception"), Is.False);
            entities.DeleteEntity(human);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EveryNonPolymorphMutationCanBeAddedAndCleanedUp()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var mutations = server.System<MutationSystem>();
        var containers = server.System<SharedContainerSystem>();
        var map = await pair.CreateTestMap();

        foreach (var idString in GeneticsPrototypeIntegrityTest.ExpectedMutations)
        {
            EntProtoId<MutationComponent> id = idString;
            if (server.ProtoMan.Index(id).Components.ContainsKey("PolymorphMutation"))
                continue; // Covered by the round-trip test below, including sequence transfer.
            await server.WaitAssertion(() =>
            {
                var mob = entities.SpawnEntity("MobHuman", map.GridCoords);
                var comp = entities.GetComponent<MutatableComponent>(mob);
                mutations.ClearMutations(mob);
                AddRequired(id);
                Assert.That(mutations.AddMutation(mob, id), Is.True, idString);
                Assert.That(mutations.HasMutation(mob, id), Is.True, idString);
                Assert.That(mutations.AddMutation(mob, id), Is.False, $"Duplicate {id}");
                var mutation = mutations.GetMutation((mob, comp), id)!.Value;
                Assert.That(mutation.Comp.Target, Is.EqualTo(mob));
                Assert.That(containers.GetContainer(mob, comp.ContainerId).ContainedEntities, Does.Contain(mutation.Owner));
                mutations.ClearMutations(mob);
                Assert.That(comp.Mutations, Is.Empty, idString);
                Assert.That(comp.TotalInstability, Is.Zero, idString);
                entities.DeleteEntity(mob);

                void AddRequired(EntProtoId<MutationComponent> requiredBy)
                {
                    foreach (var required in mutations.AllMutations[requiredBy].Required)
                    {
                        if (mutations.HasMutation(mob, required))
                            continue;
                        AddRequired(required);
                        Assert.That(mutations.AddMutation(mob, required), Is.True, $"{requiredBy} requires {required}");
                    }
                }
            });
            await server.WaitRunTicks(1); // Process deferred entity/component removal between mutations.
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ScanDeduplicatesSequencesAndHidesUndiscoveredMutationNames()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var mutations = server.System<MutationSystem>();
        var genome = server.System<ScannedGenomeSystem>();
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var mob = entities.SpawnEntity("MobHuman", map.GridCoords);
            var comp = entities.GetComponent<MutatableComponent>(mob);
            var previous = mutations.RoundData.ToDictionary(p => p.Key, p => p.Value.Discovered);
            try
            {
                foreach (var data in mutations.RoundData.Values)
                    data.Discovered = false;
                genome.ScanGenome(mob);
                var scanned = entities.GetComponent<ScannedGenomeComponent>(mob);
                var count = scanned.Sequences.Count;
                genome.ScanGenome(mob);
                Assert.That(scanned.Sequences, Has.Count.EqualTo(count));
                Assert.That(scanned.Sequences.Select(s => s.Mutation).Distinct().Count(), Is.EqualTo(count));
                var states = new List<SequenceState>();
                genome.AddSequenceStates(mob, states);
                Assert.That(states, Has.Count.EqualTo(count));
                Assert.That(states.All(state => state.Mutation == null), Is.True);
                Assert.That(states.All(state => state.Bases.Contains('X')), Is.True);
                Assert.That(genome.GetSequence(mob, uint.MaxValue), Is.Null);

                var foreign = mutations.UnlockedMutations.First(id => !comp.Dormant.Contains(id) &&
                    !server.ProtoMan.Index(id).Components.ContainsKey("PolymorphMutation") &&
                    mutations.AllMutations[id].Required.Count == 0);
                Assert.That(mutations.AddMutation(mob, foreign), Is.True);
                Assert.That(scanned.Sequences.Any(sequence => sequence.Mutation == foreign), Is.True);
                Assert.That(mutations.RemoveMutation(mob, foreign), Is.True);
                Assert.That(scanned.Sequences.Any(sequence => sequence.Mutation == foreign), Is.False);
                Assert.That(scanned.Sequences, Has.Count.EqualTo(count));
            }
            finally
            {
                foreach (var (id, discovered) in previous)
                    mutations.RoundData[id].Discovered = discovered;
                entities.DeleteEntity(mob);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PolymorphRoundTripTransfersMutationsAndExactScannedSequences()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var mutations = server.System<MutationSystem>();
        var genome = server.System<ScannedGenomeSystem>();
        var polymorph = server.System<PolymorphSystem>();
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var human = entities.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(mutations.AddMutation(human, "MutationDwarfism"), Is.True);
            genome.ScanGenome(human);
            var original = entities.GetComponent<ScannedGenomeComponent>(human).Sequences
                .Select(s => (s.Mutation, s.Bases, s.OriginalBases)).ToArray();
            var dormant = entities.GetComponent<MutatableComponent>(human).Dormant.ToArray();
            var monkey = polymorph.PolymorphEntity(human, "MutationMonkey");
            Assert.That(monkey, Is.Not.Null);
            Assert.That(mutations.HasMutation(monkey!.Value, "MutationDwarfism"), Is.True);
            Assert.That(entities.GetComponent<ScannedGenomeComponent>(human).Sequences, Is.Empty);
            Assert.That(entities.GetComponent<ScannedGenomeComponent>(monkey.Value).Sequences
                .Select(s => (s.Mutation, s.Bases, s.OriginalBases)), Is.EquivalentTo(original));
            Assert.That(entities.GetComponent<MutatableComponent>(monkey.Value).Dormant, Is.EquivalentTo(dormant));
            Assert.That(polymorph.Revert(monkey.Value), Is.EqualTo(human));
            Assert.That(mutations.HasMutation(human, "MutationDwarfism"), Is.True);
            Assert.That(entities.GetComponent<ScannedGenomeComponent>(human).Sequences
                .Select(s => (s.Mutation, s.Bases, s.OriginalBases)), Is.EquivalentTo(original));
            mutations.ClearMutations(human);
            entities.DeleteEntity(human);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HeadRemovalAndRegrowthPreserveOriginalBrainAndEyes()
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
            Assert.That(brain, Is.Not.Null);
            Assert.That(eyes, Is.Not.Null);
            Assert.That(mutations.AddMutation(human, "MutationHeadless"), Is.True);
            Assert.That(body.GetOrgan(human, "Head"), Is.Null);
            Assert.That(server.System<Content.Shared.Mobs.Systems.MobStateSystem>().IsAlive(human), Is.True);
            Assert.That(body.GetOrgan(human, "Brain"), Is.EqualTo(brain));
            Assert.That(body.GetOrgan(human, "Eyes"), Is.EqualTo(eyes));
            Assert.That(mutations.RemoveMutation(human, "MutationHeadless"), Is.True);
            Assert.That(body.GetOrgan(human, "Head"), Is.Not.Null);
            Assert.That(server.System<Content.Shared.Mobs.Systems.MobStateSystem>().IsAlive(human), Is.True);
            Assert.That(body.GetOrgan(human, "Brain"), Is.EqualTo(brain));
            Assert.That(body.GetOrgan(human, "Eyes"), Is.EqualTo(eyes));
            entities.DeleteEntity(human);
        });
        await pair.CleanReturnAsync();
    }
}
