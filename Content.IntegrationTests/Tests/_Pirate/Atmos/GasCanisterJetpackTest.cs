// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Piping.Binary.Components;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameObjects;
using GasCanisterComponent = Content.Shared.Atmos.Piping.Unary.Components.GasCanisterComponent;

namespace Content.IntegrationTests.Tests._Pirate.Atmos;

[TestFixture]
public sealed class GasCanisterJetpackTest
{
    [Test]
    public async Task JetpackRefillUpdatesCanisterUi()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var slots = server.System<ItemSlotsSystem>();
        var ui = server.System<SharedUserInterfaceSystem>();
        var map = await pair.CreateTestMap();

        EntityUid canister = default;
        EntityUid jetpack = default;

        await server.WaitAssertion(() =>
        {
            canister = entMan.SpawnEntity("AirCanister", map.GridCoords);
            jetpack = entMan.SpawnEntity("JetpackBlue", map.GridCoords);

            Assert.That(entMan.GetComponent<GasTankComponent>(jetpack).Air.Pressure, Is.Zero);
        });

        // Let the pre-existing canister complete an atmos update before the player inserts a tank.
        await server.WaitRunTicks(10);

        await server.WaitAssertion(() =>
        {
            var canisterComponent = entMan.GetComponent<GasCanisterComponent>(canister);
            Assert.That(canisterComponent.LastPressure, Is.GreaterThan(0f));
            Assert.That(slots.TryInsert(canister, canisterComponent.GasTankSlot, jetpack, null), Is.True);

            canisterComponent.ReleaseValve = true;
            entMan.Dirty(canister, canisterComponent);
        });

        await server.WaitRunTicks(10);

        await server.WaitAssertion(() =>
        {
            var tankPressure = entMan.GetComponent<GasTankComponent>(jetpack).Air.Pressure;
            Assert.That(tankPressure, Is.GreaterThan(0f), "The canister did not physically refill the jetpack.");

            Assert.That(ui.TryGetUiState<GasCanisterBoundUserInterfaceState>(canister,
                GasCanisterUiKey.Key,
                out var state),
                Is.True);
            Assert.That(state!.TankPressure,
                Is.EqualTo(tankPressure).Within(0.01f),
                "The canister UI still reports an empty jetpack after refilling it.");
        });

        await pair.CleanReturnAsync();
    }
}
