// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Server.Body.Systems;
using Content.Server.Hands.Systems;
using Content.Shared._Pirate.Medical.LimbFixation;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body.Part;
using Robust.Shared.GameObjects;

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
                TraumaType.Dismemberment);

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

            entMan.RemoveComponent<LimbFixationDamageComponent>(arm.Id);

            Assert.Multiple(() =>
            {
                Assert.That(arm.Component.Enabled, Is.True);
                Assert.That(hand.Component.Enabled, Is.True);
                Assert.That(hands.EnumerateHands(human).Count(), Is.EqualTo(2));
            });

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
