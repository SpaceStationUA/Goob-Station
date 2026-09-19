using Content.Pirate.Shared.ModularSuit;
using Content.Shared._Pirate.Body.Chips;
using Content.Shared._Pirate.Knowledge;
using Content.Shared._Shitmed.Cybernetics;
using Content.Shared.Emp;
using Content.Shared.Inventory;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Pirate.ModularSuit;

[TestFixture]
public sealed class ModularSuitEmpShieldIntegrationTest
{
    [TestCase("EMPShieldSuitModule", 0.3f)]
    [TestCase("EMPShieldAdvancedSuitModule", 0f)]
    public async Task ActiveShieldProtectsWearerContentsAndConsumesPower(
        string modulePrototype,
        float expectedPassiveDraw)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var containers = server.System<SharedContainerSystem>();
        var chips = server.System<OrganChipSystem>();
        var knowledge = server.System<SharedKnowledgeSystem>();
        var emp = server.System<Content.Server.Emp.EmpSystem>();
        var inventory = server.System<InventorySystem>();
        var batteries = server.System<SharedBatterySystem>();
        var modularSuits = server.System<Content.Pirate.Server.ModularSuit.ModularSuitSystem>();

        await server.WaitAssertion(() =>
        {
            var wearer = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var suit = entMan.SpawnEntity("ClothingModularControllerStandard", MapCoordinates.Nullspace);
            var module = entMan.SpawnEntity(modulePrototype, MapCoordinates.Nullspace);

            var suitComp = entMan.GetComponent<ModularSuitComponent>(suit);
#pragma warning disable RA0002 // Test setup for system-owned MODsuit runtime state.
            suitComp.Wearer = wearer;
            suitComp.Active = true;

            var moduleComp = entMan.GetComponent<ModularSuitModuleComponent>(module);
            moduleComp.IsActive = true;
#pragma warning restore RA0002
            Assert.That(moduleComp.PowerUsage, Is.EqualTo(expectedPassiveDraw).Within(0.001f));
            Assert.That(moduleComp.PowerInstanceUsage, Is.EqualTo(25f));

            var installed = new ModularSuitInstalledEvent(suit, wearer);
            entMan.EventBus.RaiseLocalEvent(module, ref installed);
            Assert.That(entMan.HasComponent<ModularSuitEmpShieldedComponent>(wearer), Is.True);

            Assert.That(chips.InstallChip(wearer, "SkillChipHeavy"), Is.True);
            var brain = knowledge.EnsureKnowledgeContainer(wearer).Owner;
            var chipContainer = entMan.GetComponent<OrganChipContainerComponent>(brain).Container!;
            Assert.That(chipContainer.ContainedEntities, Has.Count.EqualTo(1));
            var chip = chipContainer.ContainedEntities[0];

            var coreContainer = containers.GetContainer(suit, SharedModularSuitSystem.CoreContainer);
            Assert.That(coreContainer.ContainedEntities, Has.Count.EqualTo(1));
            var core = coreContainer.ContainedEntities[0];
            var coreComp = entMan.GetComponent<ModularSuitCoreComponent>(core);
            var initialCharge = coreComp.Charge;

            var pocketCell = entMan.SpawnEntity("PowerCellSmall", MapCoordinates.Nullspace);
            Assert.That(inventory.TryEquip(wearer, pocketCell, "pocket1", force: true), Is.True);
            var pocketBattery = entMan.GetComponent<BatteryComponent>(pocketCell);
            var pocketCharge = batteries.GetCharge((pocketCell, pocketBattery));

            Assert.That(emp.TryEmpEffects(pocketCell, 100f, TimeSpan.FromSeconds(10)), Is.False);
            Assert.That(batteries.GetCharge((pocketCell, pocketBattery)), Is.EqualTo(pocketCharge).Within(0.001f));
            Assert.That(coreComp.Charge, Is.EqualTo(initialCharge - 25f).Within(0.001f));

            // One interception must cover all entities in the pulse.
            Assert.That(emp.TryEmpEffects(chip, 100f, TimeSpan.FromSeconds(10)), Is.False);
            Assert.That(entMan.HasComponent<EmpDisabledComponent>(chip), Is.False);
            Assert.That(entMan.GetComponent<CyberneticsComponent>(chip).Disabled, Is.False);
            Assert.That(coreComp.Charge, Is.EqualTo(initialCharge - 25f).Within(0.001f));

            // Insufficient power must not shield the pulse.
            Assert.That(modularSuits.TryUseCoreCharge(suit, coreComp.Charge - 15f), Is.True);
            var shield = entMan.GetComponent<ModularSuitEmpShieldedComponent>(wearer);
            shield.LastEmpTick = uint.MaxValue;

            Assert.That(emp.TryEmpEffects(pocketCell, 100f, TimeSpan.FromSeconds(10)), Is.True);
            Assert.That(batteries.GetCharge((pocketCell, pocketBattery)), Is.EqualTo(pocketCharge - 100f).Within(0.001f));
            Assert.That(coreComp.Charge, Is.EqualTo(15f).Within(0.001f));
        });

        await pair.CleanReturnAsync();
    }
}
