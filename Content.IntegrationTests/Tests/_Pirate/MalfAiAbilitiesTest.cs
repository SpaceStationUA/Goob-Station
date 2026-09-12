// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Server._Pirate.MalfAI;
using Content.Server.Construction.Components;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.Power.Components;
using Content.Server.Robotics.Systems;
using Content.Shared._Pirate.MalfAI;
using Content.Shared._Pirate.MalfAI.Actions;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.CombatMode;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Weapons.Melee;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Mind.Components;
using Content.Shared.Movement.Components;
using Content.Shared.NPC.Components;
using Content.Shared.Radio.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Store.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.Tests._Pirate;

[TestFixture]
public sealed class MalfAiAbilitiesTest
{
    [Test]
    public async Task DetonateRcdsDeletesEveryRcdOnTheAiGridAfterTheWarningDelay()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid ai = default;
        EntityUid rcd = default;

        await server.WaitAssertion(() =>
        {
            ai = entMan.SpawnEntity(null, map.GridCoords);
            entMan.EnsureComponent<StoreComponent>(ai);
            rcd = entMan.SpawnEntity("RCD", map.GridCoords.Offset(new Vector2(1, 0)));

            var action = new MalfAiDetonateRcdsActionEvent { Performer = ai };
            entMan.EventBus.RaiseLocalEvent(ai, action);
            Assert.That(action.Handled, Is.True);
            Assert.That(entMan.EntityExists(rcd), Is.True, "RCDs must survive until the warning countdown ends.");
        });

        await pair.RunSeconds(5.25f);
        await server.WaitAssertion(() =>
            Assert.That(entMan.EntityExists(rcd), Is.False,
                "The RCD detonation action did not remove the armed RCD after its five-second delay."));

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OverloadMachineQueuesAnExplosionAndRemovesThePoweredMachine()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid ai = default;
        EntityUid machine = default;

        await server.WaitAssertion(() =>
        {
            ai = entMan.SpawnEntity(null, map.GridCoords);
            entMan.EnsureComponent<MalfAiMarkerComponent>(ai);
            entMan.EnsureComponent<StationAiHeldComponent>(ai);
            machine = entMan.SpawnEntity("Autolathe", map.GridCoords.Offset(new Vector2(1, 0)));

            // Test maps have no power network. Mark this otherwise real machine powered so
            // the action exercises its successful branch rather than its power guard.
            if (entMan.TryGetComponent<ApcPowerReceiverComponent>(machine, out var receiver))
                receiver.Powered = true;

            var action = new MalfAiOverloadMachineActionEvent
            {
                Performer = ai,
                Target = entMan.GetComponent<TransformComponent>(machine).Coordinates,
            };
            entMan.EventBus.RaiseLocalEvent(ai, action);
            Assert.That(action.Handled, Is.True);
        });

        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
            Assert.That(entMan.EntityExists(machine), Is.False,
                "A successful overload must remove the targeted machine after queuing its explosion."));

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OverrideMachineMakesItAnUnanchoredHostileMobileMachine()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid ai = default;
        EntityUid machine = default;

        await server.WaitAssertion(() =>
        {
            ai = entMan.SpawnEntity(null, map.GridCoords);
            entMan.EnsureComponent<MalfAiMarkerComponent>(ai);
            entMan.EnsureComponent<StationAiHeldComponent>(ai);
            machine = entMan.SpawnEntity("Autolathe", map.GridCoords.Offset(new Vector2(1, 0)));

            var action = new MalfAiOverrideMachineActionEvent
            {
                Performer = ai,
                Target = entMan.GetComponent<TransformComponent>(machine).Coordinates,
            };
            entMan.EventBus.RaiseLocalEvent(ai, action);
            Assert.That(action.Handled, Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<TransformComponent>(machine).Anchored, Is.False);
                Assert.That(entMan.HasComponent<NpcFactionMemberComponent>(machine), Is.True);
                Assert.That(entMan.HasComponent<HTNComponent>(machine), Is.True);
                Assert.That(entMan.HasComponent<InputMoverComponent>(machine), Is.True);
                Assert.That(entMan.GetComponent<PhysicsComponent>(machine).BodyType, Is.EqualTo(BodyType.KinematicController));
                Assert.That(entMan.HasComponent<MobMoverComponent>(machine), Is.True);
                Assert.That(entMan.HasComponent<MeleeWeaponComponent>(machine), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SyndicateDecryptionAddsSyndicateTransmitAndReceiveChannels()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        EntityUid ai = default;

        await server.WaitAssertion(() =>
        {
            ai = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.EnsureComponent<MalfAiMarkerComponent>(ai);
            entMan.EventBus.RaiseLocalEvent(ai, new MalfAiSyndicateKeysUnlockedEvent());

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<IntrinsicRadioTransmitterComponent>(ai).Channels,
                    Does.Contain("Syndicate"));
                Assert.That(entMan.GetComponent<ActiveRadioComponent>(ai).Channels,
                    Does.Contain("Syndicate"));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GyroscopeTraversesTheAiCoreToTheAdjacentTileAndFinishesCleanly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var mapSystem = entMan.System<SharedMapSystem>();
        await server.WaitAssertion(() =>
        {
            // The default Empty test map has no tiles. Gyroscope's unobstructed
            // interaction check requires both the source and destination tiles.
            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(0, 0), new Tile(1));
            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(1, 0), new Tile(1));
        });
        EntityUid core = default;
        EntityUid ai = default;
        EntityUid actionUid = default;
        MapCoordinates start = default;

        await server.WaitAssertion(() =>
        {
            core = entMan.SpawnEntity("PlayerStationAiEmpty", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            entMan.System<Content.Shared.Power.EntitySystems.SharedPowerReceiverSystem>().SetNeedsPower(core, false);
            ai = entMan.SpawnEntity("StationAiBrain", map.GridCoords);
            var holder = entMan.System<SharedContainerSystem>().GetContainer(core, StationAiHolderComponent.Container);
            Assert.That(entMan.System<SharedContainerSystem>().Insert(ai, holder), Is.True);
            entMan.EnsureComponent<MalfAiMarkerComponent>(ai);
            start = entMan.System<SharedTransformSystem>().GetMapCoordinates(core);
            // The shop grants actions to the brain, not to its core holder.
            var action = entMan.System<SharedActionsSystem>().AddAction(ai, "ActionMalfAiGyroscope");
            Assert.That(action, Is.Not.Null);
            actionUid = action!.Value;

            var actionComp = entMan.GetComponent<ActionComponent>(actionUid);
            var gyro = new MalfAiGyroscopeActionEvent
            {
                Performer = ai,
                Action = (actionUid, actionComp),
                Target = map.GridCoords.Offset(new Vector2(1.5f, 0.5f)),
            };
            entMan.EventBus.RaiseLocalEvent(ai, gyro);
            Assert.That(gyro.Handled, Is.True);
            Assert.That(entMan.HasComponent<MalfGyroTraverseComponent>(core), Is.True);
        });

        await pair.RunSeconds(0.5f);
        await server.WaitAssertion(() =>
        {
            var end = entMan.System<SharedTransformSystem>().GetMapCoordinates(core);
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<MalfGyroTraverseComponent>(core), Is.False);
                Assert.That(end.Position, Is.Not.EqualTo(start.Position));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HijackMechInsertsTheAiAndReturnToCoreRestoresTheOriginalHolder()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid core = default;
        EntityUid ai = default;
        EntityUid mech = default;

        await server.WaitAssertion(() =>
        {
            core = entMan.SpawnEntity("PlayerStationAiEmpty", map.GridCoords);
            entMan.System<Content.Shared.Power.EntitySystems.SharedPowerReceiverSystem>().SetNeedsPower(core, false);
            ai = entMan.SpawnEntity("StationAiBrain", map.GridCoords);
            var holder = entMan.System<SharedContainerSystem>().GetContainer(
                core, StationAiHolderComponent.Container) as ContainerSlot;
            Assert.That(holder, Is.Not.Null);
            Assert.That(entMan.System<SharedContainerSystem>().Insert(ai, holder!), Is.True);
            entMan.EnsureComponent<MalfAiMarkerComponent>(ai);
            mech = entMan.SpawnEntity("MechRipley", map.GridCoords.Offset(new Vector2(1, 0)));

            var hijack = new MalfAiHijackMechActionEvent { Performer = ai, Target = mech };
            entMan.EventBus.RaiseLocalEvent(ai, hijack);
            Assert.That(hijack.Handled, Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<MechComponent>(mech).PilotSlot.ContainedEntity, Is.EqualTo(ai));
                Assert.That(entMan.HasComponent<MalfAiMechHijackComponent>(ai), Is.True);
                Assert.That(entMan.HasComponent<CombatModeComponent>(ai), Is.True);
            });

            var ret = new MalfAiReturnToCoreActionEvent { Performer = ai };
            entMan.EventBus.RaiseLocalEvent(ai, ret);
            Assert.That(ret.Handled, Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<MechComponent>(mech).PilotSlot.ContainedEntity, Is.Null);
                Assert.That(holder!.ContainedEntities, Does.Contain(ai));
                Assert.That(entMan.HasComponent<MalfAiMechHijackComponent>(ai), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BorgLawZeroControlRecordsTheMalfControllerAndChangesSiliconLaws()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        EntityUid ai = default;
        EntityUid borg = default;

        await server.WaitAssertion(() =>
        {
            ai = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.EnsureComponent<StationAiHeldComponent>(ai);
            borg = entMan.SpawnEntity("PlayerBorgBatteryNoMind", MapCoordinates.Nullspace);

            entMan.System<CyborgLawReceiverSystem>().ImposeLawZero(borg, ai);
            var laws = entMan.System<Content.Server.Silicons.Laws.SiliconLawSystem>().GetLaws(borg).Laws;
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<MalfAiControlledComponent>(borg), Is.True);
                Assert.That(entMan.GetComponent<MalfAiControlledComponent>(borg).Controller, Is.EqualTo(ai));
                Assert.That(entMan.GetComponent<MalfAiControlledComponent>(borg).UniqueId, Does.StartWith("borg-"));
                Assert.That((entMan.GetComponent<EmaggedComponent>(borg).EmagType & EmagType.Interaction)
                    == EmagType.Interaction, Is.True);
                Assert.That(laws.Any(law => law.LawIdentifierOverride == "0"), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ViewportSelectionCreatesAnAnchoredEyeAndRecordsTheTarget()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid ai = default;
        EntityUid anchor = default;

        await server.WaitAssertion(() =>
        {
            ai = entMan.SpawnEntity("StationAiBrain", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            entMan.EnsureComponent<StationAiHeldComponent>(ai);
            var target = map.GridCoords;
            var set = new MalfAiSetViewportActionEvent
            {
                Performer = ai,
                Target = target,
            };
            entMan.EventBus.RaiseLocalEvent(ai, set);
            Assert.That(set.Handled, Is.True);

            var viewport = entMan.GetComponent<MalfAiViewportComponent>(ai);
            Assert.That(viewport.Selected, Is.Not.Null);
            anchor = viewport.ViewportAnchor!.Value;
            Assert.Multiple(() =>
            {
                Assert.That(entMan.EntityExists(anchor), Is.True);
                Assert.That(entMan.HasComponent<EyeComponent>(anchor), Is.True);
                Assert.That(entMan.GetComponent<TransformComponent>(anchor).Anchored, Is.True);
                Assert.That(viewport.Selected!.Value.Position, Is.EqualTo(
                    entMan.System<SharedTransformSystem>().ToMapCoordinates(target).Position));
            });
        });

        await pair.CleanReturnAsync();
    }
}
