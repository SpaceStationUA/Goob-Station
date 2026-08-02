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
        var referenceBlood = bloodstream.BloodReferenceSolution;
        var originalReferenceVolume = referenceBlood.Volume;
        var originalBloodVolume = referenceBlood.GetTotalPrototypeQuantity("Blood");

        await Server.WaitPost(() => SEntMan.EnsureComponent<BloodCultistComponent>(SPlayer));

        bloodstream = Comp<BloodstreamComponent>(Player);
        referenceBlood = bloodstream.BloodReferenceSolution;
        Assert.Multiple(() =>
        {
            Assert.That(referenceBlood.Volume, Is.EqualTo(originalReferenceVolume));
            Assert.That(
                referenceBlood.GetTotalPrototypeQuantity("SanguinePerniculate"),
                Is.EqualTo(originalReferenceVolume));
            Assert.That(bloodstreamSystem.GetBloodLevel((SPlayer, bloodstream)), Is.EqualTo(1f).Within(0.001f));
        });

        await Server.WaitPost(() => SEntMan.RemoveComponent<BloodCultistComponent>(SPlayer));

        bloodstream = Comp<BloodstreamComponent>(Player);
        referenceBlood = bloodstream.BloodReferenceSolution;
        Assert.Multiple(() =>
        {
            Assert.That(referenceBlood.Volume, Is.EqualTo(originalReferenceVolume));
            Assert.That(
                referenceBlood.GetTotalPrototypeQuantity("Blood"),
                Is.EqualTo(originalBloodVolume));
            Assert.That(bloodstreamSystem.GetBloodLevel((SPlayer, bloodstream)), Is.EqualTo(1f).Within(0.001f));
        });
    }
}
