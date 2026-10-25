// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.CombatMode;
using Content.Shared.MouseRotator;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Pirate.Singularity;

[TestFixture]
public sealed class MouseRotationTest
{
    [Test]
    public async Task InvalidCursorRotationCannotCorruptContainedToy()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var client = pair.Client;
        var entities = server.EntMan;
        EntityUid user = default;
        EntityUid toy = default;
        NetEntity netUser = default;

        await server.WaitAssertion(() =>
        {
            user = entities.SpawnEntity("MobHuman", map.GridCoords);
            toy = entities.SpawnEntity("SingularityToy", map.GridCoords);
            var bag = entities.SpawnEntity(null, map.GridCoords);
            var containers = server.System<SharedContainerSystem>();
            Assert.That(containers.Insert(bag, containers.EnsureContainer<ContainerSlot>(user, "test-bag")), Is.True);
            Assert.That(containers.Insert(toy, containers.EnsureContainer<ContainerSlot>(bag, "test-toy")), Is.True);
            server.PlayerMan.SetAttachedEntity(pair.Player!, user);
            server.System<SharedCombatModeSystem>().SetInCombatMode(user, true);
            Assert.That(entities.HasComponent<MouseRotatorComponent>(user), Is.True);
            netUser = entities.GetNetEntity(user);
        });
        await pair.RunTicksSync(10);

        // Check that a valid request reaches the server before testing rejection of invalid ones.
        await client.WaitPost(() => client.EntMan.RaisePredictiveEvent(new RequestMouseRotatorRotationEvent
        {
            User = netUser,
            Rotation = new Angle(0.75),
        }));
        await pair.RunTicksSync(10);
        await server.WaitAssertion(() =>
            Assert.That(entities.GetComponent<TransformComponent>(user).LocalRotation.Theta, Is.EqualTo(0.75).Within(0.00001)));

        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            await client.WaitPost(() => client.EntMan.RaisePredictiveEvent(new RequestMouseRotatorRotationEvent
            {
                User = netUser,
                Rotation = new Angle(invalid),
            }));
            await pair.RunTicksSync(10);

            await server.WaitAssertion(() =>
            {
                Assert.That(double.IsFinite(entities.GetComponent<TransformComponent>(user).LocalRotation.Theta), Is.True);
                var position = server.System<SharedTransformSystem>().GetWorldPosition(toy);
                Assert.That(float.IsFinite(position.X) && float.IsFinite(position.Y), Is.True);
            });
            await client.WaitAssertion(() =>
            {
                var position = client.System<SharedTransformSystem>().GetWorldPosition(pair.ToClientUid(toy));
                Assert.That(float.IsFinite(position.X) && float.IsFinite(position.Y), Is.True);
                Assert.That(double.IsFinite(client.EntMan.GetComponent<TransformComponent>(pair.ToClientUid(user)).LocalRotation.Theta), Is.True);
            });
        }

        await pair.CleanReturnAsync();
    }
}
