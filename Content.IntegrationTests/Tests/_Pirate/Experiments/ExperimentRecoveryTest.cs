// SPDX-License-Identifier: MIT

using System.Linq;
using System.Reflection;
using Content.Client.HealthAnalyzer.UI;
using Content.Client.Overlays;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Pirate.Experiments;

[TestFixture]
public sealed class ExperimentRecoveryTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task RecoveryReleasesSoftCriticalKnockdown(bool activeKnockdown)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = server.System<SharedBodySystem>();
            var damageable = server.System<DamageableSystem>();
            var mobState = server.System<MobStateSystem>();
            var stun = server.System<SharedStunSystem>();
            var standing = server.System<StandingStateSystem>();
            var blunt = server.ProtoMan.Index<DamageTypePrototype>("Blunt");
            var mob = entMan.SpawnEntity("MobExperiment", MapCoordinates.Nullspace);
            var chest = body.GetBodyChildrenOfType(mob, BodyPartType.Chest).Single().Id;

            damageable.ChangeDamage(chest, new DamageSpecifier(blunt, 100), ignoreResistances: true, canMiss: false);
            Assert.That(mobState.IsSoftCritical(mob), Is.True);
            var knockdown = entMan.GetComponent<KnockedDownComponent>(mob);
            Assert.That(stun.TryStand((mob, knockdown)), Is.False);
            Assert.That(knockdown.AutoStand, Is.False);

            if (activeKnockdown)
                stun.SetKnockdownTime((mob, knockdown), TimeSpan.FromSeconds(10));

            var nextUpdate = knockdown.NextUpdate;
            damageable.ChangeDamage(chest, new DamageSpecifier(blunt, -5), ignoreResistances: true, canMiss: false);

            Assert.That(mobState.IsAlive(mob), Is.True);
            Assert.That(entMan.HasComponent<KnockedDownComponent>(mob), Is.EqualTo(activeKnockdown));
            Assert.That(standing.IsDown(mob), Is.EqualTo(activeKnockdown));
            if (activeKnockdown)
            {
                Assert.That(knockdown.AutoStand, Is.True);
                Assert.That(knockdown.NextUpdate, Is.EqualTo(nextUpdate));
            }

            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task HungerRegenerationReachesArmsAndLegsInSoftCritical(bool fed)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = server.System<SharedBodySystem>();
            var damageable = server.System<DamageableSystem>();
            var hungerSystem = server.System<HungerSystem>();
            var blunt = server.ProtoMan.Index<DamageTypePrototype>("Blunt");
            var mob = entMan.SpawnEntity("MobExperiment", MapCoordinates.Nullspace);
            var arm = body.GetBodyChildrenOfType(mob, BodyPartType.Arm).First().Id;
            var leg = body.GetBodyChildrenOfType(mob, BodyPartType.Leg).First().Id;
            foreach (var limb in new[] { arm, leg })
                damageable.ChangeDamage(limb, new DamageSpecifier(blunt, 60), ignoreResistances: true, canMiss: false);

            Assert.That(server.System<MobStateSystem>().IsSoftCritical(mob), Is.True);
            var hunger = entMan.GetComponent<HungerComponent>(mob);
            hungerSystem.SetHunger(mob, fed ? 175f : 40f, hunger);
            hunger.NextThresholdUpdateTime = server.Resolve<IGameTiming>().CurTime;
            hungerSystem.Update(0f);

            foreach (var limb in new[] { arm, leg })
                Assert.That(entMan.GetComponent<DamageableComponent>(limb).Damage.DamageDict[blunt.ID],
                    Is.EqualTo(FixedPoint2.New(fed ? 59.975 : 60)));

            if (fed)
                Assert.That(entMan.GetComponent<DamageableComponent>(mob).TotalDamage,
                    Is.EqualTo(FixedPoint2.New(119.95)));

            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase(true, MobState.Alive, 0, 1f)]
    [TestCase(true, MobState.Alive, 50, 0.5f)]
    [TestCase(true, MobState.SoftCritical, 100, 1f)]
    [TestCase(true, MobState.SoftCritical, 125, 0.75f)]
    [TestCase(true, MobState.Critical, 150, 0.5f)]
    [TestCase(true, MobState.Critical, 175, 0.25f)]
    [TestCase(true, MobState.Dead, 200, 0f)]
    [TestCase(false, MobState.Alive, 50, 0.5f)]
    [TestCase(false, MobState.Critical, 100, 1f)]
    [TestCase(false, MobState.Critical, 150, 0.5f)]
    public async Task MedicalHealthBarUsesTheFirstCriticalThreshold(bool softCritical, MobState state,
        int damage, float expectedRatio)
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Client.WaitAssertion(() =>
        {
            var entMan = pair.Client.EntMan;
            var mob = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var thresholds = new MobThresholdsComponent();
            thresholds.Thresholds[0] = MobState.Alive;
            thresholds.Thresholds[100] = softCritical ? MobState.SoftCritical : MobState.Critical;
            if (softCritical)
                thresholds.Thresholds[150] = MobState.Critical;
            thresholds.Thresholds[200] = MobState.Dead;

            var overlay = new EntityHealthBarOverlay(entMan, pair.Client.ProtoMan);
            var calculate = typeof(EntityHealthBarOverlay).GetMethod("CalcProgress", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var result = ((float ratio, bool inCrit)) calculate.Invoke(overlay,
                new object[] { mob, new MobStateComponent { CurrentState = state }, new DamageableComponent { TotalDamage = damage }, thresholds })!;

            Assert.That(result.ratio, Is.InRange(0f, 1f));
            Assert.That(result.ratio, Is.EqualTo(expectedRatio).Within(0.001f));
            Assert.That(result.inCrit, Is.EqualTo(state != MobState.Alive));
            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HealthAnalyzerRecognizesSoftCriticalPatients()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Client.WaitAssertion(() =>
        {
            var getStatus = typeof(HealthAnalyzerWindow).GetMethod("GetStatus", BindingFlags.Static | BindingFlags.NonPublic)!;
            var status = (string) getStatus.Invoke(null, new object[] { MobState.SoftCritical })!;
            Assert.That(status, Is.EqualTo(Loc.GetString("health-analyzer-window-entity-critical-text")));
            Assert.That(status, Is.Not.EqualTo(Loc.GetString("health-analyzer-window-entity-unknown-text")));
        });

        await pair.CleanReturnAsync();
    }
}
