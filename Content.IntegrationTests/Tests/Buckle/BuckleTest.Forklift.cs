// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Buckle;

public sealed partial class BuckleTest
{
    [TestCase("VehicleForklift", true)]
    [TestCase(StrapDummyId, false)]
    public async Task SmallSeatDisplacement(string seatPrototype, bool staysSeated)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var ents = pair.Server.ResolveDependency<IEntityManager>();

        await pair.Server.WaitAssertion(() =>
        {
            var rider = ents.SpawnAtPosition("MobHuman", map.GridCoords);
            var seat = ents.SpawnAtPosition(seatPrototype, map.GridCoords);
            var buckle = ents.GetComponent<BuckleComponent>(rider);
            var offset = ents.GetComponent<StrapComponent>(seat).BuckleOffset;
            var transforms = ents.System<SharedTransformSystem>();
            Assert.That(ents.System<SharedBuckleSystem>().TryBuckle(rider, rider, seat), Is.True);

            transforms.SetLocalPosition(rider, offset + new Vector2(0.1f, 0));
            Assert.That(buckle.Buckled, Is.EqualTo(staysSeated));
            if (staysSeated)
            {
                Assert.That(ents.GetComponent<TransformComponent>(rider).LocalPosition, Is.EqualTo(offset));
                transforms.SetLocalPosition(rider, offset + new Vector2(0.4f, 0));
                Assert.That(buckle.Buckled, Is.False);
            }
        });

        await pair.CleanReturnAsync();
    }
}
