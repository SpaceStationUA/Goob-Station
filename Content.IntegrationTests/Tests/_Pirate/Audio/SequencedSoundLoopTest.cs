// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Server._Pirate.Audio;
using Content.Server.Lathe;
using Content.Server.Lathe.Components;
using Content.Server.Power.Components;
using Content.Shared._Pirate.Audio;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Audio;

[TestFixture]
public sealed class SequencedSoundLoopTest
{
    private const string LatheProto = "Autolathe";
    private const string RecipeProto = "Wrench";
    private const string MidCollection = "PirateFabricatorPrintMid";
    private const int ExpectedMidCount = 22;

    [Test]
    public async Task MidCollectionIsIntactAndInOrder()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoMan = pair.Server.ProtoMan;

        await pair.Server.WaitAssertion(() =>
        {
            var collection = protoMan.Index<SoundCollectionPrototype>(MidCollection);

            Assert.That(collection.PickFiles, Has.Count.EqualTo(ExpectedMidCount));

            for (var i = 0; i < collection.PickFiles.Count; i++)
            {
                Assert.That(collection.PickFiles[i].Filename,
                    Is.EqualTo($"autolathe_mid{i + 1:00}.ogg"),
                    $"Mid clip {i} is out of sequence, the loop only chains seamlessly in file order");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EveryLatheCycleResolves()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var mapData = await pair.CreateTestMap();

        var entMan = server.EntMan;
        var protoMan = server.ProtoMan;
        var compFactory = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            var checked_ = 0;

            foreach (var proto in protoMan.EnumeratePrototypes<EntityPrototype>())
            {
                if (proto.Abstract || !proto.TryGetComponent<SequencedSoundLoopComponent>(out var loop, compFactory))
                    continue;

                checked_++;
                Assert.That(loop.MidSounds, Is.Not.Null, $"{proto.ID} has a sound cycle with no mid clips");
                Assert.That(protoMan.HasIndex<SoundCollectionPrototype>(loop.MidSounds!.Value),
                    $"{proto.ID} points at missing sound collection '{loop.MidSounds}'");
            }

            Assert.That(checked_, Is.GreaterThan(0), "No entity has a sound cycle, so this test proves nothing");

            var exosuitFab = entMan.SpawnEntity("ExosuitFabricator", mapData.GridCoords);
            Assert.That(entMan.GetComponent<SequencedSoundLoopComponent>(exosuitFab).MidSounds?.Id,
                Is.EqualTo("PirateSynthFabPrintMid"),
                "The exosuit fabricator's synthfab override was lost");

            entMan.DeleteEntity(exosuitFab);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OnlyIntendedMachinesHaveTheCycle()
    {
        string[] shouldHave =
        [
            "Autolathe", "AutolatheHyperConvection",
            "Protolathe", "ProtolatheHyperConvection",
            "Omnilathe",
            "EngineeringTechFab", "CargoTechFab", "ScienceTechFab", "ServiceTechFab",
            "MedicalTechFab", "SecurityTechFab", "AmmoTechFab", "ERTTechFab",
            "GoobYautjaTechFab", "GoobYautjaStructureYautjaMachinesAutolathe",
            "ExosuitFabricator",
        ];

        string[] shouldNotHave =
        [
            "Biofabricator", "MedicalBiofabricator", "Biogenerator", "NuclearFabricator",
            "OreProcessor", "OreProcessorIndustrial", "Sheetifier",
            "CircuitImprinter", "CircuitImprinterHyperConvection",
            "UniformPrinter", "CutterMachine", "PrinterDoc",
        ];

        await using var pair = await PoolManager.GetServerClient();
        var protoMan = pair.Server.ProtoMan;
        var compFactory = pair.Server.ResolveDependency<IComponentFactory>();

        await pair.Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var id in shouldHave)
                {
                    var proto = protoMan.Index<EntityPrototype>(id);
                    Assert.That(proto.TryGetComponent<SequencedSoundLoopComponent>(out _, compFactory),
                        $"{id} lost its print sound");
                }

                foreach (var id in shouldNotHave)
                {
                    var proto = protoMan.Index<EntityPrototype>(id);
                    Assert.That(proto.TryGetComponent<SequencedSoundLoopComponent>(out _, compFactory), Is.False,
                        $"{id} is not supposed to make print noise");
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MidSequenceWalksInOrderAndWraps()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var mapData = await pair.CreateTestMap();

        var entMan = server.EntMan;
        var protoMan = server.ProtoMan;
        var loopSystem = server.System<SequencedSoundLoopSystem>();

        var lathe = EntityUid.Invalid;
        SequencedSoundLoopComponent loop = default!;
        var midCount = 0;

        await server.WaitAssertion(() =>
        {
            lathe = entMan.SpawnEntity(LatheProto, mapData.GridCoords);
            loop = entMan.GetComponent<SequencedSoundLoopComponent>(lathe);

            Assert.Multiple(() =>
            {
                Assert.That(loop.MidSounds?.Id, Is.EqualTo(MidCollection));
                Assert.That(loop.MidLength, Is.GreaterThan(TimeSpan.Zero));
            });

            midCount = protoMan.Index<SoundCollectionPrototype>(loop.MidSounds!.Value).PickFiles.Count;
            Assert.That(midCount, Is.GreaterThan(1),
                "At least two mid clips are needed to prove ordering and wraparound");

            loopSystem.StartLoop((lathe, loop));

            Assert.Multiple(() =>
            {
                Assert.That(loop.Running);
                Assert.That(loop.LoopStarted, Is.False, "The mid loop must not begin until startLength has elapsed");
                Assert.That(loop.MidIndex, Is.Zero);
            });
        });

        var indices = new List<int>();
        for (var i = 0; i < midCount + 1; i++)
        {
            await server.WaitPost(() => loop.NextPlayTime = TimeSpan.Zero);
            await pair.RunTicksSync(1);
            await server.WaitPost(() => indices.Add(loop.MidIndex));
        }

        await server.WaitAssertion(() =>
        {
            Assert.That(loop.LoopStarted, "The mid loop never advanced");

            Assert.Multiple(() =>
            {
                for (var i = 0; i < midCount; i++)
                    Assert.That(indices[i], Is.EqualTo(i + 1), $"Mid clip {i} played out of order");

                Assert.That(indices[midCount], Is.EqualTo(1), "The mid loop did not wrap around");
            });

            loopSystem.StopLoop((lathe, loop));
            Assert.Multiple(() =>
            {
                Assert.That(loop.Running, Is.False);
                Assert.That(loop.LoopStarted, Is.False);
            });

            entMan.DeleteEntity(lathe);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CycleCutShortSkipsTheEndSoundAndStartIsIdempotent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var mapData = await pair.CreateTestMap();

        var entMan = server.EntMan;
        var loopSystem = server.System<SequencedSoundLoopSystem>();

        await server.WaitAssertion(() =>
        {
            var lathe = entMan.SpawnEntity(LatheProto, mapData.GridCoords);
            var loop = entMan.GetComponent<SequencedSoundLoopComponent>(lathe);

            Assert.That(loop.StartLength, Is.GreaterThan(TimeSpan.Zero),
                "This test needs a start flourish to cut short");

            // Count one-shot audio entities to verify end-sound emission.
            int AudioCount()
            {
                var count = 0;
                var query = entMan.AllEntityQueryEnumerator<AudioComponent>();
                while (query.MoveNext(out _, out _))
                    count++;
                return count;
            }

            loopSystem.StartLoop((lathe, loop));
            var scheduled = loop.NextPlayTime;

            loopSystem.StartLoop((lathe, loop));
            Assert.That(loop.NextPlayTime, Is.EqualTo(scheduled), "StartLoop restarted a running cycle");

            Assert.That(loop.LoopStarted, Is.False, "The mid loop should not have begun yet");
            var beforeCutShort = AudioCount();
            loopSystem.StopLoop((lathe, loop));

            Assert.Multiple(() =>
            {
                Assert.That(AudioCount(), Is.EqualTo(beforeCutShort),
                    "The end sound played even though the mid loop never started");
                Assert.That(loop.Running, Is.False);
                Assert.That(loop.LoopStarted, Is.False);
            });

            // Verify the end sound is not disabled entirely.
            loopSystem.StartLoop((lathe, loop));
            loop.LoopStarted = true;
            var beforeNormalStop = AudioCount();
            loopSystem.StopLoop((lathe, loop));

            Assert.Multiple(() =>
            {
                Assert.That(AudioCount(), Is.EqualTo(beforeNormalStop + 1),
                    "The end sound is missing when the mid loop had started");
                Assert.That(loop.Running, Is.False);
                Assert.That(loop.LoopStarted, Is.False);
            });

            entMan.DeleteEntity(lathe);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LatheProductionDrivesTheCycle()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var mapData = await pair.CreateTestMap();

        var entMan = server.EntMan;
        var protoMan = server.ProtoMan;
        var latheSystem = server.System<LatheSystem>();
        var materialStorage = server.System<SharedMaterialStorageSystem>();

        var lathe = EntityUid.Invalid;
        SequencedSoundLoopComponent loop = default!;

        const int sentinelIndex = 7;

        await server.WaitAssertion(() =>
        {
            lathe = entMan.SpawnEntity(LatheProto, mapData.GridCoords);
            entMan.RemoveComponent<ApcPowerReceiverComponent>(lathe);
            var latheComp = entMan.GetComponent<LatheComponent>(lathe);
            loop = entMan.GetComponent<SequencedSoundLoopComponent>(lathe);

            Assert.That(loop.Running, Is.False, "An idle lathe should not be making noise");

            var recipe = protoMan.Index<LatheRecipePrototype>(RecipeProto);
            Assert.That(recipe.CompleteTime, Is.GreaterThan(TimeSpan.Zero),
                "This test needs a recipe that takes time, pick another one");

            foreach (var (material, amount) in recipe.Materials)
                materialStorage.TryChangeMaterialAmount(lathe, material, amount * 2);

            Assert.That(latheSystem.TryAddToQueue(lathe, recipe, 2, latheComp), "Failed to queue");
            Assert.That(latheSystem.TryStartProducing(lathe, latheComp), "The lathe refused to print");

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<LatheProducingComponent>(lathe), "The lathe never started producing");
                Assert.That(loop.Running, "Production did not start the sound cycle");
            });

            loop.LoopStarted = true;
            loop.MidIndex = sentinelIndex;

            latheSystem.FinishProducing(lathe, latheComp);

            Assert.Multiple(() =>
            {
                Assert.That(loop.Running, "The cycle stopped between queued items");
                Assert.That(loop.MidIndex, Is.EqualTo(sentinelIndex), "The mid sequence was restarted mid-queue");
            });

            latheSystem.FinishProducing(lathe, latheComp);
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<LatheProducingComponent>(lathe), Is.False, "The lathe is still producing");
                Assert.That(loop.Running, Is.False, "The cycle outlived the lathe's queue");
            });

            entMan.DeleteEntity(lathe);
        });

        await pair.CleanReturnAsync();
    }
}
