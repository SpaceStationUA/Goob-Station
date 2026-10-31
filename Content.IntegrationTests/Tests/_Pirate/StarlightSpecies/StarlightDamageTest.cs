using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Shitmed.Damage;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Pirate.StarlightSpecies;

[TestFixture]
public sealed class StarlightDamageTest
{
    [Test]
    public async Task SpawnedLimbsHaveTheirSpeciesInnateResistance()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var body = em.System<SharedBodySystem>();
            string[] species = [
                "Cyclorite", "Doll", "Elf", "Felionoid", "Lagomorph", "Neocyte",
                "NeoArachnid", "NeoAvali", "NeoCyclorite", "NeoDiona", "NeoDwarf", "NeoElf",
                "NeoFelionoid", "NeoHuman", "NeoKin", "NeoLagomorph", "NeoMoth", "NeoReptilian",
                "NeoResomi", "NeoSlimePerson", "NeoThaven", "NeoVox", "NeoVulpkanin",
            ];

            foreach (var id in species)
            {
                var mob = em.SpawnEntity($"Mob{id}", MapCoordinates.Nullspace);
                var modifier = em.GetComponent<DamageableComponent>(mob).DamageModifierSetId;
                var parts = body.GetBodyChildren(mob).ToArray();
                Assert.That(parts, Is.Not.Empty, id);
                foreach (var part in parts)
                    Assert.That(em.GetComponent<DamageableComponent>(part.Id).DamageModifierSetId,
                        Is.EqualTo(modifier), $"{id}: {part.Component.PartType}");
                em.DeleteEntity(mob);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CycloriteResistsBluntAndSlashDamageToItsLimbs()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var damage = em.System<DamageableSystem>();
            var blunt = server.ProtoMan.Index<DamageTypePrototype>("Blunt");
            var slash = server.ProtoMan.Index<DamageTypePrototype>("Slash");
            var mob = em.SpawnEntity("MobCyclorite", MapCoordinates.Nullspace);
            var applied = damage.ChangeDamage(mob,
                new DamageSpecifier(blunt, 10) + new DamageSpecifier(slash, 10),
                targetPart: TargetBodyPart.LeftArm, canMiss: false);

            Assert.That(applied.DamageDict[blunt.ID], Is.EqualTo(FixedPoint2.New(8.5)));
            Assert.That(applied.DamageDict[slash.ID], Is.EqualTo(FixedPoint2.New(7)));
            em.DeleteEntity(mob);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(TargetBodyPart.Head)]
    [TestCase(TargetBodyPart.Chest)]
    [TestCase(TargetBodyPart.Groin)]
    [TestCase(TargetBodyPart.LeftArm)]
    [TestCase(TargetBodyPart.LeftHand)]
    [TestCase(TargetBodyPart.LeftLeg)]
    [TestCase(TargetBodyPart.LeftFoot)]
    public async Task HeavyFrameProtectsEachBodyPart(TargetBodyPart target)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var damage = em.System<DamageableSystem>();
            var inventory = em.System<InventorySystem>();
            var blunt = server.ProtoMan.Index<DamageTypePrototype>("Blunt");
            var slash = server.ProtoMan.Index<DamageTypePrototype>("Slash");
            var mob = em.SpawnEntity("MobNeocyte", MapCoordinates.Nullspace);
            if (inventory.TryGetSlotEntity(mob, "outerClothing2", out var initialFrame))
            {
                inventory.TryUnequip(mob, "outerClothing2", silent: true, force: true);
                em.DeleteEntity(initialFrame.Value);
            }

            var hit = new DamageSpecifier(blunt, 10) + new DamageSpecifier(slash, 10);
            var unarmored = damage.ChangeDamage(mob, hit, targetPart: target, canMiss: false);
            var frame = em.SpawnEntity("ClothingOuterArmorNeocyteHeavyBasic", MapCoordinates.Nullspace);
            Assert.That(inventory.TryEquip(mob, frame, "outerClothing2", silent: true, force: true), Is.True);
            var armored = damage.ChangeDamage(mob, hit, targetPart: target, canMiss: false);

            Assert.That(unarmored.GetTotal(), Is.GreaterThan(FixedPoint2.Zero));
            Assert.That(armored.DamageDict[blunt.ID].Float(),
                Is.EqualTo(unarmored.DamageDict[blunt.ID].Float() * 0.85f).Within(0.01f));
            Assert.That(armored.DamageDict[slash.ID].Float(),
                Is.EqualTo(unarmored.DamageDict[slash.ID].Float() * 0.75f).Within(0.01f));
            em.DeleteEntity(mob);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("MobNeocyte", true, 0.5f)]
    [TestCase("MobNeocyte", false, 0.5f)]
    [TestCase("MobNeoHuman", true, 0.5f)]
    [TestCase("MobNeoHuman", false, 0.5f)]
    [TestCase("MobHuman", true, 1f)]
    [TestCase("MobHuman", false, 1f)]
    public async Task HealingPenaltyAppliesOnceToDirectAndBodyWideTreatments(string prototype, bool direct, float modifier)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var body = em.System<SharedBodySystem>();
            var damage = em.System<DamageableSystem>();
            var blunt = server.ProtoMan.Index<DamageTypePrototype>("Blunt");
            var heat = server.ProtoMan.Index<DamageTypePrototype>("Heat");
            var mob = em.SpawnEntity(prototype, MapCoordinates.Nullspace);
            var chest = body.GetBodyChildrenOfType(mob, BodyPartType.Chest).Single().Id;
            var groin = body.GetBodyChildrenOfType(mob, BodyPartType.Groin).Single().Id;
            var head = body.GetBodyChildrenOfType(mob, BodyPartType.Head).Single().Id;

            // Uneven damage exercises surplus healing across healthy and lightly damaged parts.
            damage.ChangeDamage(chest, new DamageSpecifier(blunt, 1), ignoreResistances: true);
            damage.ChangeDamage(groin, new DamageSpecifier(blunt, 20), ignoreResistances: true);
            damage.ChangeDamage(head, new DamageSpecifier(heat, 20), ignoreResistances: true);
            var bluntHeal = new DamageSpecifier(blunt, -10);
            var heatHeal = new DamageSpecifier(heat, -10);
            var medicine = bluntHeal + heatHeal;
            var applied = direct
                ? damage.ChangeDamage(groin, bluntHeal, ignoreResistances: true)
                  + damage.ChangeDamage(head, heatHeal, ignoreResistances: true)
                : damage.ChangeDamage(mob, medicine, ignoreResistances: true,
                    targetPart: TargetBodyPart.All, splitDamage: SplitDamageBehavior.SplitEnsureAll, canMiss: false);

            Assert.That(applied.DamageDict[blunt.ID], Is.EqualTo(FixedPoint2.New(-10 * modifier)));
            Assert.That(applied.DamageDict[heat.ID], Is.EqualTo(FixedPoint2.New(-10 * modifier)));
            Assert.That(em.GetComponent<DamageableComponent>(mob).TotalDamage,
                Is.EqualTo(FixedPoint2.New(41 - 20 * modifier)));
            Assert.That(bluntHeal.DamageDict[blunt.ID], Is.EqualTo(FixedPoint2.New(-10)));
            Assert.That(heatHeal.DamageDict[heat.ID], Is.EqualTo(FixedPoint2.New(-10)));
            Assert.That(medicine.DamageDict[blunt.ID], Is.EqualTo(FixedPoint2.New(-10)));
            Assert.That(medicine.DamageDict[heat.ID], Is.EqualTo(FixedPoint2.New(-10)));
            em.DeleteEntity(mob);
        });
        await pair.CleanReturnAsync();
    }
}
