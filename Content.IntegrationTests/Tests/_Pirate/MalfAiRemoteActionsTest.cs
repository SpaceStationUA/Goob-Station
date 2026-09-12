// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Server.Station.Systems;
using Content.Shared._Pirate.MalfAI;
using Content.Shared._Pirate.MalfAI.Actions;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Emp;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Pirate;

[TestFixture]
public sealed class MalfAiRemoteActionsTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: MalfRemoteTestStation
  parent: BaseStation
";

    [TestCase(false)]
    [TestCase(true)]
    public async Task WallsRemainLimitedToOriginalCoreWhileEyeAndApcAreFarAway(bool shunt)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid ai = default;
        await server.WaitAssertion(() =>
        {
            var maps = entMan.System<SharedMapSystem>();
            for (var x = 0; x <= 20; x++)
                maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, 0), new Tile(1));
            var core = entMan.SpawnEntity("PlayerStationAiEmpty", new EntityCoordinates(map.Grid.Owner, 0.5f, 0.5f));
            entMan.System<SharedPowerReceiverSystem>().SetNeedsPower(core, false);
            ai = entMan.SpawnEntity("StationAiBrain", map.GridCoords);
            var containers = entMan.System<SharedContainerSystem>();
            Assert.That(containers.Insert(ai, containers.GetContainer(core, StationAiHolderComponent.Container)), Is.True);
            entMan.EnsureComponent<MalfAiMarkerComponent>(ai);
            if (shunt)
            {
                var apc = entMan.SpawnEntity("APCBasic", new EntityCoordinates(map.Grid.Owner, 18.5f, 0.5f));
                var action = new MalfAiShuntToApcActionEvent { Performer = ai, Target = apc };
                entMan.EventBus.RaiseLocalEvent(ai, action);
                Assert.That(action.Handled, Is.True);
                core = apc;
            }
            var eye = entMan.GetComponent<StationAiCoreComponent>(core).RemoteEntity!.Value;
            entMan.System<SharedTransformSystem>().SetCoordinates(eye, new EntityCoordinates(map.Grid.Owner, 15.5f, 0.5f));
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            var far = new MalfAiBuildWallActionEvent { Performer = ai, Target = new EntityCoordinates(map.Grid.Owner, 15.5f, 0.5f) };
            entMan.EventBus.RaiseLocalEvent(ai, far);
            Assert.That(far.Handled, Is.False, "Neither the eye nor a shunted APC may extend the original core's build range.");
            var near = new MalfAiBuildWallActionEvent { Performer = ai, Target = new EntityCoordinates(map.Grid.Owner, 3.5f, 0.5f) };
            entMan.EventBus.RaiseLocalEvent(ai, near);
            Assert.That(near.Handled, Is.True);
        });
        await pair.RunSeconds(3.5f);
        await server.WaitAssertion(() =>
            Assert.That(map.Grid.Comp.GetAnchoredEntities(new Vector2i(3, 0)).Any(uid =>
                entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "WallSolid"), Is.True));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EmpDrainsEnemyButPreservesAiAndOwnedBorgBatteries()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid ai = default;
        EntityUid core = default;
        EntityUid enemy = default;
        EntityUid ally = default;
        await server.WaitAssertion(() =>
        {
            var maps = entMan.System<SharedMapSystem>();
            for (var x = 0; x < 4; x++)
                maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, 0), new Tile(1));
            var station = entMan.SpawnEntity("MalfRemoteTestStation", MapCoordinates.Nullspace);
            entMan.System<StationSystem>().AddGridToStation(station, map.Grid);
            core = entMan.SpawnEntity("PlayerStationAiEmpty", new EntityCoordinates(map.Grid.Owner, 0.5f, 0.5f));
            entMan.System<SharedPowerReceiverSystem>().SetNeedsPower(core, false);
            ai = entMan.SpawnEntity("StationAiBrain", map.GridCoords);
            var containers = entMan.System<SharedContainerSystem>();
            Assert.That(containers.Insert(ai, containers.GetContainer(core, StationAiHolderComponent.Container)), Is.True);
            entMan.EnsureComponent<MalfAiMarkerComponent>(ai);
            enemy = entMan.SpawnEntity("PlayerBorgBatteryNoMind", new EntityCoordinates(map.Grid.Owner, 2.5f, 0.5f));
            ally = entMan.SpawnEntity("PlayerBorgBatteryNoMind", new EntityCoordinates(map.Grid.Owner, 1.5f, 0.5f));
            var ipc = entMan.SpawnEntity("MobIPC", new EntityCoordinates(map.Grid.Owner, 3.5f, 0.5f));
            var actions = entMan.System<SharedActionsSystem>();
            var empAction = actions.AddAction(ai, "ActionMalfAiEmp")!.Value;
            var subvertAction = actions.AddAction(ai, "ActionMalfAiSubvertBorg")!.Value;
            // Exercise the same target validation as a player's click, not just the handler.
            Assert.That(actions.ValidateEntityTarget(ai, enemy,
                (empAction, entMan.GetComponent<EntityTargetActionComponent>(empAction))), Is.True);
            Assert.That(actions.ValidateEntityTarget(ai, ipc,
                (empAction, entMan.GetComponent<EntityTargetActionComponent>(empAction))), Is.True);
            Assert.That(actions.ValidateEntityTarget(ai, ally,
                (subvertAction, entMan.GetComponent<EntityTargetActionComponent>(subvertAction))), Is.True);
            var subvert = new MalfAiSubvertBorgActionEvent { Performer = ai, Target = ally };
            entMan.EventBus.RaiseLocalEvent(ai, subvert);
            Assert.That(subvert.Handled, Is.True);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            var containers = entMan.System<SharedContainerSystem>();
            var friendlyCell = containers.GetContainer(ally, "cell_slot").ContainedEntities.Single();
            var enemyCell = containers.GetContainer(enemy, "cell_slot").ContainedEntities.Single();
            var batteries = entMan.System<SharedBatterySystem>();
            var friendlyCharge = batteries.GetCharge(friendlyCell);
            Assert.That(batteries.GetCharge(enemyCell), Is.GreaterThan(0));
            var emp = new MalfAiEmpActionEvent { Performer = ai, Target = enemy };
            entMan.EventBus.RaiseLocalEvent(ai, emp);
            Assert.Multiple(() =>
            {
                Assert.That(emp.Handled, Is.True);
                Assert.That(batteries.GetCharge(enemyCell), Is.Zero);
                Assert.That(batteries.GetCharge(friendlyCell), Is.EqualTo(friendlyCharge));
                Assert.That(entMan.HasComponent<EmpDisabledComponent>(core), Is.False);
                Assert.That(entMan.HasComponent<EmpDisabledComponent>(ai), Is.False);
                Assert.That(entMan.HasComponent<EmpDisabledComponent>(ally), Is.False);
            });
            // Friendly-fire protection belongs to this ability, not a permanent EMP immunity.
            entMan.System<SharedEmpSystem>().TryEmpEffects(friendlyCell, 50000, TimeSpan.FromSeconds(1));
            Assert.That(batteries.GetCharge(friendlyCell), Is.Zero);
        });
        await pair.CleanReturnAsync();
    }
}
