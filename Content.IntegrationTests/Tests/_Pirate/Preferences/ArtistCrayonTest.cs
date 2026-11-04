// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Numerics;
using Content.Shared.Charges.Components;
using Content.Shared.Decals;
using Content.Shared.Interaction;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Moq;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests._Pirate.Preferences;

[TestFixture]
public sealed class ArtistCrayonTest
{
    [Test]
    public async Task LoadoutRequiresTheAccountRatherThanTheCharacterName()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var localization = server.ResolveDependency<ILocalizationManager>();
            var previousCulture = localization.DefaultCulture;
            try
            {
                localization.SetCulture(CultureInfo.GetCultureInfo("uk-UA"));
                var profile = new HumanoidCharacterProfile().WithName("Jon_Brando");
                var loadout = new RoleLoadout("JobPassenger");
                var session = new Mock<ICommonSession>();

                foreach (var username in new[] { "Jon_Brando", "jon_brando", "OtherPlayer" })
                {
                    session.SetupGet(s => s.Name).Returns(username);
                    var allowed = loadout.IsValid(profile, session.Object, "LoadoutItemCrayonArtist",
                        IoCManager.Instance!, out var reason);
                    Assert.That(allowed, Is.EqualTo(username != "OtherPlayer"));
                    if (!allowed)
                        Assert.That(reason!.ToString(), Is.EqualTo("Потрібно бути визнаним митцем."));
                }

                Assert.That(loadout.IsValid(profile, null, "LoadoutItemCrayonArtist",
                    IoCManager.Instance!, out var missingSessionReason), Is.False);
                Assert.That(missingSessionReason!.ToString(), Is.EqualTo("Потрібно бути визнаним митцем."));
            }
            finally
            {
                localization.SetCulture(previousCulture!);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CanDrawMoreThanThirtyTimesWithoutRecharging()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var user = entities.SpawnEntity("MobHuman", map.GridCoords);
            var crayon = entities.SpawnEntity("CrayonArtist", map.GridCoords);
            var decals = server.System<SharedDecalSystem>();
            var initialCount = decals.GetDecalsInRange(map.GridCoords.EntityId, map.GridCoords.Position, 2).Count;

            const int strokes = 64;
            for (var i = 0; i < strokes; i++)
            {
                var coordinates = map.GridCoords.Offset(new Vector2(0.5f + i * 0.01f, 0.5f));
                var interaction = new AfterInteractEvent(user, crayon, null, coordinates, true);
                entities.EventBus.RaiseLocalEvent(crayon, interaction);
                Assert.That(interaction.Handled, Is.True);
            }

            Assert.That(decals.GetDecalsInRange(map.GridCoords.EntityId, map.GridCoords.Position, 2).Count,
                Is.EqualTo(initialCount + strokes));
            Assert.That(entities.HasComponent<LimitedChargesComponent>(crayon), Is.False);
            Assert.That(entities.HasComponent<AutoRechargeComponent>(crayon), Is.False);
            entities.DeleteEntity(crayon);
            entities.DeleteEntity(user);
        });

        await pair.CleanReturnAsync();
    }
}
