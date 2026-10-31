using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Body.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;

namespace Content.IntegrationTests.Tests._Pirate.StarlightSpecies;

public sealed class NeocyteFirstAidTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    [Test, Combinatorial]
    public async Task StandardFirstAidTreatsHybridBodies(
        [Values("MobNeocyte", "MobNeoMoth")] string prototype,
        [Values("Ointment", "RegenerativeMesh", "Brutepack", "MedicatedSuture", "Gauze", "Bloodpack", "HealingToolbox")] string item)
    {
        await SpawnTarget(prototype);
        var bloodBefore = 0f;
        await Server.WaitAssertion(() =>
        {
            var target = STarget!.Value;
            Assert.That(Comp<DamageableComponent>().DamageContainerID?.Id, Is.EqualTo("Hybrid"));
            SEntMan.EnsureComponent<TargetingComponent>(SPlayer).Target = TargetBodyPart.Chest;
            var chest = SEntMan.System<SharedBodySystem>().GetBodyChildrenOfType(target, BodyPartType.Chest).Single().Id;
            var damageType = item switch
            {
                "Ointment" or "RegenerativeMesh" => "Heat",
                "Bloodpack" => "Bloodloss",
                _ => "Slash",
            };
            SEntMan.System<DamageableSystem>().ChangeDamage(chest,
                new DamageSpecifier(ProtoMan.Index<DamageTypePrototype>(damageType), 20), ignoreResistances: true);
            Assert.That(Comp<DamageableComponent>().TotalDamage, Is.EqualTo(FixedPoint2.New(20)));

            var blood = SEntMan.System<BloodstreamSystem>();
            if (item == "Bloodpack")
            {
                Assert.That(blood.TryModifyBloodLevel(target, -50), Is.True);
                bloodBefore = blood.GetBloodLevel(target);
            }
            if (item is "Gauze" or "MedicatedSuture")
                Assert.That(blood.TryModifyBleedAmount(target, 2), Is.True);
        });

        await InteractUsing(item, quantity: 1, awaitDoAfters: false);
        await Server.WaitAssertion(() => Assert.That(ActiveDoAfters.Any(), Is.True,
            "The healing-item filter must allow treatment to start on a Hybrid body."));
        await AwaitDoAfters();

        await Server.WaitAssertion(() =>
        {
            Assert.That(Comp<DamageableComponent>().TotalDamage, Is.LessThan(FixedPoint2.New(20)));
            if (item == "Bloodpack")
                Assert.That(SEntMan.System<BloodstreamSystem>().GetBloodLevel(STarget!.Value), Is.GreaterThan(bloodBefore));
            if (item is "Gauze" or "MedicatedSuture")
                Assert.That(Comp<BloodstreamComponent>().BleedAmount, Is.Zero);
        });
    }

    [TearDown]
    public async Task DeletePatient()
    {
        // Complex bodies must be deleted while their map still exists.
        await Server.WaitPost(() =>
        {
            if (STarget is { } target && !SEntMan.Deleted(target))
                SEntMan.DeleteEntity(target);
        });
        await RunTicks(2);
    }
}
