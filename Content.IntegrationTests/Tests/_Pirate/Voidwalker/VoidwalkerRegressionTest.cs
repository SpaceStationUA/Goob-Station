// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Pirate.Server.Voidwalker.Kidnapping;
using Content.Pirate.Server.Voidwalker.Kidnapping.Voided;
using Content.Pirate.Shared.Voidwalker;
using Content.Pirate.Shared.Voidwalker.Actions;
using Content.Shared.CCVar;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Gravity;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Stealth.Components;
using Content.Shared.Throwing;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Pirate.Voidwalker;

[TestFixture]
public sealed partial class VoidwalkerRegressionTest
{
    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  id: VoidwalkerRegressionDummy
  components:
  - type: DoAfter
""";

    [Serializable, NetSerializable]
    private sealed partial class GazeDoAfterEvent : DoAfterEvent
    {
        public override DoAfterEvent Clone() => this;
    }

    [TestCase(false, 1f, false)]
    [TestCase(false, 25f, true)]
    [TestCase(true, 1f, true)]
    public async Task GazeStillChecksRangeAndMap(bool changeMap, float distance, bool cancelled)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var maps = entMan.System<SharedMapSystem>();
        var transform = entMan.System<SharedTransformSystem>();
        var doAfter = entMan.System<SharedDoAfterSystem>();
        var ev = new GazeDoAfterEvent();
        EntityUid firstMap = default;
        EntityUid secondMap = default;
        EntityUid target = default;
        MapId firstMapId = default;
        MapId secondMapId = default;

        await server.WaitPost(() =>
        {
            firstMap = maps.CreateMap(out firstMapId);
            secondMap = maps.CreateMap(out secondMapId);
            var user = entMan.SpawnEntity("VoidwalkerRegressionDummy", new MapCoordinates(Vector2.Zero, firstMapId));
            target = entMan.SpawnEntity("VoidwalkerRegressionDummy", new MapCoordinates(Vector2.UnitX, firstMapId));
            var args = new DoAfterArgs(entMan, user, TimeSpan.FromSeconds(2), ev, null, target)
            {
                Broadcast = true,
                NeedHand = false,
                RequireCanInteract = false,
                DistanceThreshold = 20,
                IgnoreObstruction = true,
            };
            Assert.That(doAfter.TryStartDoAfter(args), Is.True);
        });

        await server.WaitRunTicks(1);
        Assert.That(ev.Cancelled, Is.False);

        await server.WaitPost(() => transform.SetMapCoordinates(target,
            new MapCoordinates(new Vector2(distance, 0), changeMap ? secondMapId : firstMapId)));
        await server.WaitRunTicks(1);
        Assert.That(ev.Cancelled, Is.EqualTo(cancelled));

        await server.WaitPost(() =>
        {
            entMan.DeleteEntity(firstMap);
            entMan.DeleteEntity(secondMap);
        });
        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A hit in space removes stealth for a time and stops space healing for a time.
    /// Piercing also checks the modifier set: Glass made 10 piercing into 5, FlimsyGlass keeps 10.
    /// </summary>
    [Test]
    public async Task HitInSpaceRevealsAndDelaysHealing()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var maps = entMan.System<SharedMapSystem>();
        var damageSys = entMan.System<DamageableSystem>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        EntityUid map = default;
        EntityUid walker = default;
        DamageableComponent damageable = null;
        VoidwalkerComponent voidwalker = null;

        await server.WaitPost(() =>
        {
            map = maps.CreateMap(out var mapId);
            walker = entMan.SpawnEntity("BaseMobVoidWalker", new MapCoordinates(Vector2.Zero, mapId));
            damageable = entMan.GetComponent<DamageableComponent>(walker);
            voidwalker = entMan.GetComponent<VoidwalkerComponent>(walker);
        });

        await server.WaitRunTicks(5);
        FixedPoint2 afterHit = default;
        await server.WaitPost(() =>
        {
            Assert.That(voidwalker.IsInSpace, Is.True, "no grid under the voidwalker means space");
            Assert.That(entMan.HasComponent<StealthComponent>(walker), Is.True, "stealthed in space");

            var piercing = new DamageSpecifier(protoMan.Index<DamageTypePrototype>("Piercing"), 10);
            Assert.That(damageSys.TryChangeDamage((walker, damageable), piercing), Is.True);
            afterHit = damageable.TotalDamage;

            Assert.That(afterHit.Float(), Is.EqualTo(10f).Within(0.1f), "no flat reduction on piercing");
            Assert.That(entMan.HasComponent<StealthComponent>(walker), Is.False, "a hit removes stealth");
        });

        // 4 s is two heal ticks and two spaced checks: no heal and no stealth yet.
        await server.WaitRunTicks((int) (timing.TickRate * 4));
        await server.WaitPost(() =>
        {
            Assert.That(damageable.TotalDamage, Is.EqualTo(afterHit), "no space healing just after a hit");
            Assert.That(entMan.HasComponent<StealthComponent>(walker), Is.False, "still revealed just after a hit");
        });

        // 12 s heal delay + one 2 s heal tick, with margin.
        await server.WaitRunTicks((int) (timing.TickRate * 11));
        await server.WaitPost(() =>
        {
            Assert.That(damageable.TotalDamage, Is.LessThan(afterHit), "space healing comes back after the delay");
            Assert.That(entMan.HasComponent<StealthComponent>(walker), Is.True, "stealth comes back after the reveal");
            entMan.DeleteEntity(map);
        });
        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// In space the voidwalker is weightless and moves like a jetpack wearer.
    /// A Void Walk dash ends at its weightless sprint speed, so low friction cannot carry it far.
    /// </summary>
    [Test]
    public async Task SpaceMovementHasMomentumAndDashIsCapped()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var maps = entMan.System<SharedMapSystem>();
        var gravity = entMan.System<SharedGravitySystem>();
        var throwing = entMan.System<ThrowingSystem>();
        var timing = server.ResolveDependency<IGameTiming>();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var actionEvent = new VoidwalkerVoidWalkEvent();
        EntityUid map = default;
        EntityUid walker = default;
        MovementSpeedModifierComponent move = null;

        await server.WaitPost(() =>
        {
            map = maps.CreateMap(out var mapId);
            walker = entMan.SpawnEntity("BaseMobVoidWalker", new MapCoordinates(Vector2.Zero, mapId));
            move = entMan.GetComponent<MovementSpeedModifierComponent>(walker);
        });

        await server.WaitRunTicks(5);
        await server.WaitPost(() =>
        {
            var canMove = new CanWeightlessMoveEvent(walker);
            entMan.EventBus.RaiseLocalEvent(walker, ref canMove);

            Assert.Multiple(() =>
            {
                Assert.That(gravity.IsWeightless(walker), Is.True, "weightless in space");
                Assert.That(canMove.CanMove, Is.True, "can push itself in space like a jetpack wearer");
                // Between a jetpack (acceleration 1, friction 0.25) and walking: it keeps momentum, but less.
                Assert.That(move.WeightlessAcceleration, Is.GreaterThan(1f), "more acceleration than a jetpack");
                Assert.That(move.WeightlessAcceleration, Is.LessThan(move.Acceleration), "less acceleration than walking");
                // Final weightless friction is the base value times the air friction CVar, the same as for a jetpack.
                Assert.That(move.WeightlessFriction, Is.GreaterThan(0.25f * cfg.GetCVar(CCVars.AirFriction)),
                    "more friction than a jetpack");
                Assert.That(move.WeightlessFriction, Is.LessThan(move.Friction), "less friction than walking");
                Assert.That(move.WeightlessFrictionNoInput, Is.EqualTo(move.WeightlessFriction).Within(0.001f),
                    "jetpack friction without input");
                Assert.That(move.WeightlessSprintSpeed, Is.EqualTo(3.6f).Within(0.01f), "space top speed");
            });

            throwing.TryThrow(walker, new Vector2(actionEvent.Distance, 0), actionEvent.Speed, animated: false);
            Assert.That(entMan.HasComponent<ThrownItemComponent>(walker), Is.True, "the dash started");
            Assert.That(entMan.GetComponent<PhysicsComponent>(walker).LinearVelocity.Length(),
                Is.GreaterThan(move.WeightlessSprintSpeed), "the dash is faster than normal space speed");
        });

        await server.WaitRunTicks((int) (timing.TickRate * 2));
        await server.WaitPost(() =>
        {
            Assert.That(entMan.HasComponent<ThrownItemComponent>(walker), Is.False, "the dash ended");
            var speed = entMan.GetComponent<PhysicsComponent>(walker).LinearVelocity.Length();
            Assert.That(speed, Is.LessThanOrEqualTo(move.WeightlessSprintSpeed + 0.01f), "dash speed is capped");
            entMan.DeleteEntity(map);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task KidnappedBodyReturnsToNonStationOrigin()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var maps = entMan.System<SharedMapSystem>();
        EntityUid origin = default;
        EntityUid voidMap = default;
        EntityUid victim = default;
        EntityCoordinates originalCoordinates = default;

        await server.WaitPost(() =>
        {
            origin = maps.CreateMap();
            voidMap = maps.CreateMap(out var voidMapId);
            originalCoordinates = new EntityCoordinates(origin, new Vector2(2, 3));
            victim = entMan.SpawnEntity("VoidwalkerRegressionDummy", new MapCoordinates(Vector2.Zero, voidMapId));
            var kidnapped = entMan.AddComponent<VoidwalkerKidnappedComponent>(victim);
            kidnapped.OriginalMap = origin;
            kidnapped.OriginalCoordinates = originalCoordinates;
        });

        await server.WaitRunTicks(2);
        await server.WaitPost(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<TransformComponent>(victim).Coordinates, Is.EqualTo(originalCoordinates));
                Assert.That(entMan.HasComponent<VoidwalkerKidnappedComponent>(victim), Is.False);
            });
            entMan.DeleteEntity(origin);
            entMan.DeleteEntity(voidMap);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FailedSpaceReturnsDoNotBlockOtherVictimsOrVomiting()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var maps = entMan.System<SharedMapSystem>();
        var timing = server.ResolveDependency<IGameTiming>();
        EntityUid map = default;
        VoidedComponent first = null;
        VoidedComponent second = null;

        await server.WaitPost(() =>
        {
            map = maps.CreateMap(out var mapId);
            var firstVictim = entMan.SpawnEntity("VoidwalkerRegressionDummy", new MapCoordinates(Vector2.Zero, mapId));
            var secondVictim = entMan.SpawnEntity("VoidwalkerRegressionDummy", new MapCoordinates(Vector2.UnitX, mapId));
            first = entMan.AddComponent<VoidedComponent>(firstVictim);
            second = entMan.AddComponent<VoidedComponent>(secondVictim);
            first.NextVomitTime = TimeSpan.Zero;
            second.NextVomitTime = TimeSpan.Zero;
        });

        await server.WaitRunTicks(1);
        await server.WaitPost(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(first.NextSpacedCheck, Is.GreaterThan(timing.CurTime));
                Assert.That(second.NextSpacedCheck, Is.GreaterThan(timing.CurTime));
                Assert.That(first.NextVomitTime, Is.GreaterThan(timing.CurTime));
                Assert.That(second.NextVomitTime, Is.GreaterThan(timing.CurTime));
            });
            entMan.DeleteEntity(map);
        });
        await pair.CleanReturnAsync();
    }
}
