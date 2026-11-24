// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Client._Impstation.Uplink;
using Content.Goobstation.Client.Store.UI;
using Content.Goobstation.UIKit.UserInterface.Controls.NodeTree;
using Content.Client.Store.Ui;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Store.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Input;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Pirate.Store;

[TestFixture]
public sealed class StoreCatalogTest : InteractionTest
{
    [Test]
    public async Task SyndicateUplinkShowsItsTelecrystalsAndListings()
    {
        var uplink = await SpawnTarget("BaseUplinkRadio350TC", PlayerCoords);
        await Pickup(uplink);
        await PressKey(ContentKeyFunctions.UseItemInHand);
        await RunTicks(10);
        await Client.WaitAssertion(() =>
        {
            var menu = GetWindow<UplinkMenu>();
            Assert.That(menu.Balance["Telecrystal"], Is.EqualTo(FixedPoint2.New(350)));
            Assert.That(menu.FindControl<BoxContainer>("CategoryListContainer").ChildCount, Is.Positive);
            Assert.That(menu.FindControl<BoxContainer>("StoreListingsContainer").ChildCount, Is.Positive);
        });
    }

    [Test]
    public async Task TreeStoreShowsNodesAndBalanceWhenOpenedByTheServer()
    {
        var store = ToServer(await SpawnTarget("PersonalAI", PlayerCoords));
        await Server.WaitPost(() =>
        {
            var component = SEntMan.GetComponent<StoreComponent>(store);
            component.Tree = "ChangelingEvolution";
            Server.System<StoreSystem>().ToggleUi(SPlayer, store, component);
        });
        await RunTicks(10);
        await Client.WaitAssertion(() =>
        {
            var menu = GetWindow<StoreTreeMenu>();
            Assert.That(menu.FindControl<NodeTreeView>("Tree").Children.OfType<NodeTreeButton>(), Is.Not.Empty);
            Assert.That(menu.FindControl<RichTextLabel>("BalanceLabel").Text, Is.Not.Null.And.Not.Empty);
        });
    }

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

        await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, null));
        await RunTicks(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(stores.TryAddCurrency(credit, store), Is.True);
            Server.PlayerMan.SetAttachedEntity(ServerSession, SPlayer);
        });
        await RunTicks(10);
        await Client.WaitAssertion(() => Assert.That(GetWindow<StoreMenu>().Balance[currency], Is.EqualTo(initial + 3)));
    }
}
