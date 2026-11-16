// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Pirate.StarlightSpecies;

public sealed class NeoResomiDamageVisualsTest : InteractionTest
{
    // The legacy Resomi RSI lacks both thresholds used by native limb wound visuals.
    [TestCase("Slash", "Brute", 25, 20)]
    [TestCase("Slash", "Brute", 105, 100)]
    [TestCase("Heat", "Burn", 25, 20)]
    [TestCase("Heat", "Burn", 105, 100)]
    public async Task ChestWoundsUseValidNativeSprites(string damageType, string damageGroup, int damage, int threshold)
    {
        await AddAtmosphere();
        await SpawnTarget("MobNeoResomi");

        await Server.WaitAssertion(() =>
        {
            var chest = SEntMan.System<SharedBodySystem>()
                .GetBodyChildrenOfType(STarget!.Value, BodyPartType.Chest).Single().Id;
            SEntMan.System<DamageableSystem>().ChangeDamage(chest,
                new DamageSpecifier(ProtoMan.Index<DamageTypePrototype>(damageType), damage),
                ignoreResistances: true, canMiss: false);
        });
        await RunTicks(5);

        await Client.WaitAssertion(() =>
        {
            var sprite = CEntMan.GetComponent<SpriteComponent>(CTarget!.Value);
            var sprites = CEntMan.System<SpriteSystem>();
            Assert.That(sprites.LayerMapTryGet((CTarget.Value, sprite), $"Chest{damageGroup}", out var layer, false), Is.True);
            Assert.That(sprite[layer].Visible, Is.True, "chest wounds must remain visible");

            var state = sprites.LayerGetRsiState((CTarget.Value, sprite), layer);
            Assert.That(state.ToString(), Is.EqualTo($"Chest_{damageGroup}_{threshold}"));
            var rsi = sprites.LayerGetEffectiveRsi((CTarget.Value, sprite), layer);
            Assert.That(rsi, Is.Not.Null);
            Assert.That(rsi!.TryGetState(state, out _), Is.True, "the wound sprite must exist at this severity");
        });
    }

    [TearDown]
    public async Task DeletePatient()
    {
        await Server.WaitPost(() =>
        {
            if (STarget is { } target && !SEntMan.Deleted(target))
                SEntMan.DeleteEntity(target);
        });
        await RunTicks(2);
    }
}
