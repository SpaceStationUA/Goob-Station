using Content.Goobstation.Maths.FixedPoint;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._Pirate.Damage;
using Content.Server.Body.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Mobs.Systems;

namespace Content.IntegrationTests.Tests._Pirate.Damage;

public sealed class SoulDamageRegenerationTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    [TestCase(false)]
    [TestCase(true)]
    public async Task RegeneratesAfterDelayForLivingAndDeadBodies(bool dead)
    {
        await AddAtmosphere();

        var body = SEntMan.System<BodySystem>();
        var damageable = SEntMan.System<DamageableSystem>();
        var mobState = SEntMan.System<MobStateSystem>();
        var soulType = ProtoMan.Index<DamageTypePrototype>("Soul");
        FixedPoint2 initialSoulDamage = 9;
        FixedPoint2 additionalSoulDamage = 3;

        var applied = damageable.TryChangeDamage(
            SPlayer,
            new DamageSpecifier(soulType, initialSoulDamage),
            ignoreResistances: true,
            canMiss: false);

        Assert.That(applied?.DamageDict["Soul"], Is.EqualTo(initialSoulDamage));

        var playerDamage = Comp<DamageableComponent>(Player);
        var regeneration = SEntMan.GetComponent<SoulDamageRegenerationComponent>(SPlayer);
        foreach (var part in body.GetBodyChildren(SPlayer))
        {
            Assert.That(
                SEntMan.HasComponent<SoulDamageRegenerationComponent>(part.Id),
                Is.False,
                $"Attached body part {part.Id} should not regenerate Soul damage separately");
        }

        if (dead)
        {
            var bluntType = ProtoMan.Index<DamageTypePrototype>("Blunt");
            damageable.TryChangeDamage(
                SPlayer,
                new DamageSpecifier(bluntType, 210),
                ignoreResistances: true,
                targetPart: TargetBodyPart.Vital,
                canMiss: false);

            Assert.That(mobState.IsDead(SPlayer), Is.True);
        }

        var halfRecoveryDelay = (float) regeneration.RecoveryDelay.TotalSeconds / 2f;
        await RunSeconds(halfRecoveryDelay);

        var additionalApplied = damageable.TryChangeDamage(
            SPlayer,
            new DamageSpecifier(soulType, additionalSoulDamage),
            ignoreResistances: true,
            canMiss: false);

        Assert.That(additionalApplied?.DamageDict["Soul"], Is.EqualTo(additionalSoulDamage));

        var expectedSoulDamage = initialSoulDamage + additionalSoulDamage;
        await RunSeconds(halfRecoveryDelay);
        Assert.That(playerDamage.Damage.DamageDict["Soul"], Is.EqualTo(expectedSoulDamage));

        await RunSeconds(halfRecoveryDelay + TickPeriod * 2);
        Assert.That(
            playerDamage.Damage.DamageDict["Soul"],
            Is.EqualTo(expectedSoulDamage - regeneration.HealAmount));

        if (dead)
            Assert.That(mobState.IsDead(SPlayer), Is.True);
    }

    [Test]
    public async Task RegeneratesSoulStoredOnComplexBodyParent()
    {
        await AddAtmosphere();

        var damageable = SEntMan.System<DamageableSystem>();
        var soulType = ProtoMan.Index<DamageTypePrototype>("Soul");
        var playerDamage = Comp<DamageableComponent>(Player);
        FixedPoint2 initialSoulDamage = 3;

        var storedDamage = new DamageSpecifier(playerDamage.Damage);
        storedDamage.DamageDict[soulType.ID] = initialSoulDamage;
        damageable.SetDamage(SPlayer, playerDamage, storedDamage);

        var regeneration = SEntMan.GetComponent<SoulDamageRegenerationComponent>(SPlayer);
        await RunSeconds((float) regeneration.RecoveryDelay.TotalSeconds + TickPeriod * 2);

        Assert.That(
            playerDamage.Damage.DamageDict[soulType.ID],
            Is.EqualTo(initialSoulDamage - regeneration.HealAmount));
    }
}
