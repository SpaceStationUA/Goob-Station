// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.HeatTint;
using Content.Server.Temperature.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Temperature.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Pirate.Temperature;

[TestFixture]
public sealed class FoodHeatTintTest
{
    [Test]
    public async Task FoodFiltersTintUpdatesAndKeepsItsExactTemperature()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var appearance = server.System<SharedAppearanceSystem>();
        var heat = server.System<TemperatureSystem>();
        var solutions = server.System<SharedSolutionContainerSystem>();

        await server.WaitAssertion(() =>
        {
            var food = server.EntMan.SpawnAtPosition("FoodMeat", map.GridCoords);
            var temperature = server.EntMan.GetComponent<TemperatureComponent>(food);
            const float initial = 292.9f;
            var next = MathF.BitIncrement(initial);

            float ReadTint(EntityUid uid)
            {
                Assert.That(appearance.TryGetData<float>(uid, HeatTintVisuals.Temperature, out var value), Is.True);
                return value;
            }

            heat.ForceChangeTemperature(food, initial, temperature);
            Assert.That(ReadTint(food), Is.EqualTo(initial));
            heat.ForceChangeTemperature(food, next, temperature);
            Assert.That(temperature.CurrentTemperature, Is.EqualTo(next));
            Assert.That(ReadTint(food), Is.EqualTo(initial));

            Assert.That(solutions.TryGetSolution(food, "food", out var foodSolution), Is.True);
            solutions.SetTemperature(foodSolution.Value, 350f);
            Assert.That(ReadTint(food), Is.EqualTo(350f));
            heat.ForceChangeTemperature(food, initial, temperature);
            Assert.That(ReadTint(food), Is.EqualTo(initial));

            var beaker = server.EntMan.SpawnAtPosition("Beaker", map.GridCoords);
            Assert.That(solutions.TryGetSolution(beaker, "beaker", out var beakerSolution), Is.True);
            solutions.TryAddReagent(beakerSolution.Value, "Water", 5);
            solutions.SetTemperature(beakerSolution.Value, initial);
            solutions.SetTemperature(beakerSolution.Value, next);
            Assert.That(ReadTint(beaker), Is.EqualTo(next));
        });

        await pair.CleanReturnAsync();
    }
}
