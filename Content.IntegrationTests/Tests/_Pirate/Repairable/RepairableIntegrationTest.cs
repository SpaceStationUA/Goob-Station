using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Shitmed.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Medical.Healing;
using Content.Shared.Repairable;

namespace Content.IntegrationTests.Tests._Pirate.Repairable;

public sealed class RepairableIntegrationTest : InteractionTest
{
    [TestCase("BorgChassisGeneric", BodyType.Simple, 10)]
    [TestCase("MobIPC", BodyType.Complex, 15)]
    public async Task WeldingRepairsSimpleAndComplexBodies(string prototype, BodyType bodyType, int repairedPerUse)
    {
        await SpawnTarget(prototype);

        await Server.WaitAssertion(() =>
        {
            var target = STarget!.Value;
            Assert.That(SEntMan.GetComponent<BodyComponent>(target).BodyType, Is.EqualTo(bodyType));
            var repairable = SEntMan.GetComponent<RepairableComponent>(target);
            repairable.AutoDoAfter = false;

            if (bodyType == BodyType.Simple)
            {
                Assert.That(repairable.Damage, Is.Null);
                Assert.That(repairable.DamageValue, Is.EqualTo(-repairedPerUse));
            }

            var damagedPart = bodyType == BodyType.Complex
                ? SEntMan.System<SharedBodySystem>().GetBodyChildrenOfType(target, BodyPartType.Chest).Single().Id
                : target;
            var damageable = SEntMan.System<DamageableSystem>();
            damageable.ChangeDamage(damagedPart,
                new DamageSpecifier(ProtoMan.Index<DamageTypePrototype>("Blunt"), 35),
                ignoreResistances: true);
            Assert.That(SEntMan.GetComponent<DamageableComponent>(target).TotalDamage, Is.EqualTo(FixedPoint2.New(35)));

            if (bodyType == BodyType.Complex)
            {
                Assert.That(repairable.Damage, Is.Not.Null);
                Assert.That(SEntMan.System<HealingSystem>().TryGetNextDamagedPart(target,
                    new HealingComponent { Damage = repairable.Damage!, BloodlossModifier = -100 },
                    out var repairTarget), Is.True, "The IPC chest must be eligible for welding.");
                Assert.That(repairTarget, Is.EqualTo(damagedPart));
            }
        });

        await InteractUsing(Weld, awaitDoAfters: false);
        await Server.WaitAssertion(() => Assert.That(ActiveDoAfters.Any(), Is.True, "Welding must start a repair do-after."));
        await AwaitDoAfters();

        await Server.WaitAssertion(() =>
        {
            Assert.That(Comp<DamageableComponent>().TotalDamage, Is.EqualTo(FixedPoint2.New(35 - repairedPerUse)));
            Comp<RepairableComponent>().AutoDoAfter = true;
        });

        // The remaining damage takes multiple automatic repairs with the same welder.
        await InteractUsing(Weld);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Comp<DamageableComponent>().TotalDamage, Is.EqualTo(FixedPoint2.Zero));
        });
    }
}
