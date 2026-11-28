// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Physics;
using Content.Shared.Humanoid;
using Content.Shared.Input;
using Content.Shared.Speech.Components;
using Content.Shared.Standing;
using Robust.Client.GameObjects;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests._Pirate.Humanoid;

public sealed class OviniaAppearanceTest : InteractionTest
{
    protected override string PlayerPrototype => "MobOvinia";

    [Test]
    public async Task OutfitCrawlAndGulp()
    {
        var table = await Spawn("Table");
        await Server.WaitAssertion(() =>
        {
            foreach (var sounds in SEntMan.GetComponent<VocalComponent>(SPlayer).Sounds!.Values)
                Assert.That(Server.ProtoMan.Index<EmoteSoundsPrototype>(sounds).Sounds.ContainsKey("Gulp"), Is.True);
            Assert.That(Server.System<StandingStateSystem>().Down(SPlayer, force: true), Is.True);
        });

        await Pair.RunTicksSync(150);
        await PressKey(ContentKeyFunctions.ToggleCrawlingUnder);
        await Pair.RunTicksSync(10);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<StandingStateComponent>(SPlayer).IsCrawlingUnder, Is.True);
            var mask = Server.System<FixtureSystem>().GetFixtureOrNull(SPlayer, "fix1")!.CollisionMask;
            Assert.That(mask & (int) CollisionGroup.MidImpassable, Is.Zero);
        });
        await Client.WaitAssertion(() =>
        {
            var sprite = CEntMan.GetComponent<SpriteComponent>(CPlayer);
            var sprites = Client.System<SpriteSystem>();
            int Layer(string key)
            {
                Assert.That(sprites.LayerMapTryGet((CPlayer, sprite), key, out var layer, false), Is.True, key);
                return layer;
            }
            Assert.That(Layer("belt"), Is.GreaterThan(Layer("outerClothing")));
            Assert.That(sprites.LayerMapTryGet((CPlayer, sprite), HumanoidVisualLayers.Hair, out var hair, false), Is.True);
            Assert.That(Layer("maskalt"), Is.LessThan(hair));
            Assert.That(Layer("mask"), Is.GreaterThan(hair));
            Assert.That(sprite.DrawDepth, Is.LessThan(CEntMan.GetComponent<SpriteComponent>(ToClient(table)).DrawDepth));
        });
        await Server.WaitPost(() => Server.System<StandingStateSystem>().Stand(SPlayer, force: true));
        await Pair.RunTicksSync(10);
        await Client.WaitAssertion(() => Assert.That(CEntMan.GetComponent<SpriteComponent>(CPlayer).DrawDepth,
            Is.GreaterThan(CEntMan.GetComponent<SpriteComponent>(ToClient(table)).DrawDepth)));
    }
}
