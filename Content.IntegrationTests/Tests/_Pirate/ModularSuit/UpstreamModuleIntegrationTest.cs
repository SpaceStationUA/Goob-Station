// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.ModSuits.Components;
using Content.Goobstation.Shared.ModSuits.Modules;
using Content.Goobstation.Shared.ModSuits.Systems;
using Content.Pirate.Shared.ModularSuit;
using Content.Shared.Clothing.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Pirate.ModularSuit;

[TestFixture]
public sealed class UpstreamModuleIntegrationTest
{
    [Test]
    public async Task PassiveEffectUsesPirateWearerAndIsRemovedWithModule()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var (suit, wearer) = CreateSuit(entities, map.GridCoords);
            var module = Install(entities, suit, wearer, "MagneticHarnessSuitModule");
            var upstream = server.System<ModSuitSystem>();
            Assert.That(entities.HasComponent<ModSuitComponent>(suit), Is.False, "Pirate retains ownership of the suit runtime.");
            Assert.That(upstream.TryGetModuleHost(module, out var host), Is.True);
            Assert.That(host.Owner, Is.EqualTo(suit));
            Assert.That(host.Wearer, Is.EqualTo(wearer));
            Assert.That(entities.HasComponent<ModMagneticHarnessComponent>(wearer), Is.True);
            Assert.That(entities.GetComponent<ModModuleComponent>(module).Active, Is.True);
            Assert.That(entities.GetComponent<ModularSuitModuleComponent>(module).IsActive, Is.True);

            var removed = new ModularSuitRemovedEvent(suit, wearer);
            entities.EventBus.RaiseLocalEvent(module, ref removed);
            Assert.That(entities.HasComponent<ModMagneticHarnessComponent>(wearer), Is.False);
            Assert.That(entities.GetComponent<ModModuleComponent>(module).Suit, Is.Null);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task JumpJetUsesOurCoreAndEnforcesWearerAndCooldown()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var (suit, wearer) = CreateSuit(entities, map.GridCoords);
            var module = Install(entities, suit, wearer, "JumpJetSuitModule");
            var other = entities.SpawnEntity("MobHuman", map.GridCoords);
            var core = entities.GetComponent<ModularSuitCoreComponent>(
                server.System<SharedContainerSystem>().GetContainer(suit, SharedModularSuitSystem.CoreContainer).ContainedEntities[0]);
            var before = core.Charge;
            var upstream = server.System<ModSuitSystem>();
            Assert.That(upstream.TryUseModule(module, other), Is.False);
            Assert.That(core.Charge, Is.EqualTo(before));
            Assert.That(upstream.TryUseModule(module, wearer), Is.True);
            Assert.That(core.Charge, Is.EqualTo(before - 40f).Within(0.001f));
            Assert.That(upstream.TryUseModule(module, wearer), Is.False, "Cooldown must prevent a second jump.");
            Assert.That(core.Charge, Is.EqualTo(before - 40f).Within(0.001f));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PatientProjectorRetractsWhenPirateSuitLosesPower()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var (suit, wearer) = CreateSuit(entities, map.GridCoords);
            var module = Install(entities, suit, wearer, "PatientTransportSuitModule");
            Assert.That(server.System<ModSuitSystem>().TryUseModule(module, wearer), Is.True);
            var device = entities.GetComponent<ModDeviceModuleComponent>(module);
            Assert.That(device.DeviceEntity, Is.Not.Null);
            Assert.That(device.DeviceContainer.ContainedEntity, Is.Null, "Active projector is deployed into the wearer's hand.");

            var local = entities.GetComponent<ModularSuitComponent>(suit);
            server.System<Content.Pirate.Server.ModularSuit.ModularSuitSystem>().SetActive((suit, local), false);
            Assert.That(entities.GetComponent<ModModuleComponent>(module).Active, Is.False);
            Assert.That(device.DeviceContainer.ContainedEntity, Is.EqualTo(device.DeviceEntity));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ChameleonWorksUnwornAndRemovalRestoresController()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var user = entities.SpawnEntity("MobHuman", map.GridCoords);
            server.PlayerMan.SetAttachedEntity(pair.Player!, user);
            var suit = entities.SpawnEntity("ClothingModularControllerStandard", map.GridCoords);
            var module = Install(entities, suit, user, "ChameleonSuitModule");
            Assert.That(entities.HasComponent<ChameleonClothingComponent>(suit), Is.True);
            Assert.That(entities.GetComponent<ModularSuitComponent>(suit).Active, Is.False);
            Assert.That(server.System<ModSuitSystem>().TryUseModule(module, user), Is.True);

            var removed = new ModularSuitRemovedEvent(suit, user);
            entities.EventBus.RaiseLocalEvent(module, ref removed);
            Assert.That(entities.HasComponent<ChameleonClothingComponent>(suit), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    private static (EntityUid Suit, EntityUid Wearer) CreateSuit(IEntityManager entities, EntityCoordinates coordinates)
    {
        var wearer = entities.SpawnEntity("MobHuman", coordinates);
        var suit = entities.SpawnEntity("ClothingModularControllerStandard", coordinates);
        var comp = entities.GetComponent<ModularSuitComponent>(suit);
#pragma warning disable RA0002 // Set up the assembled Pirate runtime without waiting on suit deployment DoAfters.
        comp.Wearer = wearer;
        comp.Assembled = true;
        comp.Active = true;
#pragma warning restore RA0002
        var parts = entities.System<SharedContainerSystem>().GetContainer(suit, SharedModularSuitSystem.PartContainer);
        foreach (var part in new[] { "ClothingModularHelmetStandard", "ClothingModularChestPlateStandard", "ClothingModularGauntletsStandard", "ClothingModularBootsStandard" })
            entities.System<SharedContainerSystem>().Insert(entities.SpawnEntity(part, coordinates), parts);
        return (suit, wearer);
    }

    private static EntityUid Install(IEntityManager entities, EntityUid suit, EntityUid user, string prototype)
    {
        var module = entities.SpawnEntity(prototype, entities.GetComponent<TransformComponent>(suit).Coordinates);
        var container = entities.System<SharedContainerSystem>().GetContainer(suit, SharedModularSuitSystem.ModuleContainer);
        Assert.That(entities.System<SharedContainerSystem>().Insert(module, container), Is.True);
        var installed = new ModularSuitInstalledEvent(suit, user);
        entities.EventBus.RaiseLocalEvent(module, ref installed);
        return module;
    }
}
