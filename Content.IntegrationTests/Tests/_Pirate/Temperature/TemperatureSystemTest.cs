// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Temperature.Systems;
using Content.Shared.Temperature;
using Content.Shared.Temperature.Components;
using Robust.Server;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Reflection;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests._Pirate.Temperature;

[TestFixture]
[TestOf(typeof(TemperatureSystem))]
public sealed class TemperatureSystemTest : RobustIntegrationTest
{
    [Test]
    public async Task ForceChangeTemperatureRaisesOneEvent()
    {
        var options = new RobustIntegrationTest.ServerIntegrationOptions
        {
            ContentStart = true,
            ContentAssemblies = PoolManager.GetAssemblies(client: false, includePoolAssembly: false),
            Pool = false,
            Options = new ServerOptions
            {
                LoadConfigAndUserData = false,
                LoadContentResources = false,
            },
        };

        foreach (var (cvar, value) in PoolManager.TestCvars)
        {
            options.CVarOverrides[cvar] = value;
        }

        options.BeforeStart += () =>
        {
            IoCManager.Resolve<IEntitySystemManager>()
                .LoadExtraSystemType<TemperatureEventCounterSystem>();
        };

        using var server = StartServer(options);
        await server.WaitIdleAsync();

        await server.WaitAssertion(() =>
        {
            var entityManager = server.ResolveDependency<IEntityManager>();
            var temperatureSystem = entityManager.System<TemperatureSystem>();
            var counter = entityManager.System<TemperatureEventCounterSystem>();
            var entity = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);
            var temperature = entityManager.AddComponent<TemperatureComponent>(entity);

            temperatureSystem.ForceChangeTemperature(entity, temperature.CurrentTemperature + 1f, temperature);

            Assert.That(counter.EventCount, Is.EqualTo(1));
        });
    }

    [Reflect(false)]
    private sealed class TemperatureEventCounterSystem : EntitySystem
    {
        public int EventCount { get; private set; }

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<TemperatureComponent, OnTemperatureChangeEvent>(OnTemperatureChange);
        }

        private void OnTemperatureChange(Entity<TemperatureComponent> _, ref OnTemperatureChangeEvent args)
        {
            EventCount++;
        }
    }
}
