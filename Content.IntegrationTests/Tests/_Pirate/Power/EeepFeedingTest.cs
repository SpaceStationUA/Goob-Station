using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Interaction;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;

using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Pirate.Power;

public sealed class EeepFeedingTest : InteractionTest
{
    protected override string PlayerPrototype => "MobEeeplet";

    [TestCase("SMESBasic", false)]
    [TestCase("SMESAdvanced", false)]
    [TestCase("APCBasic", true)]
    [TestCase("SubstationBasic", true)]
    public async Task FeedingOnlyStartsOnAllowedPowerSources(string source, bool allowed)
    {
        await SpawnTarget(source);

        await Server.WaitAssertion(() =>
        {
            var battery = SEntMan.System<SharedBatterySystem>();
            var target = STarget!.Value;
            var charge = battery.GetCharge((target, SEntMan.GetComponent<BatteryComponent>(target)));
            var interaction = new BeforeInteractHandEvent(target);
            SEntMan.EventBus.RaiseLocalEvent(SPlayer, interaction);

            Assert.That(interaction.Handled, Is.EqualTo(allowed));
            Assert.That(ActiveDoAfters, allowed ? Is.Not.Empty : Is.Empty);
            Assert.That(battery.GetCharge((target, SEntMan.GetComponent<BatteryComponent>(target))), Is.EqualTo(charge));
        });
    }
}
