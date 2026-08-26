// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Tests.Helpers;
using Content.Server.Charges;
using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Server._Pirate.Screens;
using Content.Shared._EinsteinEngines.Revolutionary;
using Content.Shared._EinsteinEngines.Revolutionary.Components;
using Content.Shared._Pirate.Access.Systems;
using Content.Shared._Pirate.Buckle;
using Content.Shared._Pirate.Cuffs;
using Content.Shared._Pirate.Fluids;
using Content.Shared._Pirate.Parry;
using Content.Shared._Pirate.Silicons;
using Content.Shared._Pirate.Temperature;
using Content.Shared._Pirate.Weapons.Ranged;
using Content.Shared._DV.Carrying;
using Content.Shared.ActionBlocker;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Charges;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Emag.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Revolutionary.Components;
using Content.Shared.Standing;
using Content.Shared.Throwing;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.IntegrationTests.Tests._Pirate.Support;

public sealed class RevolutionaryConversionListenerSystem : TestListenerSystem<AfterRevolutionaryConvertedEvent>;

[TestFixture]
public sealed class PortedSupportIntegrationTest
{
    private static readonly EntProtoId PropagandaId = "RevPropaganda";
    private static readonly EntProtoId AdvancedPropagandaId = "RevPropagandaAdvanced";
    private static readonly EntProtoId HandcuffsId = "Handcuffs";
    private const string RelayStrappedEffectId = "PirateSupportRelayStrapped";
    private const string LockStrapEffectId = "PirateSupportLockStrap";
    private const string UnlockStrapEffectId = "PirateSupportUnlockStrap";
    private const string UnbuckleStrappedEffectId = "PirateSupportUnbuckleStrapped";

    [TestPrototypes]
    private const string TestPrototypes = @"
- type: entity
  id: PirateSupportCuffBot
  components:
  - type: CuffSpawner
  - type: DoAfter

- type: entity
  id: PirateSupportCuffTarget
  components:
  - type: Cuffable
  - type: Hands
    hands:
      hand_left_outer:
        location: Left
      hand_left_inner:
        location: Left
      hand_right_inner:
        location: Right
      hand_right_outer:
        location: Right
    sortedHands:
    - hand_left_outer
    - hand_left_inner
    - hand_right_inner
    - hand_right_outer
  - type: ComplexInteraction

- type: entity
  id: PirateSupportCuffTargetTwoHands
  components:
  - type: Cuffable
  - type: Hands
    hands:
      hand_left:
        location: Left
      hand_right:
        location: Right
    sortedHands:
    - hand_left
    - hand_right
  - type: ComplexInteraction

- type: entityEffect
  id: PirateSupportRelayStrapped
  effects:
  - !type:RelayStrapped
    effects:
    - !type:HealthChange
      damage:
        types:
          Blunt: 7
      targetPart: Chest

- type: entityEffect
  id: PirateSupportLockStrap
  effects:
  - !type:StrapLock

- type: entityEffect
  id: PirateSupportUnlockStrap
  effects:
  - !type:StrapLock
    unlock: true

- type: entityEffect
  id: PirateSupportUnbuckleStrapped
  effects:
  - !type:UnbuckleStrapped
";

    [Test]
    public async Task PropagandaConsumesOnceConvertsAndUpdatesClientVisuals()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        EntityUid user = default;
        EntityUid target = default;
        EntityUid pamphlet = default;

        await server.WaitAssertion(() =>
        {
            user = entMan.SpawnEntity("MobHuman", map.GridCoords);
            target = entMan.SpawnEntity("MobHuman", map.GridCoords);
            pamphlet = entMan.SpawnEntity(PropagandaId, map.GridCoords);
            entMan.EnsureComponent<HeadRevolutionaryComponent>(user);
            entMan.EnsureComponent<TestListenerComponent>(user);
            entMan.EnsureComponent<TestListenerComponent>(pamphlet);

            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(user, pamphlet), Is.True);

            var charges = server.System<ChargesSystem>();
            var charge = entMan.GetComponent<LimitedChargesComponent>(pamphlet);
            Assert.Multiple(() =>
            {
                Assert.That(charges.GetCurrentCharges((pamphlet, charge, null)), Is.EqualTo(1));
                Assert.That(server.System<SharedAppearanceSystem>().TryGetData<bool>(
                    pamphlet,
                    LimitedChargesState.HasCharges,
                    out var hasCharges), Is.True);
                Assert.That(hasCharges, Is.True);
            });
        });

        await pair.RunTicksSync(5);
        await AssertClientState("icon");

        await server.WaitAssertion(() =>
        {
            var interaction = new AfterInteractEvent(user, pamphlet, target, map.GridCoords, true);
            entMan.EventBus.RaiseLocalEvent(pamphlet, interaction);
            var charges = server.System<ChargesSystem>();
            var charge = entMan.GetComponent<LimitedChargesComponent>(pamphlet);

            Assert.Multiple(() =>
            {
                Assert.That(interaction.Handled, Is.True);
                Assert.That(charges.GetCurrentCharges((pamphlet, charge, null)), Is.Zero);
                Assert.That(server.System<SharedAppearanceSystem>().TryGetData<bool>(
                    pamphlet,
                    LimitedChargesState.HasCharges,
                    out var hasCharges), Is.True);
                Assert.That(hasCharges, Is.False);
            });
        });

        await pair.RunTicksSync(130);
        await server.WaitAssertion(() =>
        {
            var listener = server.System<RevolutionaryConversionListenerSystem>();
            var userEvents = listener.GetEvents(user).ToArray();
            var pamphletEvents = listener.GetEvents(pamphlet).ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(userEvents, Has.Length.EqualTo(1));
                Assert.That(pamphletEvents, Has.Length.EqualTo(1));
                Assert.That(userEvents[0].Target, Is.EqualTo(target));
                Assert.That(userEvents[0].User, Is.EqualTo(user));
                Assert.That(userEvents[0].Used, Is.EqualTo(pamphlet));
            });

            var second = new AfterInteractEvent(user, pamphlet, target, map.GridCoords, true);
            entMan.EventBus.RaiseLocalEvent(pamphlet, second);
            var charges = server.System<ChargesSystem>();
            var charge = entMan.GetComponent<LimitedChargesComponent>(pamphlet);
            Assert.That(charges.GetCurrentCharges((pamphlet, charge, null)), Is.Zero);
            Assert.That(listener.Count(user), Is.EqualTo(1));
        });

        await pair.RunTicksSync(130);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<RevolutionaryConversionListenerSystem>().Count(user), Is.EqualTo(1),
                "A spent pamphlet started a second conversion DoAfter.");
        });
        await AssertClientState("burnt");

        await server.WaitAssertion(() =>
        {
            var charges = server.System<ChargesSystem>();
            var charge = entMan.GetComponent<LimitedChargesComponent>(pamphlet);
            var appearance = server.System<SharedAppearanceSystem>();

            charges.ResetCharges((pamphlet, charge));
            AssertChargeState(1, true);
            charges.SetCharges((pamphlet, charge), 0);
            AssertChargeState(0, false);
            charges.AddCharges((pamphlet, charge, null), 50);
            AssertChargeState(1, true);
            charges.SetMaxCharges((pamphlet, charge), 0);
            AssertChargeState(0, false);
            charges.SetMaxCharges((pamphlet, charge), 1);
            AssertChargeState(1, true);

            void AssertChargeState(int expectedCharges, bool expectedVisual)
            {
                Assert.That(charges.GetCurrentCharges((pamphlet, charge, null)), Is.EqualTo(expectedCharges));
                Assert.That(appearance.TryGetData<bool>(pamphlet,
                    LimitedChargesState.HasCharges,
                    out var hasCharges), Is.True);
                Assert.That(hasCharges, Is.EqualTo(expectedVisual));
            }
        });

        await pair.RunTicksSync(5);
        await AssertClientState("icon");

        await server.WaitAssertion(() =>
        {
            entMan.DeleteEntity(pamphlet);
            entMan.DeleteEntity(target);
            entMan.DeleteEntity(user);
        });
        await pair.CleanReturnAsync();

        async Task AssertClientState(string expected)
        {
            await client.WaitAssertion(() =>
            {
                var clientUid = client.EntMan.GetEntity(entMan.GetNetEntity(pamphlet));
                var sprite = client.EntMan.GetComponent<SpriteComponent>(clientUid);
                var spriteSystem = client.System<SpriteSystem>();
                Assert.That(spriteSystem.LayerMapTryGet((clientUid, sprite), "base", out var layer, false), Is.True);
                Assert.That(sprite[layer], Is.TypeOf<Layer>());
                Assert.That(((Layer) sprite[layer]).State.Name, Is.EqualTo(expected));
            });
        }
    }

    [Test]
    public async Task CuffSpawnerDoAfterAppliesExactlyOnePairAndRevalidatesTarget()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        EntityUid bot = default;
        EntityUid target = default;
        EntityUid invalidated = default;
        EntityUid bare = default;

        await server.WaitAssertion(() =>
        {
            bot = entMan.SpawnEntity("PirateSupportCuffBot", map.GridCoords);
            target = entMan.SpawnEntity("PirateSupportCuffTargetTwoHands", map.GridCoords);
            invalidated = entMan.SpawnEntity("PirateSupportCuffTargetTwoHands", map.GridCoords);
            bare = entMan.SpawnEntity(null, map.GridCoords);
            var spawner = server.System<CuffSpawnerSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(spawner.CheckCuffs((bare, null), target), Is.False,
                    "Missing CuffSpawner must be a quiet rejection.");
                Assert.That(spawner.TryCuff((bare, null), target), Is.False,
                    "A stale bot entity must not spawn cuffs or log a Resolve error.");
                Assert.That(spawner.CheckCuffs((bot, null), bare), Is.False);
            });

            var emag = new GotEmaggedEvent(bot, EmagType.Interaction);
            entMan.EventBus.RaiseLocalEvent(bot, ref emag);
            Assert.That(emag.Handled, Is.True);

            var activate = new UserActivateInWorldEvent(bot, target, true);
            entMan.EventBus.RaiseLocalEvent(bot, activate);

            var invalidatedActivate = new UserActivateInWorldEvent(bot, invalidated, true);
            entMan.EventBus.RaiseLocalEvent(bot, invalidatedActivate);
            var hands = entMan.GetComponent<HandsComponent>(invalidated);
            server.System<SharedHandsSystem>().RemoveHands((invalidated, hands));
        });

        await pair.RunTicksSync(130);
        await server.WaitAssertion(() =>
        {
            var cuff = server.System<SharedCuffableSystem>();
            var spawner = server.System<CuffSpawnerSystem>();
            var targetCuffable = entMan.GetComponent<CuffableComponent>(target);
            var invalidatedCuffable = entMan.GetComponent<CuffableComponent>(invalidated);
            var cuffs = cuff.GetAllCuffs((target, targetCuffable));

            Assert.Multiple(() =>
            {
                Assert.That(cuff.IsCuffed((target, targetCuffable)), Is.True);
                Assert.That(cuffs, Has.Count.EqualTo(1));
                Assert.That(entMan.GetComponent<MetaDataComponent>(cuffs[0]).EntityPrototype?.ID,
                    Is.EqualTo(HandcuffsId.Id));
                Assert.That(spawner.CheckCuffs((bot, null), target), Is.False,
                    "A fully cuffed target must not accept a second generated pair.");
                Assert.That(cuff.GetAllCuffs((invalidated, invalidatedCuffable)), Is.Empty,
                    "Removing all hands during the DoAfter must cancel cuff generation.");
            });

            entMan.DeleteEntity(bare);
            entMan.DeleteEntity(invalidated);
            entMan.DeleteEntity(target);
            entMan.DeleteEntity(bot);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CuffCleanupContinuesPastQueuedCuffsAndIgnoresTerminatingOwners()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        EntityUid target = default;
        EntityUid firstCuff = default;
        EntityUid queuedCuff = default;
        EntityUid doomedTarget = default;
        EntityUid doomedCuff = default;

        await server.WaitAssertion(() =>
        {
            var cuff = server.System<SharedCuffableSystem>();
            var handsSystem = server.System<SharedHandsSystem>();
            target = entMan.SpawnEntity("PirateSupportCuffTarget", map.GridCoords);
            firstCuff = entMan.SpawnEntity(HandcuffsId, map.GridCoords);
            queuedCuff = entMan.SpawnEntity(HandcuffsId, map.GridCoords);

            Assert.That(cuff.TryAddNewCuffs(target, target, firstCuff), Is.True);
            Assert.That(cuff.TryAddNewCuffs(target, target, queuedCuff), Is.True);
            Assert.That(cuff.GetAllCuffs((target, null)), Has.Count.EqualTo(2));

            entMan.QueueDeleteEntity(queuedCuff);
            handsSystem.RemoveHands((target, entMan.GetComponent<HandsComponent>(target)));

            Assert.Multiple(() =>
            {
                Assert.That(cuff.GetAllCuffs((target, null)), Is.Empty,
                    "Cleanup must remove a queued cuff and continue to the remaining live cuff.");
                Assert.That(server.System<SharedContainerSystem>().IsEntityInContainer(firstCuff), Is.False);
                Assert.That(entMan.Deleted(firstCuff), Is.False);
            });

            doomedTarget = entMan.SpawnEntity("PirateSupportCuffTargetTwoHands", map.GridCoords);
            doomedCuff = entMan.SpawnEntity(HandcuffsId, map.GridCoords);
            Assert.That(cuff.TryAddNewCuffs(doomedTarget, doomedTarget, doomedCuff), Is.True);
            entMan.QueueDeleteEntity(doomedTarget);
            handsSystem.RemoveHands((doomedTarget, entMan.GetComponent<HandsComponent>(doomedTarget)));
            Assert.That(cuff.GetAllCuffs((doomedTarget, null)), Has.Count.EqualTo(1),
                "Hand-count cleanup must stop quietly once its owner is terminating.");
        });

        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.Deleted(queuedCuff), Is.True);
                Assert.That(entMan.Deleted(doomedTarget), Is.True);
                Assert.That(entMan.Deleted(doomedCuff), Is.True);
            });

            entMan.DeleteEntity(firstCuff);
            entMan.DeleteEntity(target);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StrapLockRequiresActorAndHandsBlocksActionsAndDropsOnHolderMovement()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        EntityUid cross = default;
        EntityUid holder = default;
        EntityUid carrier = default;
        EntityUid victim = default;
        EntityUid looseItem = default;
        float initialDamage = 0f;

        await server.WaitAssertion(() =>
        {
            cross = entMan.SpawnEntity("Crucifix", map.GridCoords);
            holder = entMan.SpawnEntity("MobHuman", map.GridCoords);
            carrier = entMan.SpawnEntity("MobHuman", map.GridCoords);
            victim = entMan.SpawnEntity("MobHuman", map.GridCoords);
            looseItem = entMan.SpawnEntity("Crowbar", map.GridCoords);
            var buckle = entMan.GetComponent<BuckleComponent>(victim);
            var buckleSystem = server.System<SharedBuckleSystem>();
            var hands = server.System<SharedHandsSystem>();
            var carrying = server.System<CarryingSystem>();
            var carriable = entMan.GetComponent<CarriableComponent>(victim);

            Assert.Multiple(() =>
            {
                Assert.That(carrying.CanCarry(carrier, (victim, carriable)), Is.True);
                Assert.That(buckleSystem.TryBuckle(victim, null, cross, buckle, popup: false), Is.False,
                    "A lock strap must reject actorless buckling.");
                Assert.That(buckleSystem.TryBuckle(victim, victim, cross, buckle, popup: false), Is.False,
                    "A victim must not hold themselves on the strap.");
            });

            Assert.That(hands.TryPickupAnyHand(holder, looseItem), Is.True);
            Assert.That(buckleSystem.TryBuckle(victim, holder, cross, buckle, popup: false), Is.False,
                "The holder must have all required hands free.");
            Assert.That(hands.TryDrop(holder, looseItem), Is.True);

            Assert.That(buckleSystem.TryBuckle(victim, holder, cross, buckle, popup: false), Is.True);
            var strapLock = entMan.GetComponent<StrapLockComponent>(cross);
            var damage = entMan.GetComponent<DamageableComponent>(victim).TotalDamage;
            initialDamage = damage.Float();

            var canDrag = new CanDragEvent();
            var frame = entMan.SpawnEntity("PowerArmorFrame", map.GridCoords);
            entMan.EventBus.RaiseLocalEvent(frame, ref canDrag);

            var actionBlocker = server.System<ActionBlockerSystem>();
            var pull = new PullAttemptEvent(victim, carrier);
            entMan.EventBus.RaiseLocalEvent(victim, pull);
            var down = new DownAttemptEvent();
            entMan.EventBus.RaiseLocalEvent(victim, down);
            var pushback = new ThrowPushbackAttemptEvent();
            entMan.EventBus.RaiseLocalEvent(victim, pushback);

            Assert.Multiple(() =>
            {
                Assert.That(canDrag.Handled, Is.True, "Buckleable frames must be valid drag sources.");
                Assert.That(buckle.BuckledTo, Is.EqualTo(cross));
                Assert.That(entMan.HasComponent<StrapLockedComponent>(victim), Is.True);
                Assert.That(entMan.HasComponent<StrapLockHeldComponent>(victim), Is.True);
                Assert.That(entMan.HasComponent<StrapLockHoldingComponent>(holder), Is.True);
                Assert.That(strapLock.VirtualItems, Has.Count.EqualTo(strapLock.RequiredHands));
                Assert.That(hands.CountFreeHands(holder), Is.Zero);
                Assert.That(actionBlocker.CanInteract(victim, carrier), Is.False);
                Assert.That(actionBlocker.CanAttack(victim, carrier), Is.False);
                Assert.That(actionBlocker.CanThrow(victim, looseItem), Is.False);
                Assert.That(pull.Cancelled, Is.True);
                Assert.That(down.Cancelled, Is.True);
                Assert.That(pushback.Cancelled, Is.True);
                Assert.That(carrying.CanCarry(carrier, (victim, carriable)), Is.False,
                    "A held or nailed victim must not be removable through carrying.");
            });

            entMan.DeleteEntity(frame);
            server.System<SharedTransformSystem>().SetCoordinates(
                holder,
                new EntityCoordinates(map.Grid, new Vector2(4f, 0f)));
        });

        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            var buckle = entMan.GetComponent<BuckleComponent>(victim);
            var damage = entMan.GetComponent<DamageableComponent>(victim).TotalDamage;
            Assert.Multiple(() =>
            {
                Assert.That(buckle.Buckled, Is.False);
                Assert.That(entMan.HasComponent<StrapLockedComponent>(victim), Is.False);
                Assert.That(entMan.HasComponent<StrapLockHeldComponent>(victim), Is.False);
                Assert.That(entMan.HasComponent<StrapLockHoldingComponent>(holder), Is.False);
                Assert.That(server.System<SharedHandsSystem>().CountFreeHands(holder), Is.EqualTo(2));
                Assert.That(damage.Float(), Is.EqualTo(initialDamage + 10f).Within(0.01f),
                    "Leaving holding range must apply CrucifixDropped exactly once.");
            });

            entMan.DeleteEntity(looseItem);
            entMan.DeleteEntity(victim);
            entMan.DeleteEntity(carrier);
            entMan.DeleteEntity(holder);
            entMan.DeleteEntity(cross);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StrapEntityEffectsRelayLockUnlockUnbuckleAndRestoreConstructionNode()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        EntityUid cross = default;
        EntityUid holder = default;
        EntityUid victim = default;
        float initialDamage = 0f;

        await server.WaitAssertion(() =>
        {
            cross = entMan.SpawnEntity("Crucifix", map.GridCoords);
            holder = entMan.SpawnEntity("MobHuman", map.GridCoords);
            victim = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var construction = server.System<ConstructionSystem>();
            Assert.That(construction.ChangeNode(cross, null, "left hand", performActions: false), Is.True);
            Assert.That(entMan.GetComponent<ConstructionComponent>(cross).Node, Is.EqualTo("left hand"));

            var buckle = entMan.GetComponent<BuckleComponent>(victim);
            Assert.That(server.System<SharedBuckleSystem>().TryBuckle(victim, holder, cross, buckle, popup: false), Is.True);
            var total = entMan.GetComponent<DamageableComponent>(victim).TotalDamage;
            initialDamage = total.Float();

            var effects = server.System<SharedEntityEffectsSystem>();
            Assert.That(effects.TryApplyEffect(cross, (ProtoId<EntityEffectPrototype>) RelayStrappedEffectId, user: holder), Is.True);
            var afterRelay = entMan.GetComponent<DamageableComponent>(victim).TotalDamage;
            Assert.That(afterRelay.Float(), Is.EqualTo(initialDamage + 7f).Within(0.01f));

            Assert.That(effects.TryApplyEffect(cross, (ProtoId<EntityEffectPrototype>) LockStrapEffectId, user: holder), Is.True);
            var strapLock = entMan.GetComponent<StrapLockComponent>(cross);
            Assert.Multiple(() =>
            {
                Assert.That(strapLock.Locked, Is.True);
                Assert.That(buckle.Buckled, Is.True);
                Assert.That(entMan.HasComponent<StrapLockedComponent>(victim), Is.True);
                Assert.That(server.System<SharedBuckleSystem>().TryUnbuckle(victim, holder, buckle, popup: false), Is.False);
            });
        });

        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<StrapLockHoldingComponent>(holder), Is.False);
                Assert.That(entMan.HasComponent<StrapLockHeldComponent>(victim), Is.False);
                Assert.That(server.System<SharedHandsSystem>().CountFreeHands(holder), Is.EqualTo(2));
            });

            var effects = server.System<SharedEntityEffectsSystem>();
            Assert.That(effects.TryApplyEffect(cross, (ProtoId<EntityEffectPrototype>) UnlockStrapEffectId, user: holder), Is.True);
            Assert.That(entMan.GetComponent<StrapLockComponent>(cross).Locked, Is.False);
            Assert.That(effects.TryApplyEffect(cross, (ProtoId<EntityEffectPrototype>) UnbuckleStrappedEffectId, user: holder), Is.True);

            var buckle = entMan.GetComponent<BuckleComponent>(victim);
            var construction = entMan.GetComponent<ConstructionComponent>(cross);
            Assert.Multiple(() =>
            {
                Assert.That(buckle.Buckled, Is.False);
                Assert.That(construction.Node, Is.EqualTo("cross"),
                    "Unstrapping must restore the crucifix construction graph to its safe cross node.");
                Assert.That(entMan.HasComponent<StrapLockedComponent>(victim), Is.False);
            });

            entMan.DeleteEntity(victim);
            entMan.DeleteEntity(holder);
            entMan.DeleteEntity(cross);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StrapLockMovementAndLifecycleCleanupNeverNeedsPolling()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        var buckleSystem = server.System<SharedBuckleSystem>();

        EntityUid SpawnCross(float x)
            => entMan.SpawnEntity("Crucifix", new EntityCoordinates(map.Grid, new Vector2(x, 0f)));

        EntityUid SpawnHuman(float x)
            => entMan.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, new Vector2(x, 0f)));

        await server.WaitAssertion(() =>
        {
            var cross = SpawnCross(0f);
            var holder = SpawnHuman(0f);
            var victim = SpawnHuman(0f);
            Assert.That(buckleSystem.TryBuckle(victim, holder, cross, popup: false), Is.True);
            server.System<SharedTransformSystem>().SetCoordinates(
                cross,
                new EntityCoordinates(map.Grid, new Vector2(4f, 0f)));
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            var victims = entMan.EntityQuery<StrapLockHeldComponent>().ToArray();
            var holders = entMan.EntityQuery<StrapLockHoldingComponent>().ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(victims, Is.Empty, "Moving the strap must run the same bounded range cleanup as moving the holder.");
                Assert.That(holders, Is.Empty);
            });
        });

        EntityUid virtualCross = default;
        EntityUid virtualHolder = default;
        EntityUid virtualVictim = default;
        await server.WaitAssertion(() =>
        {
            virtualCross = SpawnCross(10f);
            virtualHolder = SpawnHuman(10f);
            virtualVictim = SpawnHuman(10f);
            Assert.That(buckleSystem.TryBuckle(virtualVictim, virtualHolder, virtualCross, popup: false), Is.True);
            var strapLock = entMan.GetComponent<StrapLockComponent>(virtualCross);
            var virtualItem = EntityUid.Invalid;
            foreach (var item in strapLock.VirtualItems)
            {
                virtualItem = item;
                break;
            }

            Assert.That(virtualItem.IsValid(), Is.True);
            entMan.DeleteEntity(virtualItem);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<BuckleComponent>(virtualVictim).Buckled, Is.False);
                Assert.That(entMan.HasComponent<StrapLockHoldingComponent>(virtualHolder), Is.False);
                Assert.That(entMan.HasComponent<StrapLockedComponent>(virtualVictim), Is.False);
            });
            entMan.DeleteEntity(virtualVictim);
            entMan.DeleteEntity(virtualHolder);
            entMan.DeleteEntity(virtualCross);
        });

        EntityUid targetCross = default;
        EntityUid targetHolder = default;
        EntityUid targetVictim = default;
        await server.WaitAssertion(() =>
        {
            targetCross = SpawnCross(20f);
            targetHolder = SpawnHuman(20f);
            targetVictim = SpawnHuman(20f);
            Assert.That(buckleSystem.TryBuckle(targetVictim, targetHolder, targetCross, popup: false), Is.True);
            entMan.QueueDeleteEntity(targetVictim);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(targetVictim), Is.True);
            Assert.That(entMan.HasComponent<StrapLockHoldingComponent>(targetHolder), Is.False);
            entMan.DeleteEntity(targetHolder);
            entMan.DeleteEntity(targetCross);
        });

        EntityUid strapCross = default;
        EntityUid strapHolder = default;
        EntityUid strapVictim = default;
        await server.WaitAssertion(() =>
        {
            strapCross = SpawnCross(30f);
            strapHolder = SpawnHuman(30f);
            strapVictim = SpawnHuman(30f);
            Assert.That(buckleSystem.TryBuckle(strapVictim, strapHolder, strapCross, popup: false), Is.True);
            entMan.QueueDeleteEntity(strapCross);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.Deleted(strapCross), Is.True);
                Assert.That(entMan.HasComponent<StrapLockHoldingComponent>(strapHolder), Is.False);
                Assert.That(entMan.HasComponent<StrapLockHeldComponent>(strapVictim), Is.False);
                Assert.That(entMan.HasComponent<StrapLockedComponent>(strapVictim), Is.False);
            });
            entMan.DeleteEntity(strapVictim);
            entMan.DeleteEntity(strapHolder);
        });

        EntityUid holderCross = default;
        EntityUid deletedHolder = default;
        EntityUid holderVictim = default;
        await server.WaitAssertion(() =>
        {
            holderCross = SpawnCross(40f);
            deletedHolder = SpawnHuman(40f);
            holderVictim = SpawnHuman(40f);
            Assert.That(buckleSystem.TryBuckle(holderVictim, deletedHolder, holderCross, popup: false), Is.True);
            entMan.QueueDeleteEntity(deletedHolder);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.Deleted(deletedHolder), Is.True);
                Assert.That(entMan.GetComponent<BuckleComponent>(holderVictim).Buckled, Is.False);
                Assert.That(entMan.HasComponent<StrapLockHeldComponent>(holderVictim), Is.False);
                Assert.That(entMan.HasComponent<StrapLockedComponent>(holderVictim), Is.False);
            });
            entMan.DeleteEntity(holderVictim);
            entMan.DeleteEntity(holderCross);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EveryCuffSpawnerAndPropagandaPrototypeDependencyIsValid()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ProtoMan;
            var factory = server.ResolveDependency<IComponentFactory>();
            var entities = prototypes.EnumeratePrototypes<EntityPrototype>().ToArray();
            var failures = new List<string>();
            var spawnerCount = 0;

            foreach (var entity in entities)
            {
                if (!entity.TryGetComponent<CuffSpawnerComponent>(out var spawner, factory))
                    continue;

                spawnerCount++;
                if (!prototypes.TryIndex<EntityPrototype>(spawner!.HandcuffId, out var cuffPrototype) ||
                    !cuffPrototype.TryGetComponent<HandcuffComponent>(out _, factory))
                {
                    failures.Add($"{entity.ID}: {spawner.HandcuffId} is not a valid handcuff prototype");
                }
            }

            foreach (var id in new[] { PropagandaId, AdvancedPropagandaId })
            {
                if (!prototypes.TryIndex<EntityPrototype>(id, out var propaganda))
                {
                    failures.Add($"Missing propaganda prototype {id.Id}");
                    continue;
                }

                if (!propaganda.TryGetComponent<LimitedChargesComponent>(out var charges, factory))
                    failures.Add($"{id.Id}: missing LimitedCharges");
                if (!propaganda.TryGetComponent<RevolutionaryConverterComponent>(out var converter, factory))
                    failures.Add($"{id.Id}: missing RevolutionaryConverter");
                if (charges != null && converter != null && charges.MaxCharges < converter.ConsumesCharges)
                {
                    failures.Add($"{id.Id}: max charges {charges.MaxCharges} cannot pay conversion cost {converter.ConsumesCharges}");
                }
            }

            Assert.Multiple(() =>
            {
                Assert.That(spawnerCount, Is.GreaterThan(0), "No CuffSpawner prototype was loaded.");
                Assert.That(failures, Is.Empty, string.Join("\n", failures));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public void PortedSupportSystemsHaveNoPerFrameWorldScan()
    {
        var systems = new[]
        {
            typeof(SharedChargesSystem),
            typeof(CuffSpawnerSystem),
            typeof(SharedCuffableSystem),
            typeof(BuckleableSystem),
            typeof(StrapLockSystem),
            typeof(AccessScannerSystem),
            typeof(SignalScreenSystem),
            typeof(ParrySystem),
            typeof(SharedMultiMagazineGunSystem),
            typeof(BlackBodySystem),
            typeof(PuddleSpawnerSystem),
            typeof(WandskySystem),
        };

        foreach (var system in systems)
        {
            var updates = system
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method => method.DeclaringType == system && method.Name == "Update")
                .ToArray();
            Assert.That(updates, Is.Empty,
                $"{system.Name} must stay event-driven and must not scan all entities every frame.");
        }
    }
}
