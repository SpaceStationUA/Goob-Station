using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Pirate.StarlightSpecies;

[TestFixture]
public sealed class NeoMothDigestionTest
{
    [TestCase("MobNeoMoth", true)]
    [TestCase("MobMoth", true)]
    [TestCase("MobHuman", false)]
    public async Task DigestedFiberFeedsMoths(string prototype, bool nutritious)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var em = server.EntMan;
        var hungerSystem = em.System<HungerSystem>();
        EntityUid mob = default;
        HungerComponent hunger = default!;
        var before = 0f;
        await server.WaitAssertion(() =>
        {
            mob = em.Spawn(prototype, map.MapCoords);
            var stomach = em.System<SharedBodySystem>().GetBodyOrgans(mob)
                .Single(organ => em.HasComponent<StomachComponent>(organ.Id)).Id;
            var digestion = em.GetComponent<StomachComponent>(stomach);
            hunger = em.GetComponent<HungerComponent>(mob);
            hungerSystem.SetHunger(mob, 100, hunger);
            before = hungerSystem.GetHunger(hunger);

            // Cloth supplies Fiber; pass it through the real stomach and metabolism systems.
            var stomachSystem = em.System<StomachSystem>();
            Assert.That(stomachSystem.TryTransferSolution(stomach, new Solution("Fiber", FixedPoint2.New(1))), Is.True);
            digestion.DigestionDelay = TimeSpan.Zero;
        });
        await pair.RunTicksSync(180);

        await server.WaitAssertion(() =>
        {
            var after = hungerSystem.GetHunger(hunger);
            Assert.That(after > before, Is.EqualTo(nutritious),
                "The processing stomach must enable Fiber's hunger effect without relying on the heart's type.");
            em.DeleteEntity(mob);
        });
        await pair.CleanReturnAsync();
    }
}
