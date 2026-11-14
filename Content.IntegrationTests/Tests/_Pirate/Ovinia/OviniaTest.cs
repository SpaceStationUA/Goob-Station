using System.Linq;
using Content.Client.Humanoid;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Pirate.Ovinia;

[TestFixture]
public sealed class OviniaTest
{
    [TestCase("WheatBushel", true)]
    [TestCase("FoodTomato", true)]
    [TestCase("FoodMeatCooked", false)]
    public async Task HerbivoreCanDigestPlantsButNotMeat(string foodPrototype, bool edible)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var mob = em.SpawnEntity("MobOvinia", MapCoordinates.Nullspace);
            var stomach = em.System<SharedBodySystem>().GetBodyOrgans(mob)
                .Single(organ => em.HasComponent<StomachComponent>(organ.Id)).Id;
            var food = em.SpawnEntity(foodPrototype, MapCoordinates.Nullspace);
            var digestible = em.System<IngestionSystem>().IsDigestibleBy(food,
                new Entity<StomachComponent, OrganComponent>(stomach,
                    em.GetComponent<StomachComponent>(stomach), em.GetComponent<OrganComponent>(stomach)));
            Assert.That(digestible, Is.EqualTo(edible));
            em.DeleteEntity(food);
            em.DeleteEntity(mob);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WoolResistancesApplyToNativeBodyParts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var mob = em.SpawnEntity("MobOvinia", MapCoordinates.Nullspace);
            var heat = server.ProtoMan.Index<DamageTypePrototype>("Heat");
            var slash = server.ProtoMan.Index<DamageTypePrototype>("Slash");
            var applied = em.System<DamageableSystem>().ChangeDamage(mob,
                new DamageSpecifier(heat, 10) + new DamageSpecifier(slash, 10),
                targetPart: TargetBodyPart.LeftArm, canMiss: false);
            Assert.That(applied.DamageDict[heat.ID], Is.EqualTo(FixedPoint2.New(8)));
            Assert.That(applied.DamageDict[slash.ID], Is.EqualTo(FixedPoint2.New(11.5)));
            em.DeleteEntity(mob);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeerMarkingsAndNeckWoolRenderOnLobbyDummy()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        await client.WaitAssertion(() =>
        {
            var em = client.EntMan;
            var uid = em.Spawn("MobOviniaDummy");
            var appearance = client.System<HumanoidAppearanceSystem>();
            var humanoid = em.GetComponent<HumanoidAppearanceComponent>(uid);
            var sprite = em.GetComponent<SpriteComponent>(uid);
            foreach (var marking in new[] { "OviniaEarsDeer", "OviniaDeerHorns", "OviniaPatternFawn", "OviniaNeckfluff" })
                appearance.AddMarking(uid, marking, Color.White, sync: false, forced: true, humanoid: humanoid);
            appearance.UpdateSprite((uid, humanoid, sprite));

            var sprites = client.System<SpriteSystem>();
            foreach (var key in new[] { "OviniaEarsDeer-deer-ears", "OviniaDeerHorns-ovinia-deer-horns", "OviniaPatternFawn-torso-fawn", "OviniaNeckfluff-ovinia-neck-fluff" })
            {
                Assert.That(sprites.LayerMapTryGet((uid, sprite), key, out var layer, false), Is.True, key);
                Assert.That(sprite[layer].Visible, Is.True, key);
            }

            em.DeleteEntity(uid);
        });
        await pair.CleanReturnAsync();
    }
}
