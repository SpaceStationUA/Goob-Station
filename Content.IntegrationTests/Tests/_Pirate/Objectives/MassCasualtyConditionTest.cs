// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Server._Pirate.Objectives.Components;
using Content.Server.Objectives;
using Content.Shared._Pirate.Reputation;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Pirate.Objectives;

[TestFixture]
public sealed class MassCasualtyConditionTest
{
    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  parent: TraitorContractMassCasualtyObjective
  id: PirateMassCasualtyTestContract
  components:
  - type: MassCasualtyCondition
    target: 2
    minimumCrew: 2
  - type: ReputationCondition
    reputation: 0
  - type: ContractObjective
    payment: 1
""";

    [Test]
    public async Task AssignmentRequiresEnoughLivingEligibleCrewOnTheSameMap()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var maps = entMan.System<SharedMapSystem>();

        await server.WaitAssertion(() =>
        {
            var map = maps.CreateMap();
            var otherMap = maps.CreateMap();
            var coords = new EntityCoordinates(map, Vector2.Zero);
            var (_, owner) = SpawnCrew(entMan, coords, traitor: true);
            SpawnCrew(entMan, coords);
            SpawnCrew(entMan, coords, traitor: true);
            SpawnCrew(entMan, coords, hasJob: false);
            SpawnCrew(entMan, new EntityCoordinates(otherMap, Vector2.Zero));

            var (protectedBody, _) = SpawnCrew(entMan, coords);
            entMan.AddComponent<TargetObjectiveImmuneComponent>(protectedBody);
            var (_, protectedMind) = SpawnCrew(entMan, coords);
            entMan.AddComponent<TargetObjectiveImmuneComponent>(protectedMind.Owner);
            var (deadBody, _) = SpawnCrew(entMan, coords);
            entMan.System<MobStateSystem>().ChangeMobState(deadBody, MobState.Dead);

            var objective = SpawnObjective(entMan);
            var assigned = new RequirementCheckEvent(owner.Owner, owner.Comp);
            entMan.EventBus.RaiseLocalEvent(objective, ref assigned);
            Assert.That(assigned.Cancelled, Is.True,
                "Only one living, non-allied crew member is eligible.");

            SpawnCrew(entMan, coords);
            assigned = new RequirementCheckEvent(owner.Owner, owner.Comp);
            entMan.EventBus.RaiseLocalEvent(objective, ref assigned);
            Assert.That(assigned.Cancelled, Is.False);

            entMan.DeleteEntity(objective);
            entMan.DeleteEntity(map);
            entMan.DeleteEntity(otherMap);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OnlyPostAcceptanceCasualtiesCountAndRevivalRemovesProgress()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var maps = entMan.System<SharedMapSystem>();
        var mobState = entMan.System<MobStateSystem>();

        await server.WaitAssertion(() =>
        {
            var map = maps.CreateMap();
            var coords = new EntityCoordinates(map, Vector2.Zero);
            var (_, owner) = SpawnCrew(entMan, coords, traitor: true);
            var (firstBody, _) = SpawnCrew(entMan, coords);
            var (secondBody, _) = SpawnCrew(entMan, coords);
            var objective = SpawnObjective(entMan);
            var assigned = new RequirementCheckEvent(owner.Owner, owner.Comp);
            entMan.EventBus.RaiseLocalEvent(objective, ref assigned);
            Assert.That(assigned.Cancelled, Is.False);

            // The offering exists, but casualties before acceptance must not earn progress or an instant payout.
            mobState.ChangeMobState(firstBody, MobState.Dead);
            Assert.That(GetProgress(entMan, objective, owner), Is.Zero);

            var contracts = entMan.AddComponent<ContractsComponent>(owner.Owner);
            var taken = new ContractTakenEvent((owner.Owner, contracts), owner);
            entMan.EventBus.RaiseLocalEvent(objective, ref taken);
            Assert.That(GetProgress(entMan, objective, owner), Is.Zero);

            var (lateArrival, lateMind) = SpawnCrew(entMan, coords);
            Assert.That(GetProgress(entMan, objective, owner), Is.Zero);

            mobState.ChangeMobState(secondBody, MobState.Dead);
            Assert.That(GetProgress(entMan, objective, owner), Is.EqualTo(0.5f));
            mobState.ChangeMobState(secondBody, MobState.Alive);
            Assert.That(GetProgress(entMan, objective, owner), Is.Zero);

            // Gibbing/deleting a living target's body must count, using the same IC death check as kill objectives.
            entMan.DeleteEntity(secondBody);
            Assert.That(GetProgress(entMan, objective, owner), Is.EqualTo(0.5f));
            mobState.ChangeMobState(lateArrival, MobState.Dead);
            Assert.That(GetProgress(entMan, objective, owner), Is.EqualTo(1f));

            entMan.System<SharedRoleSystem>().MindAddRole(lateMind.Owner, "MindRoleTraitor", silent: true);
            Assert.That(GetProgress(entMan, objective, owner), Is.EqualTo(0.5f),
                "An allied agent must not count, even if recruited after acceptance.");

            entMan.DeleteEntity(objective);
            entMan.DeleteEntity(map);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AcceptingAnOldOfferRechecksRequirementsBeforePayingTheAdvance()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var maps = entMan.System<SharedMapSystem>();
        var reputation = entMan.System<ReputationSystem>();
        var objectives = entMan.System<ObjectivesSystem>();

        await server.WaitAssertion(() =>
        {
            var map = maps.CreateMap();
            var coords = new EntityCoordinates(map, Vector2.Zero);
            var (ownerBody, owner) = SpawnCrew(entMan, coords, traitor: true);
            var (victim, _) = SpawnCrew(entMan, coords);
            SpawnCrew(entMan, coords);
            reputation.AddContracts(ownerBody, null);
            var contracts = entMan.GetComponent<ContractsComponent>(owner.Owner);
            var objective = objectives.TryCreateObjective(owner.Owner, owner.Comp, "PirateMassCasualtyTestContract");
            Assert.That(objective, Is.Not.Null);
            var objectiveId = objective!.Value;

            entMan.System<MobStateSystem>().ChangeMobState(victim, MobState.Dead);
            Assert.That(reputation.TryTakeContract((owner.Owner, contracts), objectiveId), Is.False);
            Assert.That(owner.Comp.Objectives, Does.Not.Contain(objectiveId));
            Assert.That(entMan.GetComponent<MassCasualtyConditionComponent>(objectiveId).Active, Is.False);

            SpawnCrew(entMan, coords);
            Assert.That(reputation.TryTakeContract((owner.Owner, contracts), objectiveId), Is.True);
            Assert.That(entMan.GetComponent<MassCasualtyConditionComponent>(objectiveId).Active, Is.True);
            Assert.That(GetProgress(entMan, objectiveId, owner), Is.Zero);
            entMan.DeleteEntity(map);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LateArrivalsAreTrackedWithoutOpeningTheUplink()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var maps = entMan.System<SharedMapSystem>();
        EntityUid map = default;
        EntityUid objective = default;
        EntityUid lateArrival = default;
        Entity<MindComponent> owner = default;

        await server.WaitAssertion(() =>
        {
            map = maps.CreateMap();
            var coords = new EntityCoordinates(map, Vector2.Zero);
            owner = SpawnCrew(entMan, coords, traitor: true).Mind;
            SpawnCrew(entMan, coords);
            SpawnCrew(entMan, coords);
            objective = SpawnObjective(entMan);
            var contracts = entMan.AddComponent<ContractsComponent>(owner.Owner);
            var taken = new ContractTakenEvent((owner.Owner, contracts), owner);
            entMan.EventBus.RaiseLocalEvent(objective, ref taken);
            lateArrival = SpawnCrew(entMan, coords).Body;
        });

        // No progress/UI requests occur before the new crew member dies.
        await server.WaitRunTicks(120);
        await server.WaitAssertion(() =>
        {
            entMan.System<MobStateSystem>().ChangeMobState(lateArrival, MobState.Dead);
            Assert.That(GetProgress(entMan, objective, owner), Is.EqualTo(0.5f));
            entMan.DeleteEntity(objective);
            entMan.DeleteEntity(map);
        });

        await pair.CleanReturnAsync();
    }

    private static (EntityUid Body, Entity<MindComponent> Mind) SpawnCrew(
        IEntityManager entMan,
        EntityCoordinates coordinates,
        bool traitor = false,
        bool hasJob = true)
    {
        var body = entMan.SpawnEntity("MobHuman", coordinates);
        var minds = entMan.System<SharedMindSystem>();
        var roles = entMan.System<SharedRoleSystem>();
        var mind = minds.CreateMind(null);
        minds.TransferTo(mind.Owner, body, mind: mind.Comp);
        if (hasJob)
            roles.MindAddJobRole(mind.Owner, silent: true, jobPrototype: "Passenger");
        if (traitor)
            roles.MindAddRole(mind.Owner, "MindRoleTraitor", silent: true);
        return (body, mind);
    }

    private static EntityUid SpawnObjective(IEntityManager entMan)
    {
        return entMan.SpawnEntity("PirateMassCasualtyTestContract", MapCoordinates.Nullspace);
    }

    private static float GetProgress(IEntityManager entMan, EntityUid objective, Entity<MindComponent> owner)
    {
        var progress = new ObjectiveGetProgressEvent(owner.Owner, owner.Comp);
        entMan.EventBus.RaiseLocalEvent(objective, ref progress);
        return progress.Progress ?? 0f;
    }
}
