// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Tests.Helpers;
using Content.Pirate.Common.Genetics.Movement;
using Content.Pirate.Shared.Genetics.Mutations;
using Content.Pirate.Shared.Genetics.Trigger;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Moq;
using Robust.Shared.GameObjects;
using Robust.Shared.Random;

namespace Content.IntegrationTests.Tests._Pirate.Genetics;

[TestFixture]
public sealed class GeneticsSequenceAndMovementTest
{
    public sealed class GeneticsFootstepListener : TestListenerSystem<FootStepEvent>;

    [TestCase(false)]
    [TestCase(true)]
    public async Task SequenceMaskingUsesComplementaryPositionsOnTheTwoStrands(bool wholePairs)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var mutations = server.System<MutationSystem>();
        var genome = server.System<ScannedGenomeSystem>();
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var human = entities.SpawnEntity("MobHuman", map.GridCoords);
            var scanned = entities.AddComponent<ScannedGenomeComponent>(human);
            var data = mutations.GetRoundData("MutationDwarfism")!;
            var discovered = data.Discovered;
            var randomField = typeof(ScannedGenomeSystem).GetField("_random", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var originalRandom = randomField.GetValue(genome);
            var random = new Mock<IRobustRandom>(MockBehavior.Strict);
            random.Setup(r => r.Next(-2, 2)).Returns(0);
            random.SetupSequence(r => r.Next(0, MutationData.PairCount)).Returns(3).Returns(7);
            if (wholePairs)
                random.SetupSequence(r => r.NextDouble()).Returns(0).Returns(0);
            else
                random.SetupSequence(r => r.NextDouble()).Returns(0.9).Returns(0.1).Returns(0.9);
            try
            {
                data.Discovered = false;
                randomField.SetValue(genome, random.Object);
#pragma warning disable RA0002 // Exercise fixed masking difficulties without changing any mutation prototype.
                var definition = new MutationComponent { Difficulty = wholePairs ? 4 : 2 };
#pragma warning restore RA0002
                genome.TryAddSequence((human, scanned), "MutationDwarfism", definition);
                var sequence = scanned.Sequences.Single();
                var hidden = Enumerable.Range(0, MutationData.BaseCount).Where(i => sequence.Bases[i] == 'X').ToArray();
                Assert.That(hidden, Is.EquivalentTo(wholePairs ? new[] { 3, 19, 7, 23 } : new[] { 3, 23 }));
                Assert.That(sequence.OriginalBases, Is.EqualTo(sequence.Bases));
                foreach (var i in Enumerable.Range(0, MutationData.BaseCount).Except(hidden))
                    Assert.That(sequence.Bases[i], Is.EqualTo(data.Bases[i]));
            }
            finally
            {
                randomField.SetValue(genome, originalRandom);
                data.Discovered = discovered;
                entities.DeleteEntity(human);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MutationWalkingTriggersRequireCompletedSteps()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var mutations = server.System<MutationSystem>();
        var moverSystem = server.System<SharedMoverController>();
        var transform = server.System<SharedTransformSystem>();
        var listener = server.System<GeneticsFootstepListener>();
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var human = entities.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(mutations.AddMutation(human, "MutationTwoLeftFeet"), Is.True);
            var mutation = mutations.GetMutation((human, entities.GetComponent<MutatableComponent>(human)), "MutationTwoLeftFeet")!.Value;
            entities.GetComponent<GeneticsEffectsOnTriggerComponent>(mutation).Effects.Single().Probability = 0;
            entities.EnsureComponent<TestListenerComponent>(human);
            entities.EnsureComponent<TestListenerComponent>(mutation);
            var mover = entities.GetComponent<InputMoverComponent>(human);
            var steps = entities.GetComponent<MobMoverComponent>(human);
            var xform = entities.GetComponent<TransformComponent>(human);
            steps.LastPosition = xform.Coordinates;
            steps.StepSoundDistance = 0;
            steps.StepSoundMoveDistanceRunning = 1;
            steps.StepSoundMoveDistanceWalking = 1;
            var trySound = typeof(SharedMoverController).GetMethod("TryGetSound", BindingFlags.Instance | BindingFlags.NonPublic)!;

            // This is the movement-tick path, including repeated attempts against a wall.
            TickAt(0, 60);
            TickAt(0.4f, 20);
            Assert.That(listener.Count(human), Is.Zero);
            Assert.That(listener.Count(mutation), Is.Zero);
            TickAt(1.05f, 1);
            Assert.That(listener.Count(human), Is.EqualTo(1));
            Assert.That(listener.Count(mutation), Is.EqualTo(1));
            TickAt(1.05f, 60);
            TickAt(1.9f, 1);
            Assert.That(listener.Count(mutation), Is.EqualTo(1));
            TickAt(2.05f, 1);
            Assert.That(listener.Count(human), Is.EqualTo(2));
            Assert.That(listener.Count(mutation), Is.EqualTo(2));
            entities.DeleteEntity(human);

            void TickAt(float distance, int ticks)
            {
                transform.SetCoordinates(human, map.GridCoords.Offset(new Vector2(distance, 0)));
                for (var tick = 0; tick < ticks; tick++)
                    trySound.Invoke(moverSystem, new object[] { false, human, mover, steps, xform, null, null });
            }
        });
        await pair.CleanReturnAsync();
    }
}
