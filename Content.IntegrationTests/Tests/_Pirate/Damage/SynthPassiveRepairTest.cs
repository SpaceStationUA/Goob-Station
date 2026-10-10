using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Server._Afterlight.Silicons.Synths.Body;
using Content.Shared._Afterlight.Silicons.Synths.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Pirate.Damage;

[TestFixture]
public sealed class SynthPassiveRepairTest
{
    [TestCase(BodyPartType.Arm)]
    [TestCase(BodyPartType.Leg)]
    public async Task RepairsExtremitiesAndChargesOnlyAppliedHealing(BodyPartType partType)
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
            var mob = entMan.SpawnEntity("MobSynth", MapCoordinates.Nullspace);
            var limb = body.GetBodyChildrenOfType(mob, partType).First().Id;
            damageable.ChangeDamage(limb, new DamageSpecifier(blunt, 20), ignoreResistances: true);

            var synth = entMan.GetComponent<SynthBloodstreamComponent>(mob);
            synth.MinHunger = 0f;
            synth.MinBloodLevel = 0f;
            synth.FullEfficiencyBloodLevel = 0f;
            synth.HungerCostPerRepair = 2f;
            synth.Damage.Groups.Clear();
            synth.Damage.Types.Clear();
            synth.Damage.Types[blunt.ID] = FixedPoint2.New(-4);
            synth.BleedReductionAmount = 0f;
            synth.BloodRegeneration.TargetBloodLevel = 0f;
            synth.NextUpdate = server.Resolve<IGameTiming>().CurTime;

            var hunger = entMan.GetComponent<HungerComponent>(mob);
            var hungerBefore = hungerSystem.GetHunger(hunger);
            Assert.That(hungerBefore, Is.GreaterThan(8f));
            server.System<SynthBloodstreamSystem>().Update(0f);

            Assert.That(entMan.GetComponent<DamageableComponent>(limb).TotalDamage, Is.EqualTo(FixedPoint2.New(16)));
            Assert.That(entMan.GetComponent<DamageableComponent>(mob).TotalDamage, Is.EqualTo(FixedPoint2.New(16)));
            Assert.That(hungerSystem.GetHunger(hunger), Is.EqualTo(hungerBefore - 8f).Within(0.001f));

            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }
}
