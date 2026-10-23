// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Linq;
using Content.Pirate.Client.Genetics.UI;
using Content.Pirate.Common.Genetics.Medical;
using Content.Pirate.Server.Genetics;
using Content.Pirate.Shared.Genetics;
using Content.Pirate.Shared.Genetics.Console;
using Content.Pirate.Shared.Genetics.Mutations;
using Content.Server.Medical;
using Content.Server.Medical.Components;
using Content.Server.Power.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Tag;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Pirate.Genetics;

[TestFixture]
public sealed class GeneticsConsoleStateTest
{
    [Test]
    public async Task PublishedSequencesRemainIndependentAcrossConsolesHandheldsAndPrintouts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var ui = server.System<SharedUserInterfaceSystem>();
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var first = CreateScanner(entities, map.GridCoords, "MutationDwarfism");
            Assert.That(ui.TryGetUiState<GeneticsConsoleState>(first.Console, GeneticsConsoleUiKey.Key, out var firstState), Is.True);
            var expectedFirst = firstState!.Sequences.ToArray();
            Assert.That(expectedFirst, Has.Length.EqualTo(1));

            var second = CreateScanner(entities, map.GridCoords, "MutationStimmed");
            Assert.That(ui.TryGetUiState<GeneticsConsoleState>(second.Console, GeneticsConsoleUiKey.Key, out var secondState), Is.True);
            var expectedSecond = secondState!.Sequences.ToArray();
            Assert.That(secondState.Sequences, Is.Not.SameAs(firstState.Sequences));
            Assert.That(expectedSecond[0].Number, Is.Not.EqualTo(expectedFirst[0].Number));
            Assert.That(firstState.Sequences, Is.EqualTo(expectedFirst));

            var handheld = entities.SpawnEntity("HandheldGeneticsScanner", map.GridCoords);
            var inserted = new ScannerInsertedEvent(handheld, first.Patient);
            entities.EventBus.RaiseLocalEvent(handheld, ref inserted);
            Assert.That(ui.TryGetUiState<GeneticsConsoleState>(handheld, GeneticsConsoleUiKey.Key, out var handheldState), Is.True);
            Assert.That(handheldState!.Sequences, Is.EqualTo(expectedFirst));
            entities.EventBus.RaiseLocalEvent(handheld, new GeneticsPrintScanMessage
            {
                Actor = second.Patient, UiKey = GeneticsConsoleUiKey.Key
            });
            Assert.That(entities.GetComponent<GeneticsPrintoutComponent>(handheld).NextPrint,
                Is.GreaterThan(server.ResolveDependency<IGameTiming>().CurTime));
            Assert.That(secondState.Sequences, Is.EqualTo(expectedSecond));

            server.System<MedicalScannerSystem>().EjectBody(second.Scanner,
                entities.GetComponent<MedicalScannerComponent>(second.Scanner));
            Assert.That(ui.TryGetUiState<GeneticsConsoleState>(second.Console, GeneticsConsoleUiKey.Key, out var emptyState), Is.True);
            Assert.That(emptyState!.Sequences, Is.Empty);
            Assert.That(firstState.Sequences, Is.EqualTo(expectedFirst));
            Assert.That(secondState.Sequences, Is.EqualTo(expectedSecond));
            Assert.That(handheldState.Sequences, Is.EqualTo(expectedFirst));

            foreach (var uid in new[] { first.Console, second.Console, first.Scanner, second.Scanner, first.Patient, second.Patient, handheld })
                entities.DeleteEntity(uid);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InstantScansAndEnzymeApplicationLeaveTheConsoleUsable()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var scanner = CreateScanner(entities, map.GridCoords, "MutationDwarfism", scanned: false);
            var user = entities.SpawnEntity("MobHuman", map.GridCoords);
            server.System<TagSystem>().AddTag(user, "InstantDoAfters");
            var consoleSystem = server.System<GeneticsConsoleSystem>();
            var comp = entities.GetComponent<GeneticsScannerComponent>(scanner.Console);
            entities.EventBus.RaiseLocalEvent(scanner.Console, new GeneticsConsoleScanMessage
            {
                Actor = user, UiKey = GeneticsConsoleUiKey.Key
            });
            Assert.That(server.System<ScannedGenomeSystem>().IsScanned(scanner.Patient), Is.True);
            Assert.That(comp.Busy, Is.False);
            Assert.That(consoleSystem.CanWorkOn(scanner.Console, scanner.Patient), Is.True);

            var disk = entities.SpawnEntity("GeneticsDisk", map.GridCoords);
            var enzymes = server.System<UniqueEnzymesSystem>().GetEnzymes(user);
            server.System<GeneticsDiskSystem>().SetEnzymes((disk, entities.GetComponent<GeneticsDiskComponent>(disk)), enzymes);
            var slot = entities.GetComponent<GeneticsDiskSlotComponent>(scanner.Console).DiskSlot;
            Assert.That(server.System<ItemSlotsSystem>().TryInsert(scanner.Console, slot, disk, user), Is.True);
#pragma warning disable RA0002 // Exercise synchronous completion after the enzyme cooldown has expired.
            entities.GetComponent<GeneticsConsoleEnzymesComponent>(scanner.Console).NextApply = server.ResolveDependency<IGameTiming>().CurTime;
#pragma warning restore RA0002
            entities.EventBus.RaiseLocalEvent(scanner.Console, new GeneticsConsoleApplyEnzymesMessage
            {
                Actor = user, UiKey = GeneticsConsoleUiKey.Key
            });
            Assert.That(server.System<UniqueEnzymesSystem>().GetEnzymes(scanner.Patient), Is.EqualTo(enzymes));
            Assert.That(comp.Busy, Is.False);
            Assert.That(consoleSystem.CanWorkOn(scanner.Console, scanner.Patient), Is.True);
            foreach (var uid in new[] { scanner.Console, scanner.Scanner, scanner.Patient, user })
                entities.DeleteEntity(uid);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SequencingAndCombiningUseTheirOwnConfiguredDelays(bool combine)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        (EntityUid Console, EntityUid Patient, EntityUid Scanner) scanner = default;
        EntityUid user = default;
        MutationData data = null;
        var discovered = false;
        Content.Shared.DoAfter.DoAfter pending = null;
        try
        {
            await server.WaitAssertion(() =>
            {
                scanner = CreateScanner(entities, map.GridCoords, "MutationDwarfism");
                user = entities.SpawnEntity("MobHuman", map.GridCoords);
                var comp = entities.GetComponent<GeneticsConsoleComponent>(scanner.Console);
#pragma warning disable RA0002 // Set deliberately different configured operation durations.
                comp.SequenceDelay = TimeSpan.FromSeconds(7);
                comp.CombineDelay = TimeSpan.FromSeconds(13);
#pragma warning restore RA0002
                data = server.System<MutationSystem>().GetRoundData("MutationDwarfism")!;
                discovered = data.Discovered;
                data.Discovered = false;
                var disk = entities.SpawnEntity("GeneticsDisk", map.GridCoords);
                server.System<GeneticsDiskSystem>().SetMutation((disk, entities.GetComponent<GeneticsDiskComponent>(disk)), "MutationStimmed");
                var slot = entities.GetComponent<GeneticsDiskSlotComponent>(scanner.Console).DiskSlot;
                Assert.That(server.System<ItemSlotsSystem>().TryInsert(scanner.Console, slot, disk, user), Is.True);
                BoundUserInterfaceMessage message = combine
                    ? new GeneticsConsoleCombineMessage(0)
                    : new GeneticsConsoleSequenceMessage(0);
                message.Actor = user;
                message.UiKey = GeneticsConsoleUiKey.Key;
                entities.EventBus.RaiseLocalEvent(scanner.Console, (object) message);
                pending = entities.GetComponent<DoAfterComponent>(user).DoAfters.Values.Single();
                Assert.That(pending.Args.Delay, Is.EqualTo(combine ? comp.CombineDelay : comp.SequenceDelay));
                Assert.That(pending.Args.Target, Is.EqualTo(scanner.Console));
                Assert.That(entities.GetComponent<GeneticsScannerComponent>(scanner.Console).Busy, Is.True);
                server.System<MedicalScannerSystem>().EjectBody(scanner.Scanner,
                    entities.GetComponent<MedicalScannerComponent>(scanner.Scanner));
            });
            await pair.RunTicksSync(2);
            await server.WaitAssertion(() =>
            {
                Assert.That(pending.Cancelled, Is.True, "Removing the patient must cancel console operations.");
                Assert.That(entities.GetComponent<GeneticsScannerComponent>(scanner.Console).Busy, Is.False);
                Assert.That(data.Discovered, Is.False);
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                if (data != null)
                    data.Discovered = discovered;
                foreach (var uid in new[] { scanner.Console, scanner.Scanner, scanner.Patient, user })
                {
                    if (entities.EntityExists(uid))
                        entities.DeleteEntity(uid);
                }
            });
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ScanningAfterCooldownEnablesScrambleWithoutReopeningTheWindow()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.EntMan;
            var console = entities.SpawnEntity("ComputerGeneticsConsole", MapCoordinates.Nullspace);
            var human = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var scanner = entities.GetComponent<GeneticsScannerComponent>(console);
#pragma warning disable RA0002 // Simulate the replicated scanner state after its cooldown has expired.
            scanner.Scanner = console;
            scanner.ScannedMob = human;
            entities.GetComponent<GeneticsConsoleComponent>(console).NextScramble = TimeSpan.Zero;
#pragma warning restore RA0002
            using (var window = new GeneticsConsoleWindow())
            {
                window.SetEntity(console);
                var button = window.FindControl<Button>("ScrambleButton");
                Assert.That(button.Disabled, Is.True);
                entities.AddComponent<ScannedGenomeComponent>(human);
                window.SetEntity(console);
                Assert.That(button.Disabled, Is.False);
                entities.RemoveComponent<ScannedGenomeComponent>(human);
                window.SetEntity(console);
                Assert.That(button.Disabled, Is.True);
            }
            entities.DeleteEntity(console);
            entities.DeleteEntity(human);
        });
        await pair.CleanReturnAsync();
    }

    private static (EntityUid Console, EntityUid Patient, EntityUid Scanner) CreateScanner(
        IEntityManager entities, EntityCoordinates coordinates, EntProtoId<MutationComponent> mutation, bool scanned = true)
    {
        var console = entities.SpawnEntity("ComputerGeneticsConsole", coordinates);
        var power = entities.GetComponent<ApcPowerReceiverComponent>(console);
        power.NeedsPower = false;
        power.Powered = true;
        var scanner = entities.SpawnEntity("MedicalScanner", coordinates);
        var patient = entities.SpawnEntity("MobHuman", coordinates);
        entities.System<MutationSystem>().ClearDormant(patient);
#pragma warning disable RA0002 // Give each patient a deterministic, distinct genome.
        entities.GetComponent<MutatableComponent>(patient).Dormant.Add(mutation);
#pragma warning restore RA0002
        if (scanned)
            entities.System<ScannedGenomeSystem>().ScanGenome(patient);
        entities.System<GeneticsScannerLinkSystem>().Connect((console, entities.GetComponent<GeneticsScannerComponent>(console)), scanner);
        entities.System<MedicalScannerSystem>().InsertBody(scanner, patient, entities.GetComponent<MedicalScannerComponent>(scanner));
        return (console, patient, scanner);
    }
}
