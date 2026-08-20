// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Server.Body.Systems;
using Content.Server.Hands.Systems;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Shared._Pirate.Medical.LimbFixation;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Steps;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body.Part;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Pirate.Medical;

[TestFixture]
public sealed class LimbFixationTest
{
    [Test]
    public async Task TraumaticDismembermentDisablesAndSurgeryRestoresPart()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
            InLobby = false,
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var body = entMan.System<BodySystem>();
        var hands = entMan.System<HandsSystem>();
        var surgery = entMan.System<SurgerySystem>();
        var wounds = entMan.System<WoundSystem>();
        var human = EntityUid.Invalid;

        await server.WaitAssertion(() =>
        {
            human = entMan.Spawn("MobHuman");
            entMan.EnsureComponent<LimbFixationComponent>(human);
        });
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var arm = body.GetBodyChildrenOfType(
                    human,
                    BodyPartType.Arm,
                    symmetry: BodyPartSymmetry.Left)
                .Single();
            var hand = body.GetBodyChildrenOfType(
                    human,
                    BodyPartType.Hand,
                    symmetry: BodyPartSymmetry.Left)
                .Single();
            var armWoundable = entMan.GetComponent<WoundableComponent>(arm.Id);
            var beforeTrauma = new BeforeTraumaInducedEvent(
                FixedPoint2.New(50),
                armWoundable.ParentWoundable!.Value,
                TraumaSystem.Dismemberment);

            entMan.EventBus.RaiseLocalEvent(arm.Id, ref beforeTrauma);

            Assert.Multiple(() =>
            {
                Assert.That(beforeTrauma.Cancelled, Is.True);
                Assert.That(entMan.HasComponent<LimbFixationDamageComponent>(arm.Id), Is.True);
                Assert.That(arm.Component.Enabled, Is.False);
                Assert.That(hand.Component.Enabled, Is.False);
                Assert.That(hands.EnumerateHands(human).Count(), Is.EqualTo(1));
                Assert.That(
                    wounds.GetDamageableStatesOnBody(human)[TargetBodyPart.LeftArm],
                    Is.EqualTo(WoundableSeverity.Disabled));
            });

            var restoreStep = surgery.GetSingleton("SurgeryStepRestoreLimbFunction");
            Assert.That(restoreStep, Is.Not.Null);
            var restoreStepId = restoreStep!.Value;

            var beforeRestore = new SurgeryStepCompleteCheckEvent(human, arm.Id, EntityUid.Invalid);
            entMan.EventBus.RaiseLocalEvent(restoreStepId, ref beforeRestore);
            Assert.That(beforeRestore.Cancelled, Is.True);

            var restore = new SurgeryStepEvent(
                human,
                human,
                arm.Id,
                human,
                EntityUid.Invalid,
                restoreStepId,
                false);
            entMan.EventBus.RaiseLocalEvent(restoreStepId, ref restore);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<LimbFixationDamageComponent>(arm.Id), Is.False);
                Assert.That(arm.Component.Enabled, Is.True);
                Assert.That(hand.Component.Enabled, Is.True);
                Assert.That(hands.EnumerateHands(human).Count(), Is.EqualTo(2));
                Assert.That(
                    entMan.GetComponent<TargetingComponent>(human).BodyStatus[TargetBodyPart.LeftArm],
                    Is.Not.EqualTo(WoundableSeverity.Disabled));
            });

            var afterRestore = new SurgeryStepCompleteCheckEvent(human, arm.Id, EntityUid.Invalid);
            entMan.EventBus.RaiseLocalEvent(restoreStepId, ref afterRestore);
            Assert.That(afterRestore.Cancelled, Is.False);

            wounds.AmputateWoundable(
                armWoundable.ParentWoundable!.Value,
                arm.Id,
                armWoundable);

            Assert.Multiple(() =>
            {
                Assert.That(arm.Component.Body, Is.EqualTo(human));
                Assert.That(entMan.HasComponent<LimbFixationDamageComponent>(arm.Id), Is.True);
            });

            entMan.RemoveComponent<LimbFixationDamageComponent>(arm.Id);

            var integrityChanged = new WoundableIntegrityChangedEvent(
                armWoundable.WoundableIntegrity,
                FixedPoint2.Zero);
            entMan.EventBus.RaiseLocalEvent(arm.Id, ref integrityChanged);

            Assert.That(entMan.HasComponent<LimbFixationDamageComponent>(arm.Id), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RestoreFunctionLeavesPartCriticallyDamaged()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
            InLobby = false,
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var body = entMan.System<BodySystem>();
        var surgery = entMan.System<SurgerySystem>();
        var wounds = entMan.System<WoundSystem>();

        await server.WaitAssertion(() =>
        {
            var human = entMan.Spawn("MobHuman");
            entMan.EnsureComponent<LimbFixationComponent>(human);

            var leg = body.GetBodyChildrenOfType(
                    human,
                    BodyPartType.Leg,
                    symmetry: BodyPartSymmetry.Left)
                .Single();
            var woundable = entMan.GetComponent<WoundableComponent>(leg.Id);

            Assert.That(
                wounds.TryInduceWound(leg.Id, "Blunt", woundable.IntegrityCap, out _, woundable),
                Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(woundable.WoundableIntegrity, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(entMan.HasComponent<LimbFixationDamageComponent>(leg.Id), Is.True);
                Assert.That(
                    entMan.GetComponent<TargetingComponent>(human).BodyStatus[TargetBodyPart.LeftLeg],
                    Is.EqualTo(WoundableSeverity.Disabled));
            });

            var restoreStep = surgery.GetSingleton("SurgeryStepRestoreLimbFunction");
            Assert.That(restoreStep, Is.Not.Null);

            var restore = new SurgeryStepEvent(
                human,
                human,
                leg.Id,
                human,
                EntityUid.Invalid,
                restoreStep!.Value,
                false);
            entMan.EventBus.RaiseLocalEvent(restoreStep.Value, ref restore);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<LimbFixationDamageComponent>(leg.Id), Is.False);
                Assert.That(
                    woundable.WoundableIntegrity,
                    Is.EqualTo(woundable.Thresholds[WoundableSeverity.Critical]));
                Assert.That(woundable.WoundableSeverity, Is.EqualTo(WoundableSeverity.Critical));
                Assert.That(
                    entMan.GetComponent<TargetingComponent>(human).BodyStatus[TargetBodyPart.LeftLeg],
                    Is.EqualTo(WoundableSeverity.Critical));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DisabledPartsBlockHealingSurgeriesButAllowAmputation()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
            InLobby = false,
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var body = entMan.System<BodySystem>();
        var surgery = entMan.System<SurgerySystem>();

        await server.WaitAssertion(() =>
        {
            var human = entMan.Spawn("MobHuman");
            entMan.EnsureComponent<LimbFixationComponent>(human);

            var leg = body.GetBodyChildrenOfType(
                    human,
                    BodyPartType.Leg,
                    symmetry: BodyPartSymmetry.Left)
                .Single();
            var foot = body.GetBodyChildrenOfType(
                    human,
                    BodyPartType.Foot,
                    symmetry: BodyPartSymmetry.Left)
                .Single();
            entMan.EnsureComponent<LimbFixationDamageComponent>(leg.Id);

            Assert.That(entMan.HasComponent<LimbFixationDisabledComponent>(foot.Id), Is.True);

            foreach (var surgeryId in new[]
                     {
                         "SurgeryMendBones",
                         "SurgeryTendWoundsBrute",
                         "SurgeryTendWoundsBurn",
                     })
            {
                var healingSurgery = surgery.GetSingleton(surgeryId);
                Assert.That(healingSurgery, Is.Not.Null);
                Assert.That(
                    entMan.HasComponent<SurgeryFunctionalPartConditionComponent>(healingSurgery!.Value),
                    Is.True,
                    $"{surgeryId} should require a functional body part");
            }

            foreach (var surgeryId in new[] { "SurgeryRemovePart", "SurgeryAttachLeftLeg" })
            {
                var unaffectedSurgery = surgery.GetSingleton(surgeryId);
                Assert.That(unaffectedSurgery, Is.Not.Null);
                Assert.That(
                    entMan.HasComponent<SurgeryFunctionalPartConditionComponent>(unaffectedSurgery!.Value),
                    Is.False,
                    $"{surgeryId} should remain available for disabled parts");
            }

            var condition = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.EnsureComponent<SurgeryFunctionalPartConditionComponent>(condition);

            var blocked = new SurgeryValidEvent(human, leg.Id);
            entMan.EventBus.RaiseLocalEvent(condition, ref blocked);
            Assert.That(blocked.Cancelled, Is.True);

            var indirectlyBlocked = new SurgeryValidEvent(human, foot.Id);
            entMan.EventBus.RaiseLocalEvent(condition, ref indirectlyBlocked);
            Assert.That(indirectlyBlocked.Cancelled, Is.True);

            var amputation = surgery.GetSingleton("SurgeryRemovePart");
            Assert.That(amputation, Is.Not.Null);
            var amputationValid = new SurgeryValidEvent(human, leg.Id);
            entMan.EventBus.RaiseLocalEvent(amputation!.Value, ref amputationValid);
            Assert.That(amputationValid.Cancelled, Is.False);

            entMan.RemoveComponent<LimbFixationDamageComponent>(leg.Id);

            var unblocked = new SurgeryValidEvent(human, leg.Id);
            entMan.EventBus.RaiseLocalEvent(condition, ref unblocked);
            Assert.That(unblocked.Cancelled, Is.False);

            var indirectlyUnblocked = new SurgeryValidEvent(human, foot.Id);
            entMan.EventBus.RaiseLocalEvent(condition, ref indirectlyUnblocked);
            Assert.That(indirectlyUnblocked.Cancelled, Is.False);

            var rightFoot = body.GetBodyChildrenOfType(
                    human,
                    BodyPartType.Foot,
                    symmetry: BodyPartSymmetry.Right)
                .Single();
            var rightLeg = body.GetBodyChildrenOfType(
                    human,
                    BodyPartType.Leg,
                    symmetry: BodyPartSymmetry.Right)
                .Single();
            entMan.EnsureComponent<LimbFixationDamageComponent>(rightFoot.Id);

            Assert.That(entMan.HasComponent<LimbFixationDisabledComponent>(rightLeg.Id), Is.True);

            var legDisabledByFoot = new SurgeryValidEvent(human, rightLeg.Id);
            entMan.EventBus.RaiseLocalEvent(condition, ref legDisabledByFoot);
            Assert.That(legDisabledByFoot.Cancelled, Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RestoreSurgeryIsAvailableForBleedingDamagedHead()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
            InLobby = false,
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var body = entMan.System<BodySystem>();
        var surgery = entMan.System<SurgerySystem>();

        await server.WaitAssertion(() =>
        {
            var human = entMan.Spawn("MobHuman");
            entMan.EnsureComponent<LimbFixationComponent>(human);

            var head = body.GetBodyChildrenOfType(human, BodyPartType.Head).Single();
            var woundable = entMan.GetComponent<WoundableComponent>(head.Id);
            woundable.Bleeds = FixedPoint2.New(1);
            entMan.EnsureComponent<LimbFixationDamageComponent>(head.Id);

            var restoreSurgery = surgery.GetSingleton("SurgeryRestoreLimbFunction");
            Assert.That(restoreSurgery, Is.Not.Null);
            var restoreSurgeryId = restoreSurgery!.Value;

            var valid = new SurgeryValidEvent(human, head.Id);
            entMan.EventBus.RaiseLocalEvent(restoreSurgeryId, ref valid);

            Assert.That(valid.Cancelled, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RestoreSurgeryTargetsExactDamagedPart()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
            InLobby = false,
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var body = entMan.System<BodySystem>();
        var surgery = entMan.System<SurgerySystem>();

        await server.WaitAssertion(() =>
        {
            var human = entMan.Spawn("MobHuman");
            entMan.EnsureComponent<LimbFixationComponent>(human);

            var restoreSurgery = surgery.GetSingleton("SurgeryRestoreLimbFunction");
            Assert.That(restoreSurgery, Is.Not.Null);
            var restoreSurgeryId = restoreSurgery!.Value;

            var pairs = new[]
            {
                (Damaged: BodyPartType.Arm, Other: BodyPartType.Hand),
                (Damaged: BodyPartType.Hand, Other: BodyPartType.Arm),
                (Damaged: BodyPartType.Leg, Other: BodyPartType.Foot),
                (Damaged: BodyPartType.Foot, Other: BodyPartType.Leg),
            };

            foreach (var (damagedType, otherType) in pairs)
            {
                var damaged = body.GetBodyChildrenOfType(
                        human,
                        damagedType,
                        symmetry: BodyPartSymmetry.Left)
                    .Single();
                var other = body.GetBodyChildrenOfType(
                        human,
                        otherType,
                        symmetry: BodyPartSymmetry.Left)
                    .Single();

                entMan.EnsureComponent<LimbFixationDamageComponent>(damaged.Id);

                var targeting = entMan.GetComponent<TargetingComponent>(human);
                Assert.Multiple(() =>
                {
                    Assert.That(
                        targeting.BodyStatus[body.GetTargetBodyPart(damaged.Component)],
                        Is.EqualTo(WoundableSeverity.Disabled));
                    Assert.That(
                        targeting.BodyStatus[body.GetTargetBodyPart(other.Component)],
                        Is.Not.EqualTo(WoundableSeverity.Disabled));
                });

                var damagedValid = new SurgeryValidEvent(human, damaged.Id);
                entMan.EventBus.RaiseLocalEvent(restoreSurgeryId, ref damagedValid);
                var otherValid = new SurgeryValidEvent(human, other.Id);
                entMan.EventBus.RaiseLocalEvent(restoreSurgeryId, ref otherValid);

                Assert.Multiple(() =>
                {
                    Assert.That(damagedValid.Cancelled, Is.False, $"{damagedType} should be repairable");
                    Assert.That(otherValid.Cancelled, Is.True, $"{otherType} should not inherit the repair surgery");
                });

                entMan.RemoveComponent<LimbFixationDamageComponent>(damaged.Id);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DestroyedHeadRedirectsFurtherDamageToChest()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
            InLobby = false,
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var body = entMan.System<BodySystem>();
        var wounds = entMan.System<WoundSystem>();

        await server.WaitAssertion(() =>
        {
            var human = entMan.Spawn("MobHuman");
            entMan.EnsureComponent<LimbFixationComponent>(human);

            var head = body.GetBodyChildrenOfType(human, BodyPartType.Head).Single();
            var chest = body.GetBodyChildrenOfType(human, BodyPartType.Chest).Single();
            var headWoundable = entMan.GetComponent<WoundableComponent>(head.Id);
            headWoundable.WoundableIntegrity = FixedPoint2.Zero;

            Assert.That(
                wounds.GetDamageRedirectTarget(human, head.Id, "Piercing"),
                Is.EqualTo(chest.Id));
        });

        await pair.CleanReturnAsync();
    }
}
