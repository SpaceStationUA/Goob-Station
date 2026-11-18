// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._Pirate.Cleanup;
using Content.Server._Pirate.Fluids;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Spreader;
using Content.Shared._Funkystation.WallStains;
using Content.Shared._Funkystation.WallStains.Components;
using Content.Shared._Pirate.Cleanup;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Forensics.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Pirate.Fluids;

public sealed class RoundLitterTest : InteractionTest
{
    [Test]
    public async Task ChocolateWrapperOnFloor()
    {
        var placed = ToServer(await Spawn("FoodPacketChocolateTrash", PlayerCoords));
        await PlaceInHands("FoodSnackChocolate");
        await UseInHand();
        await RunTicks(2);

        EntityUid wrapper = default;
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<RoundLitterComponent>(placed), Is.False);
            var query = SEntMan.AllEntityQueryEnumerator<DisposableLitterComponent, RoundLitterComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out _, out var litter, out var xform))
            {
                if (xform.MapID != MapId)
                    continue;

                Assert.That(litter.Lifetime, Is.EqualTo(TimeSpan.FromMinutes(20)));
                Assert.That(wrapper, Is.EqualTo(EntityUid.Invalid));
                wrapper = uid;
            }
            Assert.That(wrapper.IsValid());
        });

        await Pickup(FromServer(wrapper));
        await Server.WaitPost(() => SEntMan.GetComponent<RoundLitterComponent>(wrapper).ExpiresAt = TimeSpan.Zero);
        await RunSeconds(2);
        Assert.That(SEntMan.EntityExists(wrapper));
        await Server.WaitPost(() => SEntMan.GetComponent<RoundLitterComponent>(wrapper).ExpiresAt = TimeSpan.Zero);
        await Drop();
        Assert.That(SEntMan.GetComponent<RoundLitterComponent>(wrapper).ExpiresAt, Is.GreaterThan(TimeSpan.Zero));
        await Server.WaitPost(() => SEntMan.GetComponent<RoundLitterComponent>(wrapper).ExpiresAt = TimeSpan.Zero);
        await RunSeconds(2);
        Assert.That(SEntMan.EntityExists(wrapper), Is.False);
        Assert.That(SEntMan.EntityExists(placed));
    }

    [Test]
    public async Task OnlyFiredCartridgeIsTimed()
    {
        var gun = ToServer(await Spawn("WeaponPistolMk58", PlayerCoords));
        var casing = ToServer(await Spawn("CartridgePistol", PlayerCoords));
        var placed = ToServer(await Spawn("CartridgePistolSpent", PlayerCoords));
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<RoundLitterComponent>(casing), Is.False);
            var from = ToServer(PlayerCoords);
            SGun.Shoot((gun, SEntMan.GetComponent<GunComponent>(gun)), casing,
                from, from.Offset(Vector2.UnitX * 4), out _);
            Assert.That(SEntMan.GetComponent<CartridgeAmmoComponent>(casing).Spent);
            var litter = SEntMan.GetComponent<RoundLitterComponent>(casing);
            Assert.That(litter.Lifetime, Is.EqualTo(TimeSpan.FromMinutes(10)));
            Assert.That(litter.FloorOnly);
            Assert.That(SEntMan.HasComponent<RoundLitterComponent>(placed), Is.False);
        });
        await RunSeconds(2); // Let the ejected casing land before expiring it.
        await Server.WaitPost(() => SEntMan.GetComponent<RoundLitterComponent>(casing).ExpiresAt = TimeSpan.Zero);
        await RunSeconds(2);
        Assert.That(SEntMan.EntityExists(casing), Is.False);
        Assert.That(SEntMan.EntityExists(placed));
    }

    [Test]
    public async Task TraceWaterEvaporatesAndOnlyRealSpillsRenewLifetime()
    {
        var puddles = Server.System<PuddleSystem>();
        var chemistry = Server.System<SharedSolutionContainerSystem>();
        EntityUid puddle = default;
        await Server.WaitAssertion(() =>
        {
            var mix = new Solution("Blood", 30);
            mix.AddReagent("Water", 1);
            Assert.That(puddles.TrySpillAt(MapData.Tile, mix, out puddle, sound: false, tileReact: false));
            Assert.That(SEntMan.HasComponent<EvaporationComponent>(puddle));
            Assert.That(SEntMan.HasComponent<ActiveEdgeSpreaderComponent>(puddle), Is.False);
        });
        await RunSeconds(4);
        await Server.WaitAssertion(() =>
        {
            Assert.That(chemistry.TryGetSolution(puddle, "puddle", out var sol, out var solution));
            Assert.That(solution.GetTotalPrototypeQuantity("Water"), Is.LessThan(FixedPoint2.New(1)));
            Assert.That(solution.GetTotalPrototypeQuantity("Blood"), Is.EqualTo(FixedPoint2.New(30)));
            chemistry.RemoveReagent(sol!.Value, "Water", 1);
            Assert.That(SEntMan.HasComponent<EvaporationComponent>(puddle), Is.False);

            var litter = SEntMan.GetComponent<RoundLitterComponent>(puddle);
            litter.ExpiresAt = TimeSpan.Zero;
            Assert.That(puddles.TryAddSolution(puddle, new Solution(), sound: false), Is.False);
            Assert.That(litter.ExpiresAt, Is.EqualTo(TimeSpan.Zero));
            Assert.That(puddles.TrySpillAt(MapData.Tile, new Solution("Blood", 21), out var existing, sound: false, tileReact: false));
            Assert.That(existing, Is.EqualTo(puddle));
            Assert.That(litter.ExpiresAt, Is.GreaterThan(TimeSpan.Zero));
            Assert.That(SEntMan.HasComponent<ActiveEdgeSpreaderComponent>(puddle));
        });
    }

    [Test]
    public async Task MapSpillStaysPersistentOnTopUp()
    {
        var puddles = Server.System<PuddleSystem>();
        await Server.WaitAssertion(() =>
        {
            puddles.SpillPersistentAt(ToServer(PlayerCoords), new Solution("Blood", 10), sound: false);
            Assert.That(puddles.TryGetPuddle(MapData.Tile, out var placed));
            Assert.That(puddles.TrySpillAt(MapData.Tile, new Solution("Blood", 1), out var existing, sound: false, tileReact: false));
            Assert.That(existing, Is.EqualTo(placed));
            Assert.That(SEntMan.HasComponent<RoundLitterComponent>(placed), Is.False);
        });
    }

    [Test]
    public async Task ExpiringWallStainPreservesForensics()
    {
        var wall = ToServer(await Spawn("WallSolid", PlayerCoords));
        EntityUid stain = default;
        var dna = ("test DNA", TimeSpan.Zero);
        await Server.WaitAssertion(() =>
        {
            var splash = new SplashOnWallEvent(ToServer(PlayerCoords), new Solution("Blood", 20));
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref splash);
            var children = Xform(wall).ChildEnumerator;
            while (children.MoveNext(out var child))
            {
                if (SEntMan.HasComponent<WallStainComponent>(child))
                    stain = child;
            }
            Assert.That(stain.IsValid());
            Assert.That(SEntMan.HasComponent<EvaporatingWallStainComponent>(stain), Is.False);
            SEntMan.GetComponent<ForensicsComponent>(wall).DNAs.Add(dna);
            var litter = SEntMan.GetComponent<RoundLitterComponent>(stain);
            litter.ExpiresAt = TimeSpan.Zero;
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref splash);
            Assert.That(litter.ExpiresAt, Is.EqualTo(TimeSpan.Zero), "A full stain cannot accept another splash.");
        });
        await RunSeconds(2);
        Assert.That(SEntMan.EntityExists(stain), Is.False);
        Assert.That(SEntMan.HasComponent<StainedWallComponent>(wall), Is.False);
        Assert.That(SEntMan.GetComponent<ForensicsComponent>(wall).DNAs, Does.Contain(dna));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task FloorWaterAbsorbsInEitherSpawnOrder(bool waterFirst)
    {
        if (waterFirst)
            await Spawn("FloorWaterEntity", PlayerCoords);
        EntityUid puddle = default;
        await Server.WaitPost(() => puddle = SEntMan.SpawnAtPosition("PuddleBlood", ToServer(PlayerCoords)));
        if (!waterFirst)
        {
            await RunSeconds(2);
            Assert.That(SEntMan.EntityExists(puddle));
            await Spawn("FloorWaterEntity", PlayerCoords);
        }
        await RunSeconds(2);
        Assert.That(SEntMan.EntityExists(puddle), Is.False);
    }
}
