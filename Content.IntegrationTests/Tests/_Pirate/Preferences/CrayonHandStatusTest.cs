// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Linq;
using Content.Client.Items;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.Tests._Pirate.Preferences;

public sealed class CrayonHandStatusTest : InteractionTest
{
    [TestCase("CrayonArtist", "uk-UA", "(∞)")]
    [TestCase("CrayonArtist", "en-US", "(∞)")]
    [TestCase("CrayonRainbow", "uk-UA", "(30/30)")]
    [TestCase("CrayonInfinite", "en-US", "(30/30)")]
    public async Task PickupDropAndPickupAgainShowTheHandStatus(string prototype, string culture, string counter)
    {
        var localization = Client.ResolveDependency<ILocalizationManager>();
        var previousCulture = localization.DefaultCulture;
        Control status = null;

        try
        {
            await Client.WaitPost(() => localization.SetCulture(CultureInfo.GetCultureInfo(culture)));
            var crayon = await SpawnTarget(prototype);

            for (var i = 0; i < 2; i++)
            {
                await Pickup(crayon);
                await RunTicks(5);

                await Client.WaitAssertion(() =>
                {
                    var uid = ToClient(crayon);
                    Assert.That(Client.System<SharedHandsSystem>().GetActiveItem(CPlayer), Is.EqualTo(uid));

                    // Exercise the exact HUD event that previously threw on picking up CrayonArtist.
                    var message = new ItemStatusCollectMessage();
                    CEntMan.EventBus.RaiseLocalEvent(uid, message);
                    Assert.That(message.Controls, Has.Count.EqualTo(1));
                    status = message.Controls.Single();
                    UiMan.RootControl.AddChild(status);
                });

                await RunTicks(5);
                await Client.WaitAssertion(() =>
                {
                    var label = (RichTextLabel) status.Children.Single();
                    Assert.That(label.GetMessage(), Does.Contain(counter));
                    status.Orphan();
                    status = null;
                });

                await Drop();
                await RunTicks(5);
            }
        }
        finally
        {
            await Client.WaitPost(() =>
            {
                status?.Orphan();
                localization.SetCulture(previousCulture!);
            });
        }
    }
}
