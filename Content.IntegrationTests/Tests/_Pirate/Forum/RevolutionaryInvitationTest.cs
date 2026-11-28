// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Server.Mind;
using Content.Shared._EinsteinEngines.Revolutionary;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mindshield.Components;
using Content.Shared.Revolutionary.Components;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Localization;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Pirate.Forum;

[TestFixture]
public sealed class RevolutionaryInvitationTest
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task PlayerRespondsToManifesto(bool accept, bool addMindshield)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true, DummyTicker = false, Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        var session = pair.Player!;
        var entities = server.EntMan;
        var locale = client.ResolveDependency<ILocalizationManager>();
        var previousCulture = locale.DefaultCulture;
        await client.WaitPost(() => locale.SetCulture(CultureInfo.GetCultureInfo("uk-UA")));
        EntityUid target = default;
        EntityUid leader = default;
        EntityUid book = default;
        await server.WaitPost(() =>
        {
            target = entities.SpawnAttachedTo("MobHuman", map.GridCoords);
            leader = entities.SpawnAttachedTo("MobHuman", map.GridCoords);
            var minds = server.System<MindSystem>();
            var mind = minds.CreateMind(session.UserId, session.Name);
            minds.TransferTo(mind, target, mind: mind.Comp);
            entities.EnsureComponent<HeadRevolutionaryComponent>(leader);
            book = entities.SpawnAttachedTo("RevolutionaryManifesto", map.GridCoords);
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(leader, book), Is.True);
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            Assert.That(session.AttachedEntity, Is.EqualTo(target));
            var converter = server.System<RevolutionaryConverterSystem>();
            converter.ConvertTarget(book, target, leader);
            converter.ConvertTarget(book, target, leader);
            Assert.That(entities.HasComponent<RevolutionaryComponent>(target), Is.False);
            if (addMindshield)
                entities.EnsureComponent<MindShieldComponent>(target);
        });
        await pair.RunTicksSync(5);

        Button button = default!;
        var ui = client.ResolveDependency<IUserInterfaceManager>();
        await client.WaitAssertion(() =>
        {
            var window = ui.WindowRoot.Children.OfType<FancyWindow>()
                .Single(w => w.Title == locale.GetString("pirate-rev-invite-title"));
            var label = locale.GetString(accept ? "pirate-rev-invite-accept" : "pirate-rev-invite-decline");
            button = Buttons(window).Single(b => b.Text == label);
            button.Mode = BaseButton.ActionMode.Press;
            button.MuteSounds = true;
        });
        var screen = new ScreenCoordinates(Vector2.Zero, default);
        foreach (var state in new[] { BoundKeyState.Down, BoundKeyState.Up })
            await client.DoGuiEvent(button, new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, state, screen, false, Vector2.Zero, Vector2.Zero));
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
            Assert.That(entities.HasComponent<RevolutionaryComponent>(target), Is.EqualTo(accept && !addMindshield)));
        await client.WaitAssertion(() =>
        {
            Assert.That(ui.WindowRoot.Children.OfType<FancyWindow>().Any(w => w.Title == locale.GetString("pirate-rev-invite-title")), Is.False);
            locale.SetCulture(previousCulture!);
        });
        await pair.CleanReturnAsync();
    }

    private static IEnumerable<Button> Buttons(Control parent)
    {
        foreach (var child in parent.Children)
        {
            if (child is Button button)
                yield return button;
            foreach (var descendant in Buttons(child))
                yield return descendant;
        }
    }
}
