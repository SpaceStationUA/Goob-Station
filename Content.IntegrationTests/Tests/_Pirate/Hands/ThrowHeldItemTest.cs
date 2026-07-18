// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.IntegrationTests.Tests.Interaction;

namespace Content.IntegrationTests.Tests._Pirate.Hands;

public sealed class ThrowHeldItemTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    [Test]
    public async Task ManualThrowUsesOnlyActiveHandWhileDisarmCanFallback()
    {
        var item = ToServer(await PlaceInHands("Crowbar"));
        var heldHand = Hands!.ActiveHandId!;
        var emptyHand = Hands.SortedHands.First(hand => hand != heldHand);

        await Server.WaitPost(() =>
        {
            Assert.That(HandSys.TrySetActiveHand((SPlayer, Hands), emptyHand), Is.True);
        });
        await RunTicks(1);

        Assert.Multiple(() =>
        {
            Assert.That(HandSys.GetActiveItem((SPlayer, Hands)), Is.Null);
            Assert.That(HandSys.GetHeldItem((SPlayer, Hands), heldHand), Is.EqualTo(item));
        });

        Assert.That(await ThrowItem(), Is.False);
        Assert.That(HandSys.GetHeldItem((SPlayer, Hands), heldHand), Is.EqualTo(item));

        var disarmThrow = false;
        await Server.WaitPost(() =>
        {
            disarmThrow = HandSys.ThrowHeldItem(
                SPlayer,
                ToServer(TargetCoords),
                minDistance: 4f,
                allowInactiveHand: true);
        });
        await RunTicks(1);

        Assert.Multiple(() =>
        {
            Assert.That(disarmThrow, Is.True);
            Assert.That(HandSys.GetHeldItem((SPlayer, Hands), heldHand), Is.Null);
        });
    }
}
