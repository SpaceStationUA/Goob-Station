// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Linq;
using Content.Pirate.Server.Genetics;
using Content.Pirate.Shared.Genetics;
using Content.Pirate.Shared.Genetics.Console;
using Content.Pirate.Shared.Genetics.Mutations;
using Content.Pirate.Shared.Genetics.Tools;
using Content.Server.Medical;
using Content.Server.Medical.Components;
using Content.Server.Power.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Materials;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Pirate.Genetics;

[TestFixture]
public sealed class GeneticsConsoleIntegrationTest
{
    [Test]
    public async Task ScannerSequencingRejectsBadInputsAndActivatesSolvedDna()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var mutations = server.System<MutationSystem>();
        var genome = server.System<ScannedGenomeSystem>();
        var consoleSystem = server.System<GeneticsConsoleSystem>();
        var links = server.System<GeneticsScannerLinkSystem>();
        var scannerSystem = server.System<MedicalScannerSystem>();
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var console = entities.SpawnEntity("ComputerGeneticsConsole", map.GridCoords);
            var comp = entities.GetComponent<GeneticsConsoleComponent>(console);
            entities.GetComponent<ApcPowerReceiverComponent>(console).NeedsPower = false;
            var scanner = entities.SpawnEntity("MedicalScanner", map.GridCoords);
            var scannerComp = entities.GetComponent<MedicalScannerComponent>(scanner);
            var human = entities.SpawnEntity("MobHuman", map.GridCoords);
            var other = entities.SpawnEntity("MobHuman", map.GridCoords);
            var mutatable = entities.GetComponent<MutatableComponent>(human);
            mutations.ClearDormant(human);
#pragma warning disable RA0002 // Deterministic fixture setup for protected timing/genome state.
            mutatable.Dormant.Add("MutationDwarfism");
#pragma warning restore RA0002
            var data = mutations.GetRoundData(mutatable.Dormant[0])!;
            var discovered = data.Discovered;
            try
            {
                data.Discovered = false;
                links.Connect((console, entities.GetComponent<GeneticsScannerComponent>(console)), scanner);
                scannerSystem.InsertBody(scanner, human, scannerComp);
                Assert.That(consoleSystem.GetScannedMob(console), Is.EqualTo(human));
                Assert.That(consoleSystem.CanScan(console, human), Is.True);
                genome.ScanGenome(human);
                Assert.That(consoleSystem.CanScan(console, human), Is.False);
                var sequence = genome.GetSequence(human, 0)!;
                var original = sequence.Bases;
                foreach (var index in new[] { (uint) original.Length, uint.MaxValue })
                {
                    var invalid = new GeneticsConsoleSetBaseMessage(0, index, GeneticsCycle.Next)
                    { Actor = other, UiKey = GeneticsConsoleUiKey.Key };
                    Assert.DoesNotThrow(() => entities.EventBus.RaiseLocalEvent(console, invalid));
                    Assert.That(sequence.Bases, Is.EqualTo(original));
                }
                var knownIndex = (uint) original.IndexOf('X');
                var cycle = new GeneticsConsoleSetBaseMessage(0, knownIndex, GeneticsCycle.Next)
                { Actor = other, UiKey = GeneticsConsoleUiKey.Key };
                entities.EventBus.RaiseLocalEvent(console, cycle);
                Assert.That(sequence.Bases[(int) knownIndex], Is.EqualTo('A'));
                Assert.That(consoleSystem.SequenceMutation((console, comp), other, 0), Is.False);
                Assert.That(consoleSystem.SequenceMutation((console, comp), human, uint.MaxValue), Is.False);
                sequence.Bases = new string('X', MutationData.BaseCount);
                var damage = mutations.GetGeneticDamage(human);
                Assert.That(consoleSystem.SequenceMutation((console, comp), human, 0), Is.False);
                Assert.That(mutations.GetGeneticDamage(human), Is.GreaterThan(damage));
                Assert.That(data.Discovered, Is.False);
                sequence.Bases = data.Bases;
                Assert.That(consoleSystem.SequenceMutation((console, comp), human, 0), Is.True);
                Assert.That(data.Discovered, Is.True);
                Assert.That(mutations.HasMutation(human, sequence.Mutation), Is.True);
                Assert.That(consoleSystem.SequenceMutation((console, comp), human, 0), Is.False,
                    "A discovered sequence must not award research a second time.");
                scannerSystem.EjectBody(scanner, scannerComp);
                Assert.That(consoleSystem.GetScannedMob(console), Is.Null);
                Assert.That(consoleSystem.CanWorkOn(console, human), Is.False);
            }
            finally
            {
                data.Discovered = discovered;
                entities.DeleteEntity(console);
                entities.DeleteEntity(scanner);
                entities.DeleteEntity(human);
                entities.DeleteEntity(other);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PrintingConsumesExactBiomassAndRejectsCooldownEmptyDiskAndOverflow()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var disks = server.System<GeneticsDiskSystem>();
        var slots = server.System<ItemSlotsSystem>();
        var materials = server.System<SharedMaterialStorageSystem>();
        var timing = server.ResolveDependency<IGameTiming>();
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var console = entities.SpawnEntity("ComputerGeneticsConsole", map.GridCoords);
            var comp = entities.GetComponent<GeneticsConsoleComponent>(console);
            var disk = entities.SpawnEntity("GeneticsDisk", map.GridCoords);
            var diskComp = entities.GetComponent<GeneticsDiskComponent>(disk);
            var user = entities.SpawnEntity("MobHuman", map.GridCoords);
            entities.GetComponent<ApcPowerReceiverComponent>(console).NeedsPower = false;
            var slotName = entities.GetComponent<GeneticsDiskSlotComponent>(console).DiskSlot;
            Assert.That(slots.TryInsert(console, slotName, disk, user), Is.True);
            disks.SetMutation((disk, diskComp), "MutationDwarfism");
            for (uint index = 0; index < comp.Prints.Count; index++)
            {
                var print = comp.Prints[(int) index];
#pragma warning disable RA0002 // Deterministic fixture setup for protected timing/genome state.
                comp.NextPrint = timing.CurTime;
#pragma warning restore RA0002
                materials.TrySetMaterialAmount(console, "Biomass", print.Cost - 1);
                Send(index);
                Assert.That(materials.GetMaterialAmount(console, "Biomass"), Is.EqualTo(print.Cost - 1));
                Assert.That(comp.NextPrint, Is.EqualTo(timing.CurTime));

                materials.TrySetMaterialAmount(console, "Biomass", print.Cost);
                Send(uint.MaxValue);
                Assert.That(materials.GetMaterialAmount(console, "Biomass"), Is.EqualTo(print.Cost));
                Send(index);
                Assert.That(materials.GetMaterialAmount(console, "Biomass"), Is.Zero);
                Assert.That(comp.NextPrint, Is.EqualTo(timing.CurTime + comp.PrintDelay));
                var injector = entities.EntityQuery<MutatorComponent, MetaDataComponent>(true)
                    .Single(pair => pair.Item2.EntityPrototype?.ID == print.Proto.ToString());
                Assert.That(injector.Item1.Mutations.Select(id => id.ToString()), Is.EqualTo(new[] { "MutationDwarfism" }));

                materials.TrySetMaterialAmount(console, "Biomass", print.Cost);
                Send(index);
                Assert.That(materials.GetMaterialAmount(console, "Biomass"), Is.EqualTo(print.Cost), "Cooldown must prevent another print.");
                entities.DeleteEntity(injector.Item2.Owner);
            }
            disks.SetMutation((disk, diskComp), null);
#pragma warning disable RA0002 // Deterministic fixture setup for protected timing/genome state.
            comp.NextPrint = timing.CurTime;
#pragma warning restore RA0002
            var before = materials.GetMaterialAmount(console, "Biomass");
            Send(0);
            Assert.That(materials.GetMaterialAmount(console, "Biomass"), Is.EqualTo(before));
            entities.DeleteEntity(console);
            entities.DeleteEntity(user);

            void Send(uint index)
            {
                var message = new GeneticsConsolePrintMessage(index) { Actor = user, UiKey = GeneticsConsoleUiKey.Key };
                entities.EventBus.RaiseLocalEvent(console, message);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DisksKeepMutationAndUniqueEnzymeStorageMutuallyExclusive()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var disks = server.System<GeneticsDiskSystem>();
        var enzymes = server.System<UniqueEnzymesSystem>();
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var disk = entities.SpawnEntity("GeneticsDisk", map.GridCoords);
            var comp = entities.GetComponent<GeneticsDiskComponent>(disk);
            var source = entities.SpawnEntity("MobHuman", map.GridCoords);
            var target = entities.SpawnEntity("MobHuman", map.GridCoords);
            var data = enzymes.GetEnzymes(source);
            disks.SetMutation((disk, comp), "MutationDwarfism");
            disks.SetEnzymes((disk, comp), data);
            Assert.That(comp.Mutation, Is.Null);
            Assert.That(comp.Enzymes, Is.EqualTo(data));
            enzymes.ChangeEnzymes(target, data);
            Assert.That(enzymes.GetEnzymes(target), Is.EqualTo(data));
            disks.SetMutation((disk, comp), "MutationDwarfism");
            Assert.That(comp.Enzymes, Is.Null);
            var microwaved = new Content.Shared.Kitchen.BeingMicrowavedEvent(disk, null);
            entities.EventBus.RaiseLocalEvent(disk, microwaved);
            Assert.That(comp.Mutation, Is.Null);
            Assert.That(comp.Enzymes, Is.Null);
            entities.DeleteEntity(disk);
            entities.DeleteEntity(source);
            entities.DeleteEntity(target);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("GeneticsMutator", false, true)]
    [TestCase("GeneticsActivator", false, false)]
    [TestCase("GeneticsActivator", true, true)]
    [TestCase("GeneticsCleanser", true, false)]
    public async Task InjectorsRespectDormancyRemoveMutationsAndBecomeSpent(string prototype, bool dormant, bool expected)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var mutations = server.System<MutationSystem>();
        var injectors = server.System<MutatorSystem>();
        var tags = server.System<TagSystem>();
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var human = entities.SpawnEntity("MobHuman", map.GridCoords);
            mutations.ClearDormant(human);
            if (dormant)
#pragma warning disable RA0002 // Deterministic fixture setup for protected timing/genome state.
                entities.GetComponent<MutatableComponent>(human).Dormant.Add("MutationDwarfism");
#pragma warning restore RA0002
            if (prototype == "GeneticsCleanser")
                Assert.That(mutations.AddMutation(human, "MutationDwarfism"), Is.True);
            var injector = entities.SpawnEntity(prototype, map.GridCoords);
            var comp = entities.GetComponent<MutatorComponent>(injector);
#pragma warning disable RA0002 // Deterministic fixture setup for protected timing/genome state.
            comp.InjectTime = TimeSpan.Zero;
#pragma warning restore RA0002
            injectors.AddMutation(injector, "MutationDwarfism");
            injectors.StartInject((injector, comp), human, human);
            Assert.That(mutations.HasMutation(human, "MutationDwarfism"), Is.EqualTo(expected));
            Assert.That(comp.Mutations, Is.Empty);
            Assert.That(tags.HasTag(injector, "Trash"), Is.True);
            injectors.StartInject((injector, comp), human, human);
            Assert.That(mutations.HasMutation(human, "MutationDwarfism"), Is.EqualTo(expected));
            entities.DeleteEntity(injector);
            entities.DeleteEntity(human);
        });
        await pair.CleanReturnAsync();
    }
}
