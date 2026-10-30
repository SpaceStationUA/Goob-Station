// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Steps;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Pirate.Experiments;

[TestFixture]
public sealed class ExperimentSurgeryTest
{
    [TestCase(BodyPartSymmetry.Left, null)]
    [TestCase(BodyPartSymmetry.Right, null)]
    [TestCase(BodyPartSymmetry.Left, "LeftLegHuman")]
    [TestCase(BodyPartSymmetry.Right, "RightLegHuman")]
    [TestCase(BodyPartSymmetry.Left, "LeftLegCybernetic")]
    [TestCase(BodyPartSymmetry.Right, "RightLegCybernetic")]
    public async Task MissingLegCanBeSurgicallyAttached(BodyPartSymmetry symmetry, string replacementPrototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = server.System<SharedBodySystem>();
            var surgery = server.System<SurgerySystem>();
            var mob = entMan.SpawnEntity("MobExperiment", MapCoordinates.Nullspace);
            var groins = body.GetBodyChildrenOfType(mob, BodyPartType.Groin).ToList();
            Assert.That(groins, Has.Count.EqualTo(1), "Experiments need a groin for leg attachment surgery.");
            var groin = groins.Single().Id;
            var chest = body.GetBodyChildrenOfType(mob, BodyPartType.Chest).Single().Id;
            var leg = body.GetBodyChildrenOfType(mob, BodyPartType.Leg, symmetry: symmetry).Single().Id;
            Assert.That(body.GetParentPartOrNull(groin), Is.EqualTo(chest));
            Assert.That(body.GetParentPartOrNull(leg), Is.EqualTo(groin));

            var attachSurgery = surgery.GetSingleton($"SurgeryAttach{symmetry}Leg")!.Value;
            var occupied = new SurgeryValidEvent(mob, groin);
            entMan.EventBus.RaiseLocalEvent(attachSurgery, ref occupied);
            Assert.That(occupied.Cancelled, Is.True);

            Assert.That(body.TryDetachPart(leg), Is.True);
            var available = new SurgeryValidEvent(mob, groin);
            entMan.EventBus.RaiseLocalEvent(attachSurgery, ref available);
            Assert.That(available.Cancelled, Is.False, "The missing leg must be offered in the surgery menu.");

            var replacement = replacementPrototype != null
                ? entMan.SpawnEntity(replacementPrototype, MapCoordinates.Nullspace)
                : leg;
            var insertStep = surgery.GetSingleton("SurgeryStepInsertFeature")!.Value;
            var insert = new SurgeryStepEvent(mob, mob, groin, replacement, attachSurgery, insertStep, false);
            entMan.EventBus.RaiseLocalEvent(insertStep, ref insert);

            var inserted = new SurgeryStepCompleteCheckEvent(mob, groin, attachSurgery);
            entMan.EventBus.RaiseLocalEvent(insertStep, ref inserted);
            Assert.That(inserted.Cancelled, Is.False);
            Assert.That(entMan.GetComponent<BodyPartComponent>(replacement).Body, Is.EqualTo(mob));
            Assert.That(body.GetParentPartOrNull(replacement), Is.EqualTo(groin));
            Assert.That(entMan.HasComponent<BodyPartReattachedComponent>(replacement), Is.True);

            var sealStep = surgery.GetSingleton("SurgeryStepSealWounds")!.Value;
            var cautery = entMan.SpawnEntity("Cautery", MapCoordinates.Nullspace);
            var seal = new SurgeryStepEvent(mob, mob, groin, cautery, attachSurgery, sealStep, false);
            entMan.EventBus.RaiseLocalEvent(sealStep, ref seal);
            Assert.That(entMan.HasComponent<BodyPartReattachedComponent>(replacement), Is.False);
            Assert.That(body.GetBodyChildrenOfType(mob, BodyPartType.Leg).Count(), Is.EqualTo(2));

            var finished = new SurgeryValidEvent(mob, groin);
            entMan.EventBus.RaiseLocalEvent(attachSurgery, ref finished);
            Assert.That(finished.Cancelled, Is.True);

            entMan.DeleteEntity(cautery);
            entMan.DeleteEntity(mob);
            if (replacementPrototype != null)
                entMan.DeleteEntity(leg);
        });

        await pair.CleanReturnAsync();
    }
}
