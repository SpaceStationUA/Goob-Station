// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.GrabIntent;
using Content.IntegrationTests.Tests.Movement;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.Input;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.Puller;

[TestFixture]
public sealed class NonGrabbablePullEscapeTest : MovementTest
{
    protected override string PlayerPrototype => "NonGrabbablePullEscapeTestMob";

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
        var puller = await SpawnEntity("MobHuman", ToServer(TargetCoords));
        var pulling = SEntMan.System<PullingSystem>();
        var pullable = Comp<PullableComponent>(Player);

        Assert.That(HasComp<GrabbableComponent>(Player), Is.False);

        await Server.WaitAssertion(() =>
        {
            Assert.That(pulling.TryStartPull(puller, SPlayer), Is.True);
            Assert.That(pullable.Puller, Is.EqualTo(puller));
        });

        await RunTicks(5);
        await SetMovementKey(DirectionFlag.West, BoundKeyState.Down);
        await RunTicks(5);
        await SetMovementKey(DirectionFlag.West, BoundKeyState.Up);
        await RunTicks(1);

        Assert.That(pullable.Puller, Is.Null);
    }
}
