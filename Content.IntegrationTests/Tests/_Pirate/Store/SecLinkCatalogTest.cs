// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Client.Store.Ui;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Pirate.Store;
using Content.Shared.FixedPoint;
using Content.Shared.Input;
using Content.Shared.Store;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Pirate.Store;

public sealed class SecLinkCatalogTest : InteractionTest
{
    protected override string PlayerPrototype => "MobIPC";

    [Test]
    public async Task HeldAndFloorRadiosShowTheirOwnCatalogs()
    {
        var held = await SpawnTarget("BaseSeclinkRadio", PlayerCoords);
        await Pickup(held);
        await PressKey(ContentKeyFunctions.UseItemInHand);
        await RunTicks(10);
        await Client.WaitAssertion(() => AssertRadioMenu(held));

        var floor = await SpawnTarget("BaseSeclinkRadio", PlayerCoords);
        await PressKey(ContentKeyFunctions.ActivateItemInWorld, coordinates: PlayerCoords);
        await RunTicks(10);
        await Client.WaitAssertion(() =>
        {
            AssertRadioMenu(held);
            AssertRadioMenu(floor);
        });

        await Client.WaitAssertion(() =>
        {
            var clientUi = Client.System<SharedUserInterfaceSystem>();
            Assert.That(clientUi.TryGetOpenUi(ToClient(held), StoreUiKey.Key, out var heldUi), Is.True);
            heldUi!.SendMessage(new StoreBuyListingMessage("SeclinkBaton"));
        });
        await RunTicks(10);
        await Client.WaitAssertion(() =>
        {
            AssertRadioMenu(held, batonTokens: 0);
            AssertRadioMenu(floor);
        });

        var ui = Server.System<SharedUserInterfaceSystem>();
        await Server.WaitPost(() => ui.CloseUi(ToServer(held), StoreUiKey.Key, SPlayer));
        await RunTicks(5);
        await Client.WaitAssertion(() =>
        {
            var viewer = CEntMan.GetComponent<StoreCatalogViewerComponent>(CPlayer);
            Assert.That(viewer.Catalogs.Keys, Is.EquivalentTo(new[] { floor }));
            AssertRadioMenu(floor);
        });

        await Server.WaitPost(() => ui.CloseUi(ToServer(floor), StoreUiKey.Key, SPlayer));
        await RunTicks(5);
        await Client.WaitAssertion(() => Assert.That(CEntMan.HasComponent<StoreCatalogViewerComponent>(CPlayer), Is.False));
    }

    private void AssertRadioMenu(NetEntity radio, int batonTokens = 1)
    {
        var menu = UiMan.WindowRoot.Children.OfType<StoreMenu>().Single(w => w.IsOpen && w.Owner == ToClient(radio));
        Assert.That(menu.Balance["BatonToken"], Is.EqualTo(FixedPoint2.New(batonTokens)));
        Assert.That(menu.Balance["DisablerToken"], Is.EqualTo(FixedPoint2.New(1)));
        Assert.That(menu.Balance["UtilityToken"], Is.EqualTo(FixedPoint2.New(1)));
        Assert.That(menu.FindControl<BoxContainer>("CategoryListContainer").ChildCount, Is.EqualTo(3));
        Assert.That(menu.FindControl<BoxContainer>("StoreListingsContainer").ChildCount, Is.Positive);
        Assert.That(menu.FindControl<Button>("RefundButton").Visible, Is.False);
        Assert.That(menu.FindControl<BoxContainer>("TraitorFooter").Visible, Is.False);
    }
}
