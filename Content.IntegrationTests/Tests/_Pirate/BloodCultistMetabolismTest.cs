// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Body.Systems;
using Content.Shared.BloodCult;
using Content.Shared.Body.Components;

namespace Content.IntegrationTests.Tests._Pirate;

public sealed class BloodCultistMetabolismTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    [Test]
    public async Task DeconversionRestoresOriginalBloodVolume()
    {
        var bloodstreamSystem = SEntMan.System<BloodstreamSystem>();
        var bloodstream = Comp<BloodstreamComponent>(Player);
        var originalReferenceVolume = bloodstream.BloodReferenceSolution.Volume;
        var originalBloodVolume = bloodstream.BloodReferenceSolution.GetTotalPrototypeQuantity("Blood");

        await Server.WaitPost(() => SEntMan.EnsureComponent<BloodCultistComponent>(SPlayer));

        bloodstream = Comp<BloodstreamComponent>(Player);
        Assert.Multiple(() =>
        {
            Assert.That(bloodstream.BloodReferenceSolution.Volume, Is.EqualTo(originalReferenceVolume));
            Assert.That(
                bloodstream.BloodReferenceSolution.GetTotalPrototypeQuantity("SanguinePerniculate"),
                Is.EqualTo(originalReferenceVolume));
            Assert.That(bloodstreamSystem.GetBloodLevel((SPlayer, bloodstream)), Is.EqualTo(1f).Within(0.001f));
        });

        await Server.WaitPost(() => SEntMan.RemoveComponent<BloodCultistComponent>(SPlayer));

        bloodstream = Comp<BloodstreamComponent>(Player);
        Assert.Multiple(() =>
        {
            Assert.That(bloodstream.BloodReferenceSolution.Volume, Is.EqualTo(originalReferenceVolume));
            Assert.That(
                bloodstream.BloodReferenceSolution.GetTotalPrototypeQuantity("Blood"),
                Is.EqualTo(originalBloodVolume));
            Assert.That(bloodstreamSystem.GetBloodLevel((SPlayer, bloodstream)), Is.EqualTo(1f).Within(0.001f));
        });
    }
}
