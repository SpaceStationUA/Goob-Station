// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.GrabIntent;
using Content.IntegrationTests.Tests.Movement;
using Content.Shared.Input;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.Puller;

[TestFixture]
public sealed class NonGrabbablePullEscapeTest : MovementTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: NonGrabbablePullEscapeTestMob
          parent: BaseMob
          components:
          - type: Pullable
        """;

    [Test]
    public async Task MoveInputStopsOrdinaryPull()
    {
        await SpawnTarget("NonGrabbablePullEscapeTestMob");

        var puller = Comp<PullerComponent>(Player);
        var pullable = Comp<PullableComponent>(Target);
        var target = STarget!.Value;

        Assert.That(HasComp<GrabbableComponent>(Target), Is.False);

        await PressKey(ContentKeyFunctions.TryPullObject);
        await RunTicks(5);

        Assert.That(puller.Pulling, Is.EqualTo(target));
        Assert.That(pullable.Puller, Is.EqualTo(SPlayer));

        await Server.WaitPost(() =>
        {
            var mover = SEntMan.GetComponent<InputMoverComponent>(target);
            var ev = new MoveInputEvent((target, mover), mover.HeldMoveButtons, Direction.West, true);
            SEntMan.EventBus.RaiseLocalEvent(target, ref ev);
        });
        await RunTicks(5);

        Assert.That(pullable.Puller, Is.Null);
        Assert.That(puller.Pulling, Is.Null);
    }
}
