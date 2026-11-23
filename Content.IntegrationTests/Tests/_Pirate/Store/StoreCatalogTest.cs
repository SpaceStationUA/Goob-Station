// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Client.Store.Ui;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Store.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Pirate.Store;

[TestFixture]
public sealed class StoreCatalogTest : InteractionTest
{
    [Test]
    public async Task CatalogReachesTheOpenWindowWithoutEnteringEntityState()
    {
        await SpawnTarget("PersonalAI", PlayerCoords);
        var store = STarget!.Value;
        var stores = Server.System<StoreSystem>();
        var ui = Server.System<SharedUserInterfaceSystem>();
        const string currency = "SiliconMemory";
        var credit = new Dictionary<string, FixedPoint2> { [currency] = 1 };
        FixedPoint2 initial = default;

        await Server.WaitPost(() =>
        {
            initial = SEntMan.GetComponent<StoreComponent>(store).Balance[currency];
            stores.ToggleUi(SPlayer, store);
        });
        await RunTicks(10);
        await Client.WaitAssertion(() => Assert.That(GetWindow<StoreMenu>().Balance[currency], Is.EqualTo(initial)));

        await Server.WaitAssertion(() =>
        {
            Assert.That(stores.TryAddCurrency(credit, store), Is.True);
            Assert.That(ui.TryGetUiState<BoundUserInterfaceState>(store, StoreUiKey.Key, out _), Is.False);
        });
        await RunTicks(10);
        await Client.WaitAssertion(() => Assert.That(GetWindow<StoreMenu>().Balance[currency], Is.EqualTo(initial + 1)));

        await Server.WaitPost(() => stores.CloseUi(store));
        await RunTicks(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(stores.TryAddCurrency(credit, store), Is.True);
            Assert.That(ui.TryGetUiState<BoundUserInterfaceState>(store, StoreUiKey.Key, out _), Is.False);
            stores.ToggleUi(SPlayer, store);
        });
        await RunTicks(10);
        await Client.WaitAssertion(() => Assert.That(GetWindow<StoreMenu>().Balance[currency], Is.EqualTo(initial + 2)));
    }
}
