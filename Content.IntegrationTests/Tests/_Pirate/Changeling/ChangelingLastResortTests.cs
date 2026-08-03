// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Server.Changeling;
using Content.Goobstation.Shared.Changeling.Actions;
using Content.Goobstation.Shared.Changeling.Components;
using Content.Goobstation.Shared.InternalResources.EntitySystems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Store.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Pirate.Changeling;

[TestFixture]
public sealed class ChangelingLastResortTests
{
    [Test]
    public async Task HatchRestoresChangelingStateBeforeMindTransfer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        var actions = entMan.System<SharedActionsSystem>();
        var actionContainer = entMan.System<ActionContainerSystem>();
        var changeling = entMan.System<ChangelingSystem>();
        var eggSystem = entMan.System<ChangelingEggSystem>();
        var mindSystem = entMan.System<SharedMindSystem>();
        var mobState = entMan.System<MobStateSystem>();
        var resources = entMan.System<SharedInternalResourcesSystem>();

        EntityUid ling = default;
        EntityUid corpse = default;
        EntityUid mindId = default;
        EntityUid purchasedAction = default;

        await server.WaitPost(() =>
        {
            ling = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            corpse = entMan.SpawnEntity("MobHuman", testMap.GridCoords);

            mindId = mindSystem.CreateMind(null).Owner;
            mindSystem.TransferTo(mindId, ling);

            entMan.EnsureComponent<ChangelingComponent>(ling);
            var identity = entMan.GetComponent<ChangelingIdentityComponent>(ling);
            identity.TotalAbsorbedEntities = 4;
            identity.TotalEvolutionPoints = 23;

            var chemicals = entMan.GetComponent<ChangelingChemicalComponent>(ling);
            Assert.That(chemicals.ResourceData, Is.Not.Null);
            chemicals.ResourceData!.RegenerationRate = 0f;
            resources.TryUpdateResourcesAmount(
                ling,
                chemicals.ResourceData!,
                37f - chemicals.ResourceData!.CurrentAmount);

            entMan.EnsureComponent<AugmentedEyesightComponent>(ling);

            var store = entMan.EnsureComponent<StoreComponent>(ling);
            store.Balance["EvolutionPoint"] = 7;
            store.Listings.Single(listing => listing.ID == "EvolutionMenuUtilityEyesight").PurchaseAmount = 1;

            purchasedAction = actionContainer.AddAction(mindId, "ActionToggleArmblade")!.Value;
            Assert.That(entMan.GetComponent<ActionComponent>(purchasedAction).AttachedEntity, Is.EqualTo(ling));

            var lastResort = new ActionLastResortEvent();
            changeling.OnLastResort(ling, identity, ref lastResort);
            Assert.That(lastResort.Handled, Is.True);

            var headslug = entMan.GetComponent<MindComponent>(mindId).OwnedEntity;
            Assert.That(headslug, Is.Not.Null);
            Assert.That(entMan.GetComponent<MetaDataComponent>(headslug!.Value).EntityPrototype!.ID,
                Is.EqualTo("MobHeadcrab"));

            var headslugStore = entMan.GetComponent<StoreComponent>(headslug.Value);
            Assert.That(headslugStore.Listings
                .Single(listing => listing.ID == "EvolutionMenuUtilityEyesight").PurchaseAmount, Is.EqualTo(1));
            headslugStore.Balance["EvolutionPoint"] = 5;
            headslugStore.Listings
                .Single(listing => listing.ID == "EvolutionMenuUtilityEyesight").PurchaseAmount = 2;

            entMan.EnsureComponent<AbsorbableComponent>(corpse);
            mobState.ChangeMobState(corpse, MobState.Dead);

            var headslugIdentity = entMan.GetComponent<ChangelingIdentityComponent>(headslug.Value);
            var layEgg = new StingLayEggsEvent { Target = corpse };
            changeling.OnLayEgg(headslug.Value, headslugIdentity, ref layEgg);
            Assert.That(layEgg.Handled, Is.True);

            var egg = entMan.GetComponent<ChangelingEggComponent>(corpse);
            Assert.That(egg.Active, Is.False);
            eggSystem.Cycle(corpse, egg);
            Assert.That(egg.Active, Is.True);
            eggSystem.Cycle(corpse, egg);
            Assert.That(entMan.HasComponent<ChangelingEggComponent>(corpse), Is.False);
            eggSystem.Cycle(corpse, egg);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var newBody = entMan.GetComponent<MindComponent>(mindId).OwnedEntity;
            Assert.That(newBody, Is.Not.Null);
            var uid = newBody!.Value;

            var identity = entMan.GetComponent<ChangelingIdentityComponent>(uid);
            var chemicals = entMan.GetComponent<ChangelingChemicalComponent>(uid);
            var store = entMan.GetComponent<StoreComponent>(uid);
            var action = entMan.GetComponent<ActionComponent>(purchasedAction);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID, Is.EqualTo("MobMonkey"));
                Assert.That(entMan.HasComponent<ChangelingComponent>(uid), Is.True);
                Assert.That(entMan.HasComponent<ChangelingRegenerateComponent>(uid), Is.True);
                Assert.That(entMan.HasComponent<ChangelingStasisComponent>(uid), Is.True);
                Assert.That(entMan.HasComponent<AugmentedEyesightComponent>(uid), Is.True);
                Assert.That(identity.TotalAbsorbedEntities, Is.EqualTo(4));
                Assert.That(identity.TotalEvolutionPoints, Is.EqualTo(23f).Within(0.01f));
                Assert.That(chemicals.ResourceData!.CurrentAmount, Is.EqualTo(37f).Within(0.01f));
                Assert.That(store.Balance["EvolutionPoint"].Float(), Is.EqualTo(5f));
                Assert.That(store.Listings.Single(listing => listing.ID == "EvolutionMenuUtilityEyesight").PurchaseAmount,
                    Is.EqualTo(2));
                Assert.That(action.AttachedEntity, Is.EqualTo(uid));
                Assert.That(actions.GetActions(uid).Any(entity => entity.Owner == purchasedAction), Is.True);
            });
        });

        await server.WaitPost(() =>
        {
            if (entMan.GetComponent<MindComponent>(mindId).OwnedEntity is { } body)
                entMan.DeleteEntity(body);
            if (entMan.EntityExists(ling))
                entMan.DeleteEntity(ling);
            entMan.DeleteEntity(mindId);
        });

        await pair.CleanReturnAsync();
    }
}
