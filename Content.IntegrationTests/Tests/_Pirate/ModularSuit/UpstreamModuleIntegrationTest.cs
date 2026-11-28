// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Linq;
using Content.Client.Popups;
using Content.Goobstation.Shared.ModSuits.Components;
using Content.Goobstation.Shared.ModSuits.Modules;
using Content.Goobstation.Shared.ModSuits.Systems;
using Content.Goobstation.Shared.Loudspeaker.Components;
using Content.Pirate.Shared.ModularSuit;
using Content.Server.Speech.Components;
using Content.Server.Chat.Systems;
using Content.Server.VoiceMask;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Inventory;
using Content.Shared.Actions.Components;
using Content.Shared.Storage;
using Content.Shared.Clothing.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Forensics.Components;
using Content.Shared.Stealth.Components;
using Content.Shared.VoiceMask;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
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

    [Test]
    public async Task CloakBreaksOnAttackOnPirateSuit()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var (suit, wearer) = CreateSuit(entities, map.GridCoords);
            var module = Install(entities, suit, wearer, "CloakSuitModule");
            Assert.That(server.System<ModSuitSystem>().TryUseModule(module, wearer), Is.True);
            Assert.That(entities.HasComponent<ModCloakedComponent>(wearer), Is.True);
            Assert.That(entities.HasComponent<StealthComponent>(wearer), Is.True);

            var attack = new MeleeAttackEvent(wearer);
            entities.EventBus.RaiseLocalEvent(wearer, ref attack);
            Assert.That(entities.GetComponent<ModModuleComponent>(module).Active, Is.False, "Attacking must break the cloak.");
            Assert.That(entities.HasComponent<ModCloakedComponent>(wearer), Is.False);
            Assert.That(entities.HasComponent<StealthComponent>(wearer), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PowerKickStartsUseAndPaysFromOurCore()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var (suit, wearer) = CreateSuit(entities, map.GridCoords);
            var module = Install(entities, suit, wearer, "PowerKickSuitModule");
            var core = GetCore(entities, suit);
            var before = core.Charge;
            var upstream = server.System<ModSuitSystem>();
            Assert.That(upstream.TryStartModuleUse(module, wearer), Is.True, "A usable module with its own action is never active.");
            Assert.That(core.Charge, Is.EqualTo(before - 45f).Within(0.001f));
            Assert.That(upstream.TryStartModuleUse(module, wearer), Is.False, "Cooldown must prevent a second kick.");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DnaLockRefusesOtherUsers()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var (suit, wearer) = CreateSuit(entities, map.GridCoords);
            var module = Install(entities, suit, wearer, "DnaLockSuitModule");
            var other = entities.SpawnEntity("MobHuman", map.GridCoords);
            var pirate = server.System<Content.Pirate.Server.ModularSuit.ModularSuitSystem>();
            var upstream = server.System<ModSuitSystem>();
            var local = entities.GetComponent<ModularSuitComponent>(suit);
            var dna = entities.GetComponent<DnaComponent>(wearer).DNA;
            Assert.That(dna, Is.Not.Null);
            Assert.That(pirate.CanAccess((suit, local), other), Is.True, "An unlocked suit accepts anyone.");

            Assert.That(upstream.TryUseModule(module, wearer), Is.True);
            Assert.That(entities.GetComponent<ModDnaLockModuleComponent>(module).LockedDna, Is.EqualTo(dna));
            Assert.That(pirate.CanAccess((suit, local), wearer), Is.True);
            Assert.That(pirate.CanAccess((suit, local), other), Is.False);

#pragma warning disable RA0002 // A stranger puts the locked suit on.
            local.Wearer = other;
#pragma warning restore RA0002
            Assert.That(upstream.TryUseModule(module, other), Is.False, "A stranger must not unlock the suit.");
            Assert.That(entities.GetComponent<ModDnaLockModuleComponent>(module).LockedDna, Is.EqualTo(dna));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DnaLockActionSendsFeedbackToWearer()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid wearer = default;
        EntityUid module = default;
        NetEntity action = default;
        string dna = null;

        await server.WaitAssertion(() =>
        {
            var (suit, user) = CreateSuit(entities, map.GridCoords);
            wearer = user;
            server.PlayerMan.SetAttachedEntity(pair.Player!, wearer);
            Assert.That(server.System<InventorySystem>().TryEquip(wearer, suit, "back", silent: true, force: true), Is.True);
            dna = entities.GetComponent<DnaComponent>(wearer).DNA;
            Assert.That(dna, Is.Not.Null);
            module = Install(entities, suit, wearer, "DnaLockSuitModule");
            action = entities.GetNetEntity(entities.GetComponent<ModModuleComponent>(module).ActionEntity!.Value);
        });
        await pair.RunTicksSync(10);

        async Task UseLock(string popup, string expectedDna)
        {
            await client.WaitPost(() => client.EntMan.RaisePredictiveEvent(new RequestPerformActionEvent(action)));
            await pair.RunTicksSync(10);
            await server.WaitAssertion(() => Assert.That(entities.GetComponent<ModDnaLockModuleComponent>(module).LockedDna,
                Is.EqualTo(expectedDna)));
            await client.WaitAssertion(() => Assert.That(client.System<PopupSystem>().WorldLabels.Select(label => label.Text),
                Does.Contain(Loc.GetString(popup))));
        }

        await UseLock("modsuit-dna-locked", dna);
        await client.WaitAssertion(() => Assert.That(client.EntMan.EntityQuery<AudioComponent>()
            .Any(audio => audio.FileName == "/Audio/Machines/id_swipe.ogg"), Is.True));
        await UseLock("modsuit-dna-unlocked", null);
        await server.WaitPost(() => entities.RemoveComponent<DnaComponent>(wearer));
        await UseLock("modsuit-dna-none", null);
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HatStabilizerAddsHatSlotToOurHelmet()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var (suit, wearer) = CreateSuit(entities, map.GridCoords);
            Assert.That(server.System<ModSuitSystem>().TryGetSuitModuleHost(suit, out var host), Is.True);
            Assert.That(host.PartEntities.TryGetValue("head", out var helmet), Is.True, "The bridge maps our helmet to the upstream head slot.");
            var module = Install(entities, suit, wearer, "HatStabilizerSuitModule");
            var itemSlots = server.System<ItemSlotsSystem>();
            Assert.That(entities.TryGetComponent<ModHatHolderComponent>(helmet, out var holder), Is.True);
            Assert.That(itemSlots.TryGetSlot(helmet, holder!.SlotId, out var slot), Is.True);

            var hat = entities.SpawnEntity("ClothingHeadHatBeret", map.GridCoords);
            var spareHelmet = entities.SpawnEntity("ClothingModularHelmetStandard", map.GridCoords);
            Assert.That(itemSlots.CanInsert(helmet, hat, wearer, slot!), Is.True, "Our helmet is the slot owner and must still accept hats.");
            Assert.That(itemSlots.CanInsert(helmet, spareHelmet, wearer, slot!), Is.False, "Our suit parts are not hats.");

            var removed = new ModularSuitRemovedEvent(suit, wearer);
            entities.EventBus.RaiseLocalEvent(module, ref removed);
            Assert.That(entities.HasComponent<ModHatHolderComponent>(helmet), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdrenalineAndMedbeamRunOnPirateSuit()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var upstream = server.System<ModSuitSystem>();

            var (boostSuit, boosted) = CreateSuit(entities, map.GridCoords);
            var boost = Install(entities, boostSuit, boosted, "AdrenalineBoostSuitModule");
            var core = GetCore(entities, boostSuit);
            var before = core.Charge;
            Assert.That(upstream.TryUseModule(boost, boosted), Is.True);
            Assert.That(core.Charge, Is.EqualTo(before - 90f).Within(0.001f));

            var (medSuit, medic) = CreateSuit(entities, map.GridCoords);
            var medbeam = Install(entities, medSuit, medic, "MedbeamSuitModule");
            Assert.That(upstream.TryUseModule(medbeam, medic), Is.True);
            var device = entities.GetComponent<ModDeviceModuleComponent>(medbeam);
            Assert.That(device.DeviceEntity, Is.Not.Null);
            Assert.That(device.DeviceContainer.ContainedEntity, Is.Null, "The beam gun is deployed into the medic's hand.");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SphereVoiceUsesWearerOrLocalizedOverride()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var wearer = entities.SpawnEntity("MobHuman", map.GridCoords);
            var suit = entities.SpawnEntity("ClothingModsuitCorporate", map.GridCoords);
            Assert.That(server.System<InventorySystem>().TryEquip(wearer, suit, "back", silent: true, force: true), Is.True);
            var modSuit = entities.GetComponent<ModSuitComponent>(suit);
            var modules = server.System<ModSuitSystem>();
            var sphere = modSuit.ModulesContainer.ContainedEntities.Single(entities.HasComponent<ModSphereTransformModuleComponent>);
            modules.SetModuleActive((suit, modSuit), (sphere, entities.GetComponent<ModModuleComponent>(sphere)), true);
            Assert.That(entities.HasComponent<ModSphereFormComponent>(wearer), Is.True);

            string Say()
            {
                var spoken = new TransformSpeakerNameEvent(wearer, entities.GetComponent<MetaDataComponent>(wearer).EntityName);
                entities.EventBus.RaiseLocalEvent(wearer, spoken);
                return server.System<ChatSystem>().WrapPublicMessage(wearer, spoken.VoiceName, "Привіт");
            }
            Assert.That(Say(), Does.Contain(entities.GetComponent<MetaDataComponent>(wearer).EntityName));
            var infiltrator = entities.SpawnEntity("ModsuitModuleInfiltrator", map.GridCoords);
            modules.SetModuleActive((suit, modSuit), (infiltrator, entities.GetComponent<ModModuleComponent>(infiltrator)), true);
            var localization = server.ResolveDependency<ILocalizationManager>();
            var original = localization.DefaultCulture;
            try
            {
                localization.SetCulture(CultureInfo.GetCultureInfo("uk-UA"));
                Assert.That(Say(), Does.Contain(Loc.GetString("voice-mask-default-name-override")));
            }
            finally
            {
                localization.SetCulture(original!);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InfiltratorWorksLikeVoiceMask()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        var inventory = server.System<InventorySystem>();
        var ui = server.System<SharedUserInterfaceSystem>();
        EntityUid suit = default;
        EntityUid wearer = default;
        EntityUid module = default;
        NetEntity netAction = default;
        await server.WaitAssertion(() =>
        {
            (suit, wearer) = CreateSuit(entities, map.GridCoords);
            server.PlayerMan.SetAttachedEntity(pair.Player!, wearer);
            module = Install(entities, suit, wearer, "InfiltratorSuitModule");
            Assert.That(inventory.TryEquip(wearer, suit, "back", silent: true, force: true), Is.True);
            Assert.That(entities.HasComponent<VoiceOverrideComponent>(wearer), Is.False, "The fixed upstream voice is replaced.");

            var mask = entities.GetComponent<VoiceMaskComponent>(module);
            Assert.That(mask.ActionEntity, Is.Not.Null);
            var action = entities.GetComponent<ActionComponent>(mask.ActionEntity!.Value);
            Assert.That(action.AttachedEntity, Is.EqualTo(wearer));
            Assert.That(action.Container, Is.EqualTo(module), "The action opens the voice mask menu of the module.");
            netAction = entities.GetNetEntity(mask.ActionEntity.Value);

            var other = entities.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(ui.TryOpenUi(module, VoiceMaskUIKey.Key, other), Is.False);
        });
        await pair.RunTicksSync(10);
        await client.WaitPost(() => client.EntMan.RaisePredictiveEvent(new RequestPerformActionEvent(netAction)));
        await pair.RunTicksSync(10);
        await client.WaitAssertion(() =>
        {
            var clientModule = pair.ToClientUid(module);
            var clientUi = client.System<SharedUserInterfaceSystem>();
            Assert.That(clientUi.TryGetOpenUi(clientModule, VoiceMaskUIKey.Key, out _), Is.True);
            clientUi.ClientSendUiMessage(clientModule, VoiceMaskUIKey.Key, new VoiceMaskChangeNameMessage("Agent Orange"));
        });
        await pair.RunTicksSync(10);
        await server.WaitAssertion(() =>
        {
            Assert.That(ui.IsUiOpen(module, VoiceMaskUIKey.Key, wearer), Is.True);
            var spoken = new TransformSpeakerNameEvent(wearer, "Real Name");
            entities.EventBus.RaiseLocalEvent(wearer, spoken);
            Assert.That(spoken.VoiceName, Is.EqualTo("Agent Orange"));

            var local = entities.GetComponent<ModularSuitComponent>(suit);
            server.System<Content.Pirate.Server.ModularSuit.ModularSuitSystem>().SetActive((suit, local), false);
            var unmasked = new TransformSpeakerNameEvent(wearer, "Real Name");
            entities.EventBus.RaiseLocalEvent(wearer, unmasked);
            Assert.That(unmasked.VoiceName, Is.EqualTo("Real Name"), "An inactive module does not change the voice.");

            Assert.That(inventory.TryUnequip(wearer, "back", silent: true, force: true), Is.True);
            Assert.That(ui.IsUiOpen(module, VoiceMaskUIKey.Key, wearer), Is.False);
            Assert.That(ui.TryOpenUi(module, VoiceMaskUIKey.Key, wearer), Is.False);

            Assert.That(inventory.TryEquip(wearer, suit, "back", silent: true, force: true), Is.True);
            Assert.That(ui.TryOpenUi(module, VoiceMaskUIKey.Key, wearer), Is.True);
            var containers = server.System<SharedContainerSystem>();
            var modules = containers.GetContainer(suit, SharedModularSuitSystem.ModuleContainer);
            Assert.That(containers.Remove(module, modules), Is.True);
            var removed = new ModularSuitRemovedEvent(suit, wearer);
            entities.EventBus.RaiseLocalEvent(module, ref removed);
            Assert.That(ui.IsUiOpen(module, VoiceMaskUIKey.Key, wearer), Is.False);
            Assert.That(ui.TryOpenUi(module, VoiceMaskUIKey.Key, wearer), Is.False);
            var mask = entities.GetComponent<VoiceMaskComponent>(module);
            var action = entities.GetComponent<ActionComponent>(mask.ActionEntity!.Value);
            Assert.That(action.AttachedEntity, Is.Null, "Removing the module takes the action away.");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CandelaLaysBeaconFromOurStorageModule()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        var containers = server.System<SharedContainerSystem>();
        EntityUid beacon = default;
        await server.WaitAssertion(() =>
        {
            var (suit, wearer) = CreateSuit(entities, map.GridCoords);
            var storage = Install(entities, suit, wearer, "StorageSuitModule");
            beacon = entities.SpawnEntity("MarkerBeacon", map.GridCoords);
            Assert.That(containers.Insert(beacon, entities.GetComponent<StorageComponent>(storage).Container), Is.True);
            var module = Install(entities, suit, wearer, "CandelaSpoolSuitModule");
            Assert.That(server.System<ModSuitSystem>().TryUseModule(module, wearer), Is.True);
        });
        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(containers.IsEntityInContainer(beacon), Is.False, "The beacon is taken from our storage module.");
            Assert.That(entities.GetComponent<TransformComponent>(beacon).Anchored, Is.True);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StamperGrantsSelectorAndDeploysStamp()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var (suit, wearer) = CreateSuit(entities, map.GridCoords);
            var module = Install(entities, suit, wearer, "StamperSuitModule");
            var selector = entities.GetComponent<ModDeviceSelectorComponent>(module);
            Assert.That(selector.SelectActionEntity, Is.Not.Null);
            Assert.That(entities.GetComponent<ActionComponent>(selector.SelectActionEntity!.Value).AttachedEntity, Is.EqualTo(wearer),
                "The select action is granted to the wearer of our suit.");

            Assert.That(server.System<ModSuitSystem>().TryUseModule(module, wearer), Is.True);
            var device = entities.GetComponent<ModDeviceModuleComponent>(module);
            Assert.That(device.DeviceEntity, Is.Not.Null);
            Assert.That(device.DeviceContainer.ContainedEntity, Is.Null, "The stamp is deployed into the wearer's hand.");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task JointTorsionChargesOurCore()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var (suit, wearer) = CreateSuit(entities, map.GridCoords);
            var module = Install(entities, suit, wearer, "JointTorsionSuitModule");
            Assert.That(entities.GetComponent<ModModuleComponent>(module).Active, Is.True, "Passive generator runs on an active suit.");

            var core = GetCore(entities, suit);
            var pirate = server.System<Content.Pirate.Server.ModularSuit.ModularSuitSystem>();
            Assert.That(pirate.TryUseCoreCharge(suit, 30f), Is.True);
            var spent = core.Charge;

            var upstream = server.System<ModSuitSystem>();
            Assert.That(upstream.TryGetSuitModuleHost(suit, out var host), Is.True);
            upstream.AddCharge(host, 10f);
            Assert.That(core.Charge, Is.EqualTo(spent + 10f).Within(0.001f));
            upstream.AddCharge(host, 1000f);
            Assert.That(core.Charge, Is.EqualTo(core.MaxCharge).Within(0.001f), "Charge is capped at the core capacity.");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MegaphoneBulwarkAndEmpPulseRunOnPirateSuit()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var upstream = server.System<ModSuitSystem>();

            var (loudSuit, speaker) = CreateSuit(entities, map.GridCoords);
            var megaphone = Install(entities, loudSuit, speaker, "MegaphoneSuitModule");
            Assert.That(upstream.TryUseModule(megaphone, speaker), Is.True);
            Assert.That(entities.HasComponent<LoudspeakerHolderComponent>(speaker), Is.True);

            var (wallSuit, guard) = CreateSuit(entities, map.GridCoords);
            Install(entities, wallSuit, guard, "BulwarkSuitModule");
            Assert.That(entities.HasComponent<ModShoveBlockerComponent>(guard), Is.True);

            var (empSuit, agent) = CreateSuit(entities, map.GridCoords);
            var pulse = Install(entities, empSuit, agent, "EmpPulseSuitModule");
            var core = GetCore(entities, empSuit);
            var before = core.Charge;
            Assert.That(upstream.TryUseModule(pulse, agent), Is.True);
            Assert.That(core.Charge, Is.EqualTo(before - 60f).Within(0.001f));
        });
        await pair.CleanReturnAsync();
    }

    private static ModularSuitCoreComponent GetCore(IEntityManager entities, EntityUid suit)
    {
        return entities.GetComponent<ModularSuitCoreComponent>(
            entities.System<SharedContainerSystem>().GetContainer(suit, SharedModularSuitSystem.CoreContainer).ContainedEntities[0]);
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
