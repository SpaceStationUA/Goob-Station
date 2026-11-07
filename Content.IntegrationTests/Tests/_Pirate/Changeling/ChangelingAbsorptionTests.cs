// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Goobstation.Server.Changeling;
using Content.Goobstation.Shared.Changeling.Actions;
using Content.Goobstation.Shared.Changeling.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.DoAfter;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Store.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Pirate.Changeling;

[TestFixture]
public sealed class ChangelingAbsorptionTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task AbsorptionDealsGeneticDamageWithoutDestroyingOrgans(bool implantEgg)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var ling = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var target = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var mindSystem = entMan.System<SharedMindSystem>();
            var mind = mindSystem.CreateMind(null).Owner;
            mindSystem.TransferTo(mind, ling);
            entMan.EnsureComponent<ChangelingComponent>(ling);
            entMan.EnsureComponent<StoreComponent>(ling);
            entMan.EnsureComponent<AbsorbableComponent>(target);
            entMan.System<MobStateSystem>().ChangeMobState(target, MobState.Dead);

            var body = entMan.System<SharedBodySystem>();
            var parts = body.GetBodyChildren(target).Select(part => part.Id).ToArray();
            var organs = parts.SelectMany(part => body.GetPartOrgans(part)).Select(organ => organ.Id).ToArray();
            var integrity = parts.Where(part => entMan.HasComponent<WoundableComponent>(part))
                .ToDictionary(part => part, part => entMan.GetComponent<WoundableComponent>(part).WoundableIntegrity);
            Assert.That(organs, Is.Not.Empty);

            if (implantEgg)
            {
                var identity = entMan.GetComponent<ChangelingIdentityComponent>(ling);
                var egg = new StingLayEggsEvent { Target = target };
                entMan.System<ChangelingSystem>().OnLayEgg(ling, identity, ref egg);
                Assert.That(egg.Handled, Is.True);
            }
            else
            {
                var absorb = new DoAfterArgs(entMan, ling, TimeSpan.Zero,
                    new AbsorbDNADoAfterEvent(), ling, target: target);
                Assert.That(entMan.System<SharedDoAfterSystem>().TryStartDoAfter(absorb), Is.True);
            }

            // Splitting damage between vital parts can round away one hundredth.
            Assert.That(entMan.GetComponent<DamageableComponent>(target).DamagePerGroup["Genetic"],
                Is.InRange(FixedPoint2.New(99.99), FixedPoint2.New(100)));
            Assert.That(body.GetBodyChildren(target).SelectMany(part => body.GetPartOrgans(part.Id))
                .Select(organ => organ.Id), Is.EquivalentTo(organs));
            foreach (var (part, previousIntegrity) in integrity)
            {
                Assert.That(entMan.GetComponent<WoundableComponent>(part).WoundableIntegrity,
                    Is.EqualTo(previousIntegrity), "Absorption must not add organ-mangling wounds.");
            }

            entMan.DeleteEntity(target);
            if (entMan.EntityExists(ling))
                entMan.DeleteEntity(ling);
            entMan.DeleteEntity(mind);
        });

        await pair.CleanReturnAsync();
    }
}
