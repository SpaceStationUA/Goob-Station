// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Body.Chips;
using Content.Shared._Pirate.Knowledge;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests._Pirate.Knowledge;

[TestFixture]
public sealed class SpawnedMobKnowledgeIntegrationTest
{
    private const string BaselineSkill = "WallsKnowledge";

    [Test]
    public async Task TakingOverASpawnedMobAppliesTheSpeciesBaseline()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var knowledge = server.System<SharedKnowledgeSystem>();
        var minds = server.System<SharedMindSystem>();

        EntityUid human = default;

        await server.WaitAssertion(() =>
        {
            human = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);

            var store = knowledge.GetContainer(human);
            Assert.That(store, Is.Not.Null);
            Assert.That(store!.Value.Comp.ProfileApplied, Is.False,
                "A freshly spawned mob has not been given a profile yet.");
            Assert.That(knowledge.GetKnowledge(store.Value, BaselineSkill), Is.Null,
                "A freshly spawned mob should start with nothing.");
        });

        await server.WaitPost(() =>
        {
            var mind = minds.CreateMind(null);
            minds.TransferTo(mind, human, mind: mind.Comp);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var store = knowledge.GetContainer(human);
            Assert.That(store!.Value.Comp.ProfileApplied, Is.True,
                "Taking over a mob must give it its species knowledge profile.");
            Assert.That(knowledge.GetKnowledge(store.Value, BaselineSkill), Is.Not.Null,
                "The species baseline did not reach the mob a player took over.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TheBaselineDoesNotWipeChipsInstalledOnSpawn()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var knowledge = server.System<SharedKnowledgeSystem>();
        var chips = server.System<OrganChipSystem>();
        var minds = server.System<SharedMindSystem>();

        EntityUid human = default;

        await server.WaitAssertion(() =>
        {
            human = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            Assert.That(chips.InstallChip(human, "SkillChipNukie"), Is.True);

            var store = knowledge.GetContainer(human)!.Value;
            Assert.That(knowledge.GetKnowledge(store, "KnowledgeWeaponsRifle")!.Value.Comp.TemporaryLevel,
                Is.EqualTo(50), "Chip did not apply before the mind was added.");
        });

        await server.WaitPost(() =>
        {
            var mind = minds.CreateMind(null);
            minds.TransferTo(mind, human, mind: mind.Comp);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var store = knowledge.GetContainer(human)!.Value;

            Assert.That(store.Comp.ProfileApplied, Is.True);
            Assert.That(knowledge.GetKnowledge(store, BaselineSkill), Is.Not.Null,
                "The baseline should still have been applied.");

            var rifle = knowledge.GetKnowledge(store, "KnowledgeWeaponsRifle");
            Assert.That(rifle, Is.Not.Null, "Applying the baseline destroyed the chip's skills.");
            Assert.That(rifle!.Value.Comp.TemporaryLevel, Is.EqualTo(50),
                "The chip's bonus was not put back after the profile rebuilt the store.");
        });

        await pair.CleanReturnAsync();
    }
}
