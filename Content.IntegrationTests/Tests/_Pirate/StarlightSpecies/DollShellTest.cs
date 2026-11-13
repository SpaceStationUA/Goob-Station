using System.Linq;
using Content.Shared._Pirate.StarlightSpecies.Doll;
using Content.Shared.Actions.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Pirate.StarlightSpecies;

[TestFixture]
public sealed class DollShellTest
{
    [Test]
    public async Task EmptyShellCanRegrowAndShutDownAfterRepeatedActionRemoval()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true, Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var body = em.System<SharedBodySystem>();
            var shells = em.System<DollShellSystem>();
            var doll = em.Spawn("MobDoll", map.MapCoords);
            var shell = em.GetComponent<ShellComponent>(doll);

            for (var cycle = 0; cycle < 2; cycle++)
            {
                Assert.That(shell.GenerateShellPieceActionEntity, Is.Not.Null);
                var action = em.GetComponent<ActionComponent>(shell.GenerateShellPieceActionEntity!.Value);
                Assert.That(action.AttachedEntity, Is.EqualTo(doll));

                var pieces = body.GetBodyOrgans(doll)
                    .Where(o => em.HasComponent<OrganShellComponent>(o.Id)).ToArray();
                var snap = new SnapShellPieceEvent { Performer = doll, DeShell = true };
                em.EventBus.RaiseEvent(EventSource.Local, snap);
                Assert.That(snap.Handled, Is.True);
                foreach (var piece in pieces)
                    em.DeleteEntity(piece.Id);

                // The final organ removal and the action handler both refresh the empty shell.
                Assert.That(action.AttachedEntity, Is.Null);
                if (cycle == 0)
                    shells.Regrow(doll, "ShellFragmentDoll");
            }

            // Shutdown removes the action once more after the final shell fragment is gone.
            em.DeleteEntity(doll);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemovedShellRegrowsWithItsMarkingsAndColors()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true, Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var body = em.System<SharedBodySystem>();
            var shell = em.System<DollShellSystem>();
            var containers = em.System<SharedContainerSystem>();
            var humanoid = em.System<SharedHumanoidAppearanceSystem>();
            var doll = em.Spawn("MobDoll", map.MapCoords);
            Assert.That(body.GetBodyOrgans(doll).Count(o => em.HasComponent<OrganShellComponent>(o.Id)), Is.EqualTo(10));

            humanoid.AddMarking(doll, "DollShellHeadFox", new[] { Color.Magenta }, forced: true);
            var appearance = em.GetComponent<HumanoidAppearanceComponent>(doll);
            var before = appearance.MarkingSet.Markings[MarkingCategories.Head]
                .Select(m => (m.MarkingId, Colors: m.MarkingColors.ToArray())).ToArray();
            var head = body.GetBodyChildren(doll).Single(p => p.Component.PartType == BodyPartType.Head);
            var slot = containers.GetContainer(head.Id, SharedBodySystem.GetOrganContainerId("shell"));
            var fragment = slot.ContainedEntities.Single();

            Assert.That(body.RemoveOrgan(fragment), Is.True);
            Assert.That(slot.ContainedEntities, Is.Empty);
            Assert.That(appearance.MarkingSet.Markings[MarkingCategories.Head], Is.Empty);
            Assert.That(body.GetBodyOrgans(doll).Count(o => em.HasComponent<OrganShellComponent>(o.Id)), Is.EqualTo(9));
            em.DeleteEntity(fragment);

            shell.Regrow(doll, "ShellFragmentDoll");
            Assert.That(slot.ContainedEntities.Count, Is.EqualTo(1));
            Assert.That(body.GetBodyOrgans(doll).Count(o => em.HasComponent<OrganShellComponent>(o.Id)), Is.EqualTo(10));
            var after = appearance.MarkingSet.Markings[MarkingCategories.Head];
            Assert.That(after.Select(m => m.MarkingId), Is.EquivalentTo(before.Select(m => m.MarkingId)));
            foreach (var marking in before)
                Assert.That(after.Single(m => m.MarkingId == marking.MarkingId).MarkingColors, Is.EqualTo(marking.Colors));
            em.DeleteEntity(doll);
        });

        await pair.CleanReturnAsync();
    }
}
