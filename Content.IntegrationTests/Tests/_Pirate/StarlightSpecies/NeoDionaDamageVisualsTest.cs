// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Pirate.StarlightSpecies;

public sealed class NeoDionaDamageVisualsTest : InteractionTest
{
    [TestCase("MobNeoDiona", "Blunt", "Brute")]
    [TestCase("MobNeoDiona", "Heat", "Burn")]
    [TestCase("MobDiona", "Blunt", "Brute")]
    [TestCase("MobDiona", "Heat", "Burn")]
    [TestCase("MobNeoResomi", "Blunt", "Brute")]
    [TestCase("MobNeoResomi", "Heat", "Burn")]
    public async Task DamageAfterLimbRemovalPreservesBodySprites(string prototype, string damageType, string damageGroup)
    {
        await AddAtmosphere();
        await SpawnTarget(prototype);
        EntityUid rightLeg = default;
        string groinState = null!;

        await Client.WaitAssertion(() =>
        {
            var sprite = CEntMan.GetComponent<SpriteComponent>(CTarget!.Value);
            var sprites = CEntMan.System<SpriteSystem>();
            var groin = sprites.LayerMapGet((CTarget.Value, sprite), HumanoidVisualLayers.Groin);
            groinState = sprites.LayerGetRsiState((CTarget.Value, sprite), groin).ToString();
        });

        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.System<SharedBodySystem>();
            rightLeg = body.GetBodyChildrenOfType(STarget!.Value, BodyPartType.Leg, symmetry: BodyPartSymmetry.Right).Single().Id;
            SEntMan.System<DamageableSystem>().ChangeDamage(rightLeg,
                new DamageSpecifier(ProtoMan.Index<DamageTypePrototype>(damageType), 20), ignoreResistances: true);
        });
        await RunTicks(5);
        await AssertWoundOverlay("RLeg", damageGroup);

        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.System<SharedBodySystem>().TryDetachPart(rightLeg), Is.True));
        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var leftLeg = SEntMan.System<SharedBodySystem>()
                .GetBodyChildrenOfType(STarget!.Value, BodyPartType.Leg, symmetry: BodyPartSymmetry.Left).Single().Id;
            // Cross another body-wide damage threshold after the removed leg's overlays are gone.
            SEntMan.System<DamageableSystem>().ChangeDamage(leftLeg,
                new DamageSpecifier(ProtoMan.Index<DamageTypePrototype>(damageType), 35), ignoreResistances: true);
        });
        await RunTicks(5);
        await AssertWoundOverlay("LLeg", damageGroup);

        await Client.WaitAssertion(() =>
        {
            var sprite = CEntMan.GetComponent<SpriteComponent>(CTarget!.Value);
            var sprites = CEntMan.System<SpriteSystem>();
            Assert.That(sprites.LayerMapTryGet((CTarget.Value, sprite), $"RLeg{damageGroup}", out _, false), Is.False);
            var groin = sprites.LayerMapGet((CTarget.Value, sprite), HumanoidVisualLayers.Groin);
            Assert.That(sprites.LayerGetRsiState((CTarget.Value, sprite), groin).ToString(), Is.EqualTo(groinState),
                "damage for a missing limb must not overwrite the first body layer");
        });

        await Server.WaitPost(() => SEntMan.DeleteEntity(rightLeg));
    }

    private Task AssertWoundOverlay(string limb, string group)
    {
        return Client.WaitAssertion(() =>
        {
            var sprite = CEntMan.GetComponent<SpriteComponent>(CTarget!.Value);
            var sprites = CEntMan.System<SpriteSystem>();
            Assert.That(sprites.LayerMapTryGet((CTarget.Value, sprite), $"{limb}{group}", out var index, false), Is.True);
            Assert.That(sprite[index].Visible, Is.True, "native wound visuals must still show the injury");
            var state = sprites.LayerGetRsiState((CTarget.Value, sprite), index);
            var rsi = sprites.LayerGetEffectiveRsi((CTarget.Value, sprite), index);
            Assert.That(rsi, Is.Not.Null);
            Assert.That(rsi!.TryGetState(state, out _), Is.True, "the damage state must exist in its overlay RSI");
        });
    }

    [TearDown]
    public async Task DeletePatient()
    {
        // Complex bodies must be deleted before the interaction map is removed.
        await Server.WaitPost(() =>
        {
            if (STarget is { } target && !SEntMan.Deleted(target))
                SEntMan.DeleteEntity(target);
        });
        await RunTicks(2);
    }
}
