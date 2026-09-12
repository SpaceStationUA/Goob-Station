// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Server.Mind;
using Content.Shared._Pirate.MalfAI;
using Content.Shared._Pirate.MalfAI.Actions;
using Content.Shared.Players;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Pirate;

[TestFixture]
public sealed class MalfAiShuntTest
{
    [Test]
    public async Task ShuntToApcAndReturnRestoresCore()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false,
            Dirty = true
        });

        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        var minds = entMan.System<MindSystem>();
        var containers = entMan.System<SharedContainerSystem>();
        var session = server.PlayerMan.Sessions.Single();

        EntityUid ai = default;
        EntityUid core = default;
        EntityUid apc = default;

        await server.WaitAssertion(() =>
        {
            core = entMan.SpawnEntity("PlayerStationAiEmpty", map.GridCoords);
            entMan.System<SharedPowerReceiverSystem>().SetNeedsPower(core, false);
            ai = entMan.SpawnEntity("StationAiBrain", map.GridCoords);
            Assert.That(containers.Insert(ai, Holder(containers, core)), Is.True);

            var mind = session.ContentData()!.Mind;
            Assert.That(mind, Is.Not.Null);
            minds.TransferTo(mind!.Value, ai, ghostCheckOverride: true);
            server.PlayerMan.SetAttachedEntity(session, ai);
            entMan.EnsureComponent<MalfAiMarkerComponent>(ai);
            apc = entMan.SpawnEntity("APCBasic", map.GridCoords.Offset(new Vector2(2, 0)));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<StationAiHeldComponent>(ai), Is.True);
            var shunt = new MalfAiShuntToApcActionEvent { Performer = ai, Target = apc };
            entMan.EventBus.RaiseLocalEvent(ai, shunt, true);
            Assert.That(shunt.Handled, Is.True);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(Holder(containers, apc).ContainedEntities, Does.Contain(ai));
            Assert.That(entMan.HasComponent<StationAiCoreComponent>(apc), Is.True);
            Assert.That(entMan.GetComponent<StationAiCoreComponent>(apc).RemoteEntity, Is.Not.Null);

            var ret = new MalfAiReturnToCoreActionEvent { Performer = ai };
            entMan.EventBus.RaiseLocalEvent(ai, ret, true);
            Assert.That(ret.Handled, Is.True);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(Holder(containers, core).ContainedEntities, Does.Contain(ai));
                Assert.That(entMan.HasComponent<StationAiCoreComponent>(apc), Is.False);
                Assert.That(entMan.HasComponent<StationAiHolderComponent>(apc), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    private static ContainerSlot Holder(SharedContainerSystem containers, EntityUid holder)
        => containers.GetContainer(holder, StationAiHolderComponent.Container) as ContainerSlot
           ?? throw new AssertionException("AI holder is missing its brain slot.");
}
