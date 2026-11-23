// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Vehicles;
using Content.Pirate.Shared.Vehicles;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Buckle;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.Item.ItemToggle;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Pirate.Vehicles;

[TestFixture]
public sealed class StvTest
{
    [Test]
    public async Task PatrolQuadUsesInstalledPartsAndFuel()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var ents = server.ResolveDependency<IEntityManager>();
        var slots = ents.System<ItemSlotsSystem>();
        var solutions = ents.System<SharedSolutionContainerSystem>();
        var actions = ents.System<SharedActionsSystem>();
        EntityUid quad = default;
        EntityUid rider = default;

        await server.WaitAssertion(() =>
        {
            quad = ents.SpawnAtPosition("VehicleATVNTSecFilled", map.GridCoords);
            rider = ents.SpawnAtPosition("MobHuman", map.GridCoords);
            Assert.That(slots.GetItemOrNull(quad, "key_slot"), Is.Not.Null);
            foreach (var slot in new[] { "engine", "tires", "armor", "headlight", "lightbar" })
                Assert.That(slots.GetItemOrNull(quad, slot), Is.Not.Null, slot);
            Assert.That(ents.GetComponent<VehicleComponent>(quad).EngineRunning, Is.False);

            Assert.That(ents.System<SharedBuckleSystem>().TryBuckle(rider, rider, quad), Is.True);
            var ignition = ents.GetComponent<StvComponent>(quad).IgnitionAction!.Value;
            actions.PerformAction(rider, (ignition, ents.GetComponent<ActionComponent>(ignition)));
        });

        await server.WaitRunTicks(150);
        await server.WaitAssertion(() =>
        {
            Assert.That(ents.GetComponent<VehicleComponent>(quad).EngineRunning, Is.True);
            Assert.That(solutions.TryGetSolution(quad, "fueltank", out _, out var fuel), Is.True);
            Assert.That(fuel!.GetTotalPrototypeQuantity("WeldingFuel"), Is.LessThan(FixedPoint2.New(150)));
            var sirenAction = ents.GetComponent<StvComponent>(quad).SirenAction!.Value;
            actions.PerformAction(rider, (sirenAction, ents.GetComponent<ActionComponent>(sirenAction)));
            var siren = slots.GetItemOrNull(quad, "lightbar")!.Value;
            Assert.That(ents.System<ItemToggleSystem>().IsActivated(siren), Is.True);

            Assert.That(slots.TryEject(quad, "engine", null, out _, doAfter: false), Is.True);
            Assert.That(ents.GetComponent<VehicleComponent>(quad).EngineRunning, Is.False);
            Assert.That(ents.System<ItemToggleSystem>().IsActivated(siren), Is.False);
        });
        await pair.CleanReturnAsync();
    }
}
