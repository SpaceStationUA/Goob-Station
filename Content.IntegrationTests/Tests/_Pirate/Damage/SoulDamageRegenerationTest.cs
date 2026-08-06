using Content.Goobstation.Maths.FixedPoint;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._Pirate.Damage;
using Content.Server.Body.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Damage;

public sealed class SoulDamageRegenerationTest : InteractionTest
{
    private static readonly ProtoId<DamageTypePrototype> SoulDamageType = "Soul";
    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";

    protected override string PlayerPrototype => "MobHuman";

    [TestCase(false)]
    [TestCase(true)]
    public async Task RegeneratesAfterDelayForLivingAndDeadBodies(bool dead)
    {
        await AddAtmosphere();

        FixedPoint2 initialSoulDamage = 9;
        FixedPoint2 additionalSoulDamage = 3;
        FixedPoint2 healAmount = 0;
        var halfRecoveryDelay = 0f;

        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.System<BodySystem>();
            var damageable = SEntMan.System<DamageableSystem>();
            var mobState = SEntMan.System<MobStateSystem>();
            var soulType = ProtoMan.Index(SoulDamageType);

            var applied = damageable.TryChangeDamage(
                SPlayer,
                new DamageSpecifier(soulType, initialSoulDamage),
                ignoreResistances: true,
                canMiss: false);

            Assert.That(applied?.DamageDict[SoulDamageType.Id], Is.EqualTo(initialSoulDamage));

            var regeneration = SEntMan.GetComponent<SoulDamageRegenerationComponent>(SPlayer);
            healAmount = regeneration.HealAmount;
            halfRecoveryDelay = (float) regeneration.RecoveryDelay.TotalSeconds / 2f;

            foreach (var part in body.GetBodyChildren(SPlayer))
            {
                Assert.That(
                    SEntMan.HasComponent<SoulDamageRegenerationComponent>(part.Id),
                    Is.False,
                    $"Attached body part {part.Id} should not regenerate Soul damage separately");
            }

            if (!dead)
                return;

            damageable.TryChangeDamage(
                SPlayer,
                new DamageSpecifier(ProtoMan.Index(BluntDamageType), 210),
                ignoreResistances: true,
                targetPart: TargetBodyPart.Vital,
                canMiss: false);
            Assert.That(mobState.IsDead(SPlayer), Is.True);
        });

        await RunSeconds(halfRecoveryDelay);

        await Server.WaitAssertion(() =>
        {
            var damageable = SEntMan.System<DamageableSystem>();
            var soulType = ProtoMan.Index(SoulDamageType);
            var additionalApplied = damageable.TryChangeDamage(
                SPlayer,
                new DamageSpecifier(soulType, additionalSoulDamage),
                ignoreResistances: true,
                canMiss: false);

            Assert.That(additionalApplied?.DamageDict[SoulDamageType.Id], Is.EqualTo(additionalSoulDamage));
        });

        var expectedSoulDamage = initialSoulDamage + additionalSoulDamage;
        await RunSeconds(halfRecoveryDelay);
        await Server.WaitAssertion(() => Assert.That(
            Comp<DamageableComponent>(Player).Damage.DamageDict[SoulDamageType.Id],
            Is.EqualTo(expectedSoulDamage)));

        await RunSeconds(halfRecoveryDelay + TickPeriod * 2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(
                Comp<DamageableComponent>(Player).Damage.DamageDict[SoulDamageType.Id],
                Is.EqualTo(expectedSoulDamage - healAmount));

            if (dead)
                Assert.That(SEntMan.System<MobStateSystem>().IsDead(SPlayer), Is.True);
        });
    }

    [Test]
    public async Task RegeneratesSoulStoredOnComplexBodyParent()
    {
        await AddAtmosphere();

        FixedPoint2 initialSoulDamage = 3;

        await Server.WaitAssertion(() =>
        {
            var damageable = SEntMan.System<DamageableSystem>();
            var regenerationSystem = SEntMan.System<SoulDamageRegenerationSystem>();
            var playerDamage = Comp<DamageableComponent>(Player);
            var storedDamage = new DamageSpecifier(playerDamage.Damage);
            storedDamage.DamageDict[SoulDamageType.Id] = initialSoulDamage;
            damageable.SetDamage(SPlayer, playerDamage, storedDamage);

            var regeneration = SEntMan.GetComponent<SoulDamageRegenerationComponent>(SPlayer);
            // Parent-only damage is transient on complex bodies, so exercise the fallback before body aggregation
            // rewrites it.
            regeneration.NextHeal = STiming.CurTime;
            regenerationSystem.Update(TickPeriod);

            Assert.That(
                Comp<DamageableComponent>(Player).Damage.DamageDict[SoulDamageType.Id],
                Is.EqualTo(initialSoulDamage - regeneration.HealAmount));
        });
    }
}
