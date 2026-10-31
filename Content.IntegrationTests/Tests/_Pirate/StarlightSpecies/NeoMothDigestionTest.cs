using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

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
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var mob = em.Spawn(prototype, MapCoordinates.Nullspace);
            var stomach = em.System<SharedBodySystem>().GetBodyOrgans(mob)
                .Single(organ => em.HasComponent<StomachComponent>(organ.Id)).Id;
            var digestion = em.GetComponent<StomachComponent>(stomach);
            var metabolism = em.GetComponent<MetabolizerComponent>(stomach);
            var hunger = em.GetComponent<HungerComponent>(mob);
            var hungerSystem = em.System<HungerSystem>();
            hungerSystem.SetHunger(mob, 100, hunger);
            var before = hungerSystem.GetHunger(hunger);

            // Cloth supplies Fiber; pass it through the real stomach and metabolism systems.
            var stomachSystem = em.System<StomachSystem>();
            Assert.That(stomachSystem.TryTransferSolution(stomach, new Solution("Fiber", FixedPoint2.New(1))), Is.True);
            var now = server.ResolveDependency<IGameTiming>().CurTime;
            digestion.DigestionDelay = TimeSpan.Zero;
            digestion.NextUpdate = now;
            stomachSystem.Update(0);
            metabolism.NextUpdate = now;
            em.System<MetabolizerSystem>().Update(0);

            var after = hungerSystem.GetHunger(hunger);
            Assert.That(after > before, Is.EqualTo(nutritious),
                "The processing stomach must enable Fiber's hunger effect without relying on the heart's type.");
            em.DeleteEntity(mob);
        });
        await pair.CleanReturnAsync();
    }
}
