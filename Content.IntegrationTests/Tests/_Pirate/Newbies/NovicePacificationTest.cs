// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Server.Pacification.Systems;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared._Pirate.CCVars;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Pirate.Newbies;

[TestFixture]
public sealed class NovicePacificationTest
{
    [TestCase(0, true)]
    [TestCase(0.99, true)]
    [TestCase(1, false)]
    [TestCase(100, false)]
    public async Task LoadingPlaytimeGuardsShootingThenReconciles(double hours, bool restricted)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        var session = pair.Player!;
        var tracking = server.ResolveDependency<PlayTimeTrackingManager>();
        EntityUid user = default;
        EntityUid gun = default;

        void AssertShooting(bool blocked)
        {
            var shot = new ShotAttemptedEvent { User = user, Used = (gun, entities.GetComponent<GunComponent>(gun)) };
            entities.EventBus.RaiseLocalEvent(user, ref shot);
            Assert.That(shot.Cancelled, Is.EqualTo(blocked));
        }

        await server.WaitAssertion(() =>
        {
            server.ResolveDependency<IConfigurationManager>().SetCVar(NoviceProtectionCVars.PacificationEnabled, true);
            NoviceTestPlaytime.Set(tracking, session, hours);
            user = entities.SpawnEntity("MobHuman", map.GridCoords);
            gun = entities.SpawnEntity("WeaponRifleLecter", map.GridCoords);
            NoviceTestPlaytime.SetLoaded(tracking, session, false);
            try
            {
                Assert.DoesNotThrow(() => server.PlayerMan.SetAttachedEntity(session, user));
                Assert.That(entities.HasComponent<PacifiedComponent>(user), Is.True);
                AssertShooting(true);
            }
            finally
            {
                NoviceTestPlaytime.SetLoaded(tracking, session, true);
            }
            tracking.QueueSendTimers(session);
        });

        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.HasComponent<PacifiedComponent>(user), Is.EqualTo(restricted));
            AssertShooting(restricted);
            // A fresh body, including a ghost role, is checked independently of the previous body.
            var replacement = entities.SpawnEntity("MobHuman", map.GridCoords);
            server.PlayerMan.SetAttachedEntity(session, replacement);
            Assert.That(entities.HasComponent<PacifiedComponent>(replacement), Is.EqualTo(restricted));
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ExperiencedPlayersKeepIndependentPacifism(bool replaceWhileLoading)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        var session = pair.Player!;
        var tracking = server.ResolveDependency<PlayTimeTrackingManager>();
        EntityUid user = default;
        PacifiedComponent independent = default!;

        await server.WaitAssertion(() =>
        {
            server.ResolveDependency<IConfigurationManager>().SetCVar(NoviceProtectionCVars.PacificationEnabled, true);
            NoviceTestPlaytime.Set(tracking, session, 100);
            user = entities.SpawnEntity("MobHuman", map.GridCoords);
            if (!replaceWhileLoading)
                independent = entities.AddComponent<PacifiedComponent>(user);
            NoviceTestPlaytime.SetLoaded(tracking, session, false);
            try
            {
                server.PlayerMan.SetAttachedEntity(session, user);
                if (replaceWhileLoading)
                {
                    entities.RemoveComponent<PacifiedComponent>(user);
                    independent = entities.AddComponent<PacifiedComponent>(user);
                }
            }
            finally
            {
                NoviceTestPlaytime.SetLoaded(tracking, session, true);
            }
            tracking.QueueSendTimers(session);
        });

        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<PacifiedComponent>(user), Is.SameAs(independent));
            Assert.That(entities.HasComponent<NovicePacificationComponent>(user), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OneHourExpiryUsesCachedTimeWithoutAnotherAttach()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        var session = pair.Player!;
        var tracking = server.ResolveDependency<PlayTimeTrackingManager>();
        EntityUid user = default;
        await server.WaitAssertion(() =>
        {
            server.ResolveDependency<IConfigurationManager>().SetCVar(NoviceProtectionCVars.PacificationEnabled, true);
            NoviceTestPlaytime.Set(tracking, session, 0);
            user = entities.SpawnEntity("MobHuman", map.GridCoords);
            server.PlayerMan.SetAttachedEntity(session, user);
            Assert.That(entities.HasComponent<PacifiedComponent>(user), Is.True);
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            // Deliberately do not queue a timer update: the five-second fallback must expire the guard.
            tracking.GetTrackerTimes(session)[Content.Shared.Players.PlayTimeTracking.PlayTimeTrackingShared.TrackerOverall] = TimeSpan.FromHours(1);
        });
        await pair.RunTicksSync(360);
        await server.WaitAssertion(() => Assert.That(entities.HasComponent<PacifiedComponent>(user), Is.False));
        await pair.CleanReturnAsync();
    }
}
