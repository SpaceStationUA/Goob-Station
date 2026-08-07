using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;

namespace Content.IntegrationTests.Tests._Pirate;

public sealed class BloodRegenerationTest : InteractionTest
{
    private const float InitialHunger = 175f;
    private const float InitialThirst = 450f;

    protected override string PlayerPrototype => "BloodRegenerationTestMob";

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: BloodRegenerationTestMob
          parent: MobHuman
          components:
          - type: Hunger
            baseDecayRate: 0
            startingHunger: 175
          - type: Thirst
            baseDecayRate: 0
            startingThirst: 450
        """;

    [Test]
    public async Task NaturalRegenerationRestoresSmallDeficitWithNutritionCost()
    {
        await AddAtmosphere();

        var bloodstream = Comp<BloodstreamComponent>(Player);
        var hunger = Comp<HungerComponent>(Player);
        var thirst = Comp<ThirstComponent>(Player);
        var bloodstreamSystem = SEntMan.System<BloodstreamSystem>();
        var hungerSystem = SEntMan.System<HungerSystem>();

        const float missingBlood = 0.25f;

        Assert.That(
            bloodstreamSystem.TryModifyBloodLevel((SPlayer, bloodstream), -missingBlood),
            Is.True,
            "Could not remove blood before testing regeneration");
        Assert.That(bloodstreamSystem.GetBloodLevel((SPlayer, bloodstream)), Is.LessThan(1f));

        var hungerBefore = hungerSystem.GetHunger(hunger);
        var thirstBefore = thirst.CurrentThirst;

        var secondsUntilUpdate = Math.Max(
            TickPeriod,
            (float) (bloodstream.NextUpdate - STiming.CurTime).TotalSeconds + TickPeriod);
        await RunSeconds(secondsUntilUpdate);

        Assert.Multiple(() =>
        {
            // Blood should be restored to full
            Assert.That(bloodstreamSystem.GetBloodLevel((SPlayer, bloodstream)), Is.EqualTo(1f).Within(0.001f));
            // Hunger should have decreased (cost of blood regeneration)
            Assert.That(hungerSystem.GetHunger(hunger), Is.LessThan(hungerBefore));
            // Thirst should have decreased (cost of blood regeneration)
            Assert.That(thirst.CurrentThirst, Is.LessThan(thirstBefore));
        });
    }

    [Test]
    public async Task NoRegenerationWhenFullySaturated()
    {
        await AddAtmosphere();

        var bloodstream = Comp<BloodstreamComponent>(Player);
        var hunger = Comp<HungerComponent>(Player);
        var thirst = Comp<ThirstComponent>(Player);
        var bloodstreamSystem = SEntMan.System<BloodstreamSystem>();
        var hungerSystem = SEntMan.System<HungerSystem>();

        // Blood is already full at spawn
        var hungerBefore = hungerSystem.GetHunger(hunger);
        var thirstBefore = thirst.CurrentThirst;

        var secondsUntilUpdate = Math.Max(
            TickPeriod,
            (float) (bloodstream.NextUpdate - STiming.CurTime).TotalSeconds + TickPeriod);
        await RunSeconds(secondsUntilUpdate);

        Assert.Multiple(() =>
        {
            // Blood should remain full
            Assert.That(bloodstreamSystem.GetBloodLevel((SPlayer, bloodstream)), Is.EqualTo(1f).Within(0.001f));
            // Hunger should NOT have decreased (no regeneration needed, no cost)
            Assert.That(hungerSystem.GetHunger(hunger), Is.EqualTo(hungerBefore).Within(0.001f));
            // Thirst should NOT have decreased
            Assert.That(thirst.CurrentThirst, Is.EqualTo(thirstBefore).Within(0.001f));
        });
    }
}
