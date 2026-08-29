// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Temperature.Systems;
using Content.Goobstation.Shared.Temperature;
using Content.Shared.Temperature;
using Content.Shared.Temperature.Components;
using Robust.Server;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
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
            FailureLogLevel = LogLevel.Fatal,
            Options = new ServerOptions
            {
                LoadConfigAndUserData = false,
                LoadContentResources = true,
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
        await server.WaitPost(() => server.CfgMan.SetCVar(RTCVars.FailureLogLevel, LogLevel.Error));

        await server.WaitAssertion(() =>
        {
            var entityManager = server.ResolveDependency<IEntityManager>();
            var temperatureSystem = entityManager.System<TemperatureSystem>();
            var counter = entityManager.System<TemperatureEventCounterSystem>();
            var entity = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);
            var temperature = entityManager.AddComponent<TemperatureComponent>(entity);
            var lastTemperature = temperature.CurrentTemperature;
            var requestedTemperature = lastTemperature + 1f;
            var expectedTemperature = requestedTemperature + TemperatureEventCounterSystem.ImmunityAdjustment;

            temperatureSystem.ForceChangeTemperature(entity, requestedTemperature, temperature);

            Assert.Multiple(() =>
            {
                Assert.That(counter.EventCount, Is.EqualTo(1));
                Assert.That(counter.CurrentTemperature, Is.EqualTo(expectedTemperature).Within(0.001f));
                Assert.That(counter.LastTemperature, Is.EqualTo(lastTemperature).Within(0.001f));
                Assert.That(counter.TemperatureDelta, Is.EqualTo(expectedTemperature - lastTemperature).Within(0.001f));
                Assert.That(temperature.CurrentTemperature, Is.EqualTo(expectedTemperature).Within(0.001f));
            });

            counter.CancelNextAttempt = true;
            var temperatureBeforeCancellation = temperature.CurrentTemperature;
            temperatureSystem.ForceChangeTemperature(entity, temperatureBeforeCancellation + 2f, temperature);

            Assert.Multiple(() =>
            {
                Assert.That(temperature.CurrentTemperature, Is.EqualTo(temperatureBeforeCancellation).Within(0.001f));
                Assert.That(counter.EventCount, Is.EqualTo(1));
            });
        });
    }

    [Reflect(false)]
    private sealed class TemperatureEventCounterSystem : EntitySystem
    {
        public const float ImmunityAdjustment = 1f;

        public int EventCount { get; private set; }
        public float CurrentTemperature { get; private set; }
        public float LastTemperature { get; private set; }
        public float TemperatureDelta { get; private set; }
        public bool CancelNextAttempt { get; set; }

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<TemperatureComponent, TemperatureImmunityEvent>(OnTemperatureImmunity);
            SubscribeLocalEvent<TemperatureComponent, TemperatureChangeAttemptEvent>(OnTemperatureChangeAttempt);
            SubscribeLocalEvent<TemperatureComponent, OnTemperatureChangeEvent>(OnTemperatureChange);
        }

        private void OnTemperatureImmunity(Entity<TemperatureComponent> _, ref TemperatureImmunityEvent args)
        {
            args.CurrentTemperature += ImmunityAdjustment;
        }

        private void OnTemperatureChangeAttempt(Entity<TemperatureComponent> _, ref TemperatureChangeAttemptEvent args)
        {
            if (!CancelNextAttempt)
                return;

            CancelNextAttempt = false;
            args.Cancel();
        }

        private void OnTemperatureChange(Entity<TemperatureComponent> _, ref OnTemperatureChangeEvent args)
        {
            EventCount++;
            CurrentTemperature = args.CurrentTemperature;
            LastTemperature = args.LastTemperature;
            TemperatureDelta = args.TemperatureDelta;
        }
    }
}
