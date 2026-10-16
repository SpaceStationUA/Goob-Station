using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Shitmed.Damage;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Pirate.Damage;

[TestFixture]
public sealed class BodyDamageIntegrationTest
{
    [Test]
    public async Task LimbIntegrityCapIsSharedAcrossDamageTypes()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = server.System<SharedBodySystem>();
            var damageable = server.System<DamageableSystem>();
            var blunt = server.ProtoMan.Index<DamageTypePrototype>("Blunt");
            var heat = server.ProtoMan.Index<DamageTypePrototype>("Heat");
            var mob = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var chest = body.GetBodyChildrenOfType(mob, BodyPartType.Chest).Single().Id;
            var woundable = entMan.GetComponent<WoundableComponent>(chest);
            woundable.AllowWounds = false;
            woundable.IntegrityCap = FixedPoint2.New(60);

            damageable.ChangeDamage(chest, new DamageSpecifier(blunt, 40), ignoreResistances: true);
            var applied = damageable.ChangeDamage(chest, new DamageSpecifier(heat, 40), ignoreResistances: true);
            Assert.That(applied.DamageDict[heat.ID], Is.EqualTo(FixedPoint2.New(20)));
            Assert.That(entMan.GetComponent<DamageableComponent>(chest).TotalDamage, Is.EqualTo(FixedPoint2.New(60)));
            Assert.That(entMan.GetComponent<DamageableComponent>(mob).TotalDamage, Is.EqualTo(FixedPoint2.New(60)));

            // A capped limb must reject other damage types, while healing releases the shared budget.
            Assert.That(damageable.ChangeDamage(chest,
                new DamageSpecifier(blunt, 10) + new DamageSpecifier(heat, 10),
                ignoreResistances: true).Empty, Is.True);
            applied = damageable.ChangeDamage(chest,
                new DamageSpecifier(blunt, -10) + new DamageSpecifier(heat, 10),
                ignoreResistances: true);
            Assert.That(applied.DamageDict[blunt.ID], Is.EqualTo(FixedPoint2.New(-10)));
            Assert.That(applied.DamageDict[heat.ID], Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(entMan.GetComponent<DamageableComponent>(chest).Damage.DamageDict[blunt.ID], Is.EqualTo(FixedPoint2.New(30)));
            Assert.That(entMan.GetComponent<DamageableComponent>(chest).Damage.DamageDict[heat.ID], Is.EqualTo(FixedPoint2.New(30)));
            Assert.That(entMan.GetComponent<DamageableComponent>(mob).TotalDamage, Is.EqualTo(FixedPoint2.New(60)));

            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HealingResidualWoundsWithNoStoredDamage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = server.System<SharedBodySystem>();
            var wounds = server.System<WoundSystem>();
            var damageable = server.System<DamageableSystem>();
            var blunt = server.ProtoMan.Index<DamageTypePrototype>("Blunt");
            var mob = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var chest = body.GetBodyChildrenOfType(mob, BodyPartType.Chest).Single().Id;

            // Fractures can leave wounds behind after healing has already cleared stored damage.
            Assert.That(wounds.TryInduceWound(chest, blunt.ID, FixedPoint2.New(20), out _), Is.True);
            Assert.That(wounds.TryHaltAllBleeding(chest), Is.True);
            Assert.That(wounds.GetWoundableWounds(chest).All(wound => wounds.CanHealWound(wound)), Is.True);
            var severity = wounds.GetWoundableSeverityPoint(chest);
            Assert.That(severity, Is.GreaterThan(FixedPoint2.Zero));
            Assert.That(entMan.GetComponent<DamageableComponent>(chest).TotalDamage, Is.EqualTo(FixedPoint2.Zero));

            var applied = damageable.ChangeDamage(chest, new DamageSpecifier(blunt, -5), ignoreResistances: true);
            Assert.That(applied.Empty, Is.True);
            Assert.That(wounds.GetWoundableSeverityPoint(chest), Is.LessThan(severity));
            Assert.That(entMan.GetComponent<DamageableComponent>(mob).TotalDamage, Is.EqualTo(FixedPoint2.Zero));

            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase(TargetBodyPart.Vital)]
    [TestCase(TargetBodyPart.Hands)]
    [TestCase(TargetBodyPart.Legs)]
    public async Task CompositeTargetsExcludeOtherBodyParts(TargetBodyPart target)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = server.System<SharedBodySystem>();
            var damageable = server.System<DamageableSystem>();
            var blunt = server.ProtoMan.Index<DamageTypePrototype>("Blunt");
            var mob = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var parts = body.GetBodyChildrenWithComponent<DamageableComponent>(mob).ToList();

            var applied = damageable.ChangeDamage(mob,
                new DamageSpecifier(blunt, 12),
                ignoreResistances: true,
                targetPart: target,
                canMiss: false);

            Assert.That(applied.DamageDict[blunt.ID], Is.EqualTo(FixedPoint2.New(12)));
            Assert.That(entMan.GetComponent<DamageableComponent>(mob).TotalDamage, Is.EqualTo(FixedPoint2.New(12)));

            foreach (var (_, part, damage) in parts)
            {
                var expected = target switch
                {
                    TargetBodyPart.Vital => part.PartType is BodyPartType.Head or BodyPartType.Chest or BodyPartType.Groin ? 4 : 0,
                    TargetBodyPart.Hands => part.PartType == BodyPartType.Hand ? 6 : 0,
                    TargetBodyPart.Legs => part.PartType == BodyPartType.Leg ? 6 : 0,
                    _ => throw new ArgumentOutOfRangeException(nameof(target)),
                };
                Assert.That(damage.Damage.DamageDict[blunt.ID], Is.EqualTo(FixedPoint2.New(expected)),
                    $"Unexpected damage to {part.PartType} when targeting {target}.");
            }

            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase(SplitDamageBehavior.SplitEnsureAll, true)]
    [TestCase(SplitDamageBehavior.SplitEnsureAll, false)]
    [TestCase(SplitDamageBehavior.SplitEnsureAllDamaged, true)]
    [TestCase(SplitDamageBehavior.SplitEnsureAllDamaged, false)]
    [TestCase(SplitDamageBehavior.SplitEnsureAllOrganic, true)]
    [TestCase(SplitDamageBehavior.SplitEnsureAllOrganic, false)]
    [TestCase(SplitDamageBehavior.SplitEnsureAllDamagedAndOrganic, true)]
    [TestCase(SplitDamageBehavior.SplitEnsureAllDamagedAndOrganic, false)]
    public async Task HealingConservesEachDamageType(SplitDamageBehavior split, bool singleDamagedPart)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = server.System<SharedBodySystem>();
            var damageable = server.System<DamageableSystem>();
            var blunt = server.ProtoMan.Index<DamageTypePrototype>("Blunt");
            var heat = server.ProtoMan.Index<DamageTypePrototype>("Heat");
            var mob = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var lightlyDamagedPart = body.GetBodyChildrenOfType(mob, BodyPartType.Chest).Single().Id;
            var bluntPart = body.GetBodyChildrenOfType(mob, BodyPartType.Groin).Single().Id;
            var heatPart = singleDamagedPart ? bluntPart : body.GetBodyChildrenOfType(mob, BodyPartType.Head).Single().Id;

            // Separate damaged parts also exercise surplus healing and parts healthy in only one damage type.
            if (!singleDamagedPart)
                damageable.ChangeDamage(lightlyDamagedPart, new DamageSpecifier(blunt, 1), ignoreResistances: true);
            damageable.ChangeDamage(bluntPart, new DamageSpecifier(blunt, 20), ignoreResistances: true);
            damageable.ChangeDamage(heatPart, new DamageSpecifier(heat, 20), ignoreResistances: true);

            var applied = damageable.ChangeDamage(mob,
                new DamageSpecifier(blunt, -10) + new DamageSpecifier(heat, -10),
                ignoreResistances: true,
                targetPart: TargetBodyPart.All,
                splitDamage: split,
                canMiss: false);

            Assert.That(applied.DamageDict[blunt.ID], Is.EqualTo(FixedPoint2.New(-10)));
            Assert.That(applied.DamageDict[heat.ID], Is.EqualTo(FixedPoint2.New(-10)));
            Assert.That(entMan.GetComponent<DamageableComponent>(bluntPart).Damage.DamageDict[blunt.ID],
                Is.EqualTo(FixedPoint2.New(singleDamagedPart ? 10 : 11)));
            Assert.That(entMan.GetComponent<DamageableComponent>(heatPart).Damage.DamageDict[heat.ID],
                Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(entMan.GetComponent<DamageableComponent>(lightlyDamagedPart).TotalDamage, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(entMan.GetComponent<DamageableComponent>(mob).TotalDamage,
                Is.EqualTo(FixedPoint2.New(singleDamagedPart ? 20 : 21)));

            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }
}
