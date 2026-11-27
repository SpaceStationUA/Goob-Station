// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Item.ItemToggle;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Pirate.Tools;

[TestFixture]
[TestOf(typeof(SharedToolSystem))]
public sealed class WelderFuelTimerTest
{
    [TestCase("Welder")]
    [TestCase("FlippoLighter")]
    public async Task UnlitFuelTimerAdvancesWithoutDirtying(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        EntityUid uid = default;
        var lastChange = GameTick.Zero;
        var firstUpdate = TimeSpan.Zero;

        await server.WaitPost(() =>
        {
            uid = server.EntMan.SpawnAtPosition(prototype, map.GridCoords);
            var welder = server.EntMan.GetComponent<WelderComponent>(uid);
            lastChange = welder.LastModifiedTick;
            firstUpdate = welder.NextUpdate;
        });

        await pair.RunSeconds(2.5f);
        await server.WaitAssertion(() =>
        {
            var welder = server.EntMan.GetComponent<WelderComponent>(uid);
            Assert.That(welder.NextUpdate, Is.GreaterThan(firstUpdate));
            Assert.That(welder.LastModifiedTick, Is.EqualTo(lastChange));

            var nextUpdate = welder.NextUpdate;
            Assert.That(server.System<ItemToggleSystem>().TryActivate(uid), Is.True);
            Assert.That(welder.Enabled, Is.True);
            Assert.That(welder.NextUpdate, Is.EqualTo(nextUpdate));
            Assert.That(welder.LastModifiedTick > lastChange, Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
