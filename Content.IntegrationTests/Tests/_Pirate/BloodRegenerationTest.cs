using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;

namespace Content.IntegrationTests.Tests._Pirate;

public sealed class BloodRegenerationTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    [Test]
    public async Task NaturalRegenerationRestoresSmallDeficitWithoutNutritionCost()
    {
        await AddAtmosphere();

        var bloodstream = Comp<BloodstreamComponent>(Player);
        var hunger = Comp<HungerComponent>(Player);
        var thirst = Comp<ThirstComponent>(Player);
        var bloodstreamSystem = SEntMan.System<BloodstreamSystem>();
        var hungerSystem = SEntMan.System<HungerSystem>();
        var thirstSystem = SEntMan.System<ThirstSystem>();

        const float initialHunger = 175f;
        const float initialThirst = 450f;
        const float missingBlood = 0.25f;

        hunger.BaseDecayRate = 0f;
        hunger.ActualDecayRate = 0f;
        thirst.BaseDecayRate = 0f;
        thirst.ActualDecayRate = 0f;
        hungerSystem.SetHunger(SPlayer, initialHunger, hunger);
        thirstSystem.SetThirst(SPlayer, thirst, initialThirst);

        Assert.That(
            bloodstreamSystem.TryModifyBloodLevel((SPlayer, bloodstream), -missingBlood),
            Is.True,
            "Could not remove blood before testing regeneration");
        Assert.That(bloodstreamSystem.GetBloodLevel((SPlayer, bloodstream)), Is.LessThan(1f));

        var secondsUntilUpdate = Math.Max(
            TickPeriod,
            (float) (bloodstream.NextUpdate - STiming.CurTime).TotalSeconds + TickPeriod);
        await RunSeconds(secondsUntilUpdate);

        Assert.Multiple(() =>
        {
            Assert.That(bloodstreamSystem.GetBloodLevel((SPlayer, bloodstream)), Is.EqualTo(1f).Within(0.001f));
            Assert.That(hungerSystem.GetHunger(hunger), Is.EqualTo(initialHunger).Within(0.001f));
            Assert.That(thirst.CurrentThirst, Is.EqualTo(initialThirst).Within(0.001f));
        });
    }
}
