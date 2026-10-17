// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Server.Hands.Systems;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Steps;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.DoAfter;
using Content.Shared.Standing;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Pirate.Medical;

[TestFixture]
public sealed class SurgeryRegressionTest
{
    [Test]
    public async Task OpenIncisionRemainsCompleteWithoutTemporaryPain()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = server.System<SharedBodySystem>();
            var surgery = server.System<SurgerySystem>();
            var human = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var chest = body.GetBodyChildrenOfType(human, BodyPartType.Chest).Single().Id;
            var incision = surgery.GetSingleton("SurgeryOpenIncision")!.Value;

            Assert.That(surgery.GetNextStep(human, chest, incision, human)?.Step, Is.EqualTo(0));
            entMan.EnsureComponent<IncisionOpenComponent>(chest);
            Assert.That(surgery.GetNextStep(human, chest, incision, human)?.Step, Is.EqualTo(1));
            entMan.EnsureComponent<SkinRetractedComponent>(chest);

            // Reopening the UI must accept the persistent incision even after SurgeryPain expires.
            Assert.That(surgery.GetNextStep(human, chest, incision, human), Is.Null);
            entMan.RemoveComponent<IncisionOpenComponent>(chest);
            Assert.That(surgery.GetNextStep(human, chest, incision, human)?.Step, Is.EqualTo(0));
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("Blunt", "SurgeryStepRepairBruteTissue")]
    [TestCase("Heat", "SurgeryStepRepairBurnTissue")]
    public async Task TendingCompletesWhenOnlyUnhealableWoundsRemain(string damageType, string stepId)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = server.System<SharedBodySystem>();
            var surgery = server.System<SurgerySystem>();
            var wounds = server.System<WoundSystem>();
            var human = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var chest = body.GetBodyChildrenOfType(human, BodyPartType.Chest).Single().Id;
            Assert.That(wounds.TryInduceWound(chest, damageType, 10, out var wound), Is.True);
            wounds.TryHaltAllBleeding(chest);
            var step = surgery.GetSingleton(stepId)!.Value;

            var healable = new SurgeryStepCompleteCheckEvent(human, chest, EntityUid.Invalid);
            entMan.EventBus.RaiseLocalEvent(step, ref healable);
            Assert.That(healable.Cancelled, Is.True);

            wound!.Value.Comp.CanBeHealed = false;
            var blocked = new SurgeryStepCompleteCheckEvent(human, chest, EntityUid.Invalid);
            entMan.EventBus.RaiseLocalEvent(step, ref blocked);
            Assert.That(blocked.Cancelled, Is.False);
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TendingCompletesAtTheTraumaHealingFloor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var human = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var chest = server.System<SharedBodySystem>().GetBodyChildrenOfType(human, BodyPartType.Chest).Single().Id;
            var woundable = entMan.GetComponent<WoundableComponent>(chest);
            var wounds = server.System<WoundSystem>();
            Assert.That(wounds.TryInduceWound(chest, "Blunt", 20, out var wound), Is.True);
            var bone = woundable.Bone.ContainedEntities.Single();
            entMan.GetComponent<BoneComponent>(bone).BoneSeverity = BoneSeverity.Cracked;
            var inflicter = entMan.EnsureComponent<TraumaInflicterComponent>(wound!.Value);
            server.System<TraumaSystem>().AddTrauma(bone, (chest, woundable), (wound.Value, inflicter), TraumaSystem.BoneDamage, 20);
            wounds.TryHaltAllBleeding(chest);

            Assert.That(wounds.GetWoundableSeverityPoint(chest, damageGroup: "Brute", healable: true),
                Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(wounds.TryHealWoundsOnWoundable(chest, 100, out var healed), Is.True);
            Assert.That(healed, Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(wound.Value.Comp.WoundSeverityPoint, Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(wounds.GetWoundableSeverityPoint(chest, damageGroup: "Brute", healable: true),
                Is.EqualTo(FixedPoint2.Zero));
            var check = new SurgeryStepCompleteCheckEvent(human, chest, EntityUid.Invalid);
            entMan.EventBus.RaiseLocalEvent(server.System<SurgerySystem>().GetSingleton("SurgeryStepRepairBruteTissue")!.Value,
                ref check);
            Assert.That(check.Cancelled, Is.False);
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HaltedBleedingDoesNotRestartOnUpdateButNewDamageCanReopenIt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid human = default;
        EntityUid chest = default;
        Entity<WoundComponent> wound = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            human = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            chest = server.System<SharedBodySystem>().GetBodyChildrenOfType(human, BodyPartType.Chest).Single().Id;
            var wounds = server.System<WoundSystem>();
            Assert.That(wounds.TryInduceWound(chest, "Slash", 10, out var induced), Is.True);
            wound = induced!.Value;
            Assert.That(entMan.GetComponent<BleedInflicterComponent>(wound).IsBleeding, Is.True);
            Assert.That(wounds.TryHaltAllBleeding(chest), Is.True);
        });

        await server.WaitRunTicks(90);

        await server.WaitAssertion(() =>
        {
            var bleeds = server.EntMan.GetComponent<BleedInflicterComponent>(wound);
            Assert.That(bleeds.IsBleeding, Is.False);
            Assert.That(bleeds.BleedingAmount, Is.EqualTo(FixedPoint2.Zero));
            var severity = wound.Comp.WoundSeverityPoint;
            var unchanged = new WoundSeverityPointChangedEvent(wound.Comp, severity, severity);
            server.EntMan.EventBus.RaiseLocalEvent(wound, ref unchanged);
            Assert.That(bleeds.IsBleeding, Is.False);

            // New damage can overflow a capped wound without increasing its stored severity.
            var overflow = new WoundSeverityPointChangedEvent(wound.Comp, severity, severity, FixedPoint2.New(1));
            server.EntMan.EventBus.RaiseLocalEvent(wound, ref overflow);
            Assert.That(bleeds.IsBleeding, Is.True);
            server.System<WoundSystem>().TryHaltAllBleeding(chest);
            Assert.That(server.System<WoundSystem>().TryInduceWound(chest, "Slash", 1, out _), Is.True);
            Assert.That(bleeds.IsBleeding, Is.True);
            server.EntMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("MobHuman", BodyPartType.Groin, "SurgeryAttachLeftLeg", "left leg", BodyPartSymmetry.Left)]
    [TestCase("MobExperiment", BodyPartType.Chest, "SurgeryAttachLeftLeg", "left leg", BodyPartSymmetry.Left)]
    [TestCase("MobExperiment", BodyPartType.Chest, "SurgeryAttachRightLeg", "right leg", BodyPartSymmetry.Right)]
    public async Task MissingLegCanBeReattachedToItsActualParent(string prototype, BodyPartType parentType,
        string surgeryId, string slot, BodyPartSymmetry symmetry)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = server.System<SharedBodySystem>();
            var surgery = server.System<SurgerySystem>();
            var patient = entMan.SpawnEntity(prototype, MapCoordinates.Nullspace);
            var parent = body.GetBodyChildrenOfType(patient, parentType).Single().Id;
            var leg = body.GetBodyChildrenOfType(patient, BodyPartType.Leg, symmetry: symmetry).Single().Id;
            var procedure = surgery.GetSingleton(surgeryId)!.Value;

            var attached = new SurgeryValidEvent(patient, parent);
            entMan.EventBus.RaiseLocalEvent(procedure, ref attached);
            Assert.That(attached.Cancelled, Is.True);
            Assert.That(body.DetachPart(parent, slot, leg), Is.True);

            var missing = new SurgeryValidEvent(patient, parent);
            entMan.EventBus.RaiseLocalEvent(procedure, ref missing);
            Assert.That(missing.Cancelled, Is.False);
            var wrongParent = body.GetBodyChildrenOfType(patient, BodyPartType.Head).Single().Id;
            var invalid = new SurgeryValidEvent(patient, wrongParent);
            entMan.EventBus.RaiseLocalEvent(procedure, ref invalid);
            Assert.That(invalid.Cancelled, Is.True);
            Assert.That(body.AttachPart(parent, slot, leg), Is.True);
            entMan.DeleteEntity(patient);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ScannersReportResidualWoundsWithoutStoredDamage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = server.System<SharedBodySystem>();
            var wounds = server.System<WoundSystem>();
            var human = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var chest = body.GetBodyChildrenOfType(human, BodyPartType.Chest).Single().Id;
            Assert.That(wounds.TryInduceWound(chest, "Blunt", 40, out _), Is.True);
            Assert.That(entMan.GetComponent<DamageableComponent>(chest).TotalDamage, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(wounds.GetWoundableStatesOnBody(human)[TargetBodyPart.Chest], Is.Not.EqualTo(WoundableSeverity.Healthy));
            Assert.That(wounds.GetDamageableStatesOnBody(human)[TargetBodyPart.Chest],
                Is.EqualTo(wounds.GetWoundableStatesOnBody(human)[TargetBodyPart.Chest]));
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("Blunt", "Brute", "SurgeryTendWoundsBrute", "SurgeryStepRepairBruteTissue")]
    [TestCase("Heat", "Burn", "SurgeryTendWoundsBurn", "SurgeryStepRepairBurnTissue")]
    public async Task TendingDoAfterTargetsPatientAndStopsAfterHealing(string damageType, string damageGroup,
        string surgeryId, string stepId)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid surgeon = default;
        EntityUid patient = default;
        EntityUid chest = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            surgeon = entMan.SpawnEntity("MobHuman", map.GridCoords);
            patient = entMan.SpawnEntity("MobHuman", map.GridCoords);
            chest = server.System<SharedBodySystem>().GetBodyChildrenOfType(patient, BodyPartType.Chest).Single().Id;
            server.System<StandingStateSystem>().Down(patient, playSound: false, dropHeldItems: false);
            var tool = entMan.SpawnEntity("OmnimedTool", map.GridCoords);
            Assert.That(server.System<HandsSystem>().TryPickupAnyHand(surgeon, tool), Is.True);
            var wounds = server.System<WoundSystem>();
            Assert.That(wounds.TryInduceWound(chest, damageType, 1, out _), Is.True);
            wounds.TryHaltAllBleeding(chest);
            entMan.EnsureComponent<IncisionOpenComponent>(chest);

            Assert.That(server.System<SurgerySystem>().TryDoSurgeryStep(patient, chest, surgeon,
                surgeryId, stepId, out var error), Is.True, error.ToString());
            var action = entMan.GetComponent<DoAfterComponent>(surgeon).DoAfters.Values.Single();
            Assert.That(action.Args.Target, Is.EqualTo(patient));
            Assert.That(((SurgeryDoAfterEvent) action.Args.Event).Part, Is.EqualTo(entMan.GetNetEntity(chest)));
        });

        await server.WaitRunTicks(90);

        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<WoundSystem>().GetWoundableSeverityPoint(chest, damageGroup: damageGroup, healable: true),
                Is.EqualTo(FixedPoint2.Zero));
            Assert.That(server.EntMan.GetComponent<DoAfterComponent>(surgeon).DoAfters.Values
                .Any(action => !action.Completed && !action.Cancelled), Is.False);
            server.EntMan.DeleteEntity(patient);
            server.EntMan.DeleteEntity(surgeon);
        });

        await pair.CleanReturnAsync();
    }
}
