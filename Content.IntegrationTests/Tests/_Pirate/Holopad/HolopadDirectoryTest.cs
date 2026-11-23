// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Holopad;
using Content.IntegrationTests.Tests.Interaction;
using HolopadSystem = Content.Server.Holopad.HolopadSystem;
using Content.Shared.Holopad;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Pirate.Holopad;

[TestFixture]
public sealed class HolopadDirectoryTest : InteractionTest
{
    [Test]
    public async Task DirectoryIsBuiltOnlyForViewersAndReflectsChangedContacts()
    {
        await SpawnTarget("Holopad", PlayerCoords);
        var pad = STarget!.Value;
        var ui = Server.System<SharedUserInterfaceSystem>();
        var holopads = Server.System<HolopadSystem>();
        EntityUid contact = default;

        await Server.WaitAssertion(() =>
        {
            holopads.UpdateUIState(pad);
            Assert.That(SEntMan.GetComponent<HolopadComponent>(pad).CachedDirectory, Is.Null);
            contact = SEntMan.SpawnAtPosition("Holopad", SEntMan.GetCoordinates(TargetCoords));
            ui.OpenUi(pad, HolopadUiKey.InteractionWindow, SPlayer);
        });
        await RunTicks(12);
        await Client.WaitAssertion(() =>
            Assert.That(GetControlFromField<BoxContainer>("ContactsList", GetWindow<HolopadWindow>()).ChildCount, Is.EqualTo(1)));

        await Server.WaitAssertion(() =>
        {
            var previous = SEntMan.GetComponent<HolopadComponent>(pad).CachedDirectory;
            holopads.UpdateUIState(pad);
            Assert.That(SEntMan.GetComponent<HolopadComponent>(pad).CachedDirectory, Is.SameAs(previous));
            Assert.That(ui.TryGetUiState<BoundUserInterfaceState>(pad, HolopadUiKey.InteractionWindow, out _), Is.False);
            SEntMan.DeleteEntity(contact);
            holopads.UpdateUIState(pad);
        });
        await RunTicks(10);
        await Client.WaitAssertion(() =>
            Assert.That(GetControlFromField<BoxContainer>("ContactsList", GetWindow<HolopadWindow>()).ChildCount, Is.Zero));

        await Server.WaitAssertion(() =>
        {
            ui.CloseUi(pad, HolopadUiKey.InteractionWindow);
            holopads.UpdateUIState(pad);
            Assert.That(SEntMan.GetComponent<HolopadComponent>(pad).CachedDirectory, Is.Null);
        });
    }
}
