// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Server.Antag.Components;
using Content.Server.Administration.Managers;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared._Pirate.MalfAI;
using Content.Shared.Administration;
using Content.Shared.Mind.Components;
using Content.Shared.Players;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Roles;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests._Pirate;

[TestFixture]
public sealed class MalfAiAdminVerbTest
{
    [Test]
    public async Task FunAdminAssignsMalfAiToStationAiOnly()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false,
            Dirty = true
        });

        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        var mindSystem = entMan.System<MindSystem>();
        var roleSystem = entMan.System<SharedRoleSystem>();
        var session = server.PlayerMan.Sessions.Single();
        var adminManager = server.ResolveDependency<IAdminManager>();

        EntityUid ai = default;
        EntityUid core = default;
        EntityUid mindId = default;

        await server.WaitAssertion(() =>
        {
            Assert.That(adminManager.HasAdminFlag(session, AdminFlags.Fun), Is.True,
                "The integration-test session must have Fun admin permission.");

            core = entMan.SpawnEntity("PlayerStationAiEmpty", map.GridCoords);
            entMan.System<SharedPowerReceiverSystem>().SetNeedsPower(core, false);

            ai = entMan.SpawnEntity("StationAiBrain", map.GridCoords);
            var holder = entMan.System<SharedContainerSystem>().GetContainer(
                core,
                StationAiHolderComponent.Container) as ContainerSlot;
            Assert.That(holder, Is.Not.Null, "The AI core must expose its brain slot.");
            Assert.That(entMan.System<SharedContainerSystem>().Insert(ai, holder!), Is.True);

            var existingMind = session.ContentData()!.Mind;
            Assert.That(existingMind, Is.Not.Null, "The test session must already have a mind.");
            mindId = existingMind!.Value;
            mindSystem.TransferTo(mindId, ai, ghostCheckOverride: true);
            server.PlayerMan.SetAttachedEntity(session, ai);

        });

        // Allow the player attachment and AiHeld container prototype to settle before
        // requesting the same server-side verbs that the context menu requests.
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<StationAiHeldComponent>(ai), Is.True);
                Assert.That(entMan.HasComponent<MindContainerComponent>(ai), Is.True);
                Assert.That(entMan.HasComponent<ActorComponent>(ai), Is.True);
            });

            var verbs = new GetVerbsEvent<Verb>(
                session.AttachedEntity!.Value,
                ai,
                @using: null,
                hands: null,
                canInteract: true,
                canComplexInteract: true,
                canAccess: true,
                extraCategories: new List<VerbCategory>());
            entMan.EventBus.RaiseLocalEvent(ai, verbs, true);

            var malfText = Loc.GetString("admin-verb-text-make-malfai");
            var malfVerbs = verbs.Verbs
                .Where(verb => verb.Category == VerbCategory.Antag && verb.Text == malfText)
                .ToList();
            Assert.That(malfVerbs, Has.Count.EqualTo(1),
                "A Fun admin viewing a Station AI must receive exactly one Malf AI verb.");

            var malfVerb = malfVerbs.Single();
            Assert.That(malfVerb.Act, Is.Not.Null);
            malfVerb.Act!.Invoke();

            var mind = entMan.GetComponent<MindComponent>(mindId);
            Assert.Multiple(() =>
            {
                Assert.That(session.ContentData()!.Mind, Is.EqualTo(mindId));
                Assert.That(mind.OwnedEntity, Is.EqualTo(ai),
                    "Malf assignment must retain the existing AI mind/entity pair.");
                Assert.That(entMan.HasComponent<MalfAiMarkerComponent>(ai), Is.True);
                Assert.That(roleSystem.MindHasRole<MalfAiRoleComponent>(mindId), Is.True);
                Assert.That(mind.MindRoleContainer.ContainedEntities.Count(role =>
                        entMan.HasComponent<MalfAiRoleComponent>(role)),
                    Is.EqualTo(1),
                    "The no-rule ForceMakeAntag path must add exactly one Malf AI role.");
            });

            var repeatedVerbs = new GetVerbsEvent<Verb>(
                session.AttachedEntity!.Value,
                ai,
                @using: null,
                hands: null,
                canInteract: true,
                canComplexInteract: true,
                canAccess: true,
                extraCategories: new List<VerbCategory>());
            entMan.EventBus.RaiseLocalEvent(ai, repeatedVerbs, true);
            Assert.That(repeatedVerbs.Verbs.Any(verb =>
                    verb.Category == VerbCategory.Antag && verb.Text == malfText),
                Is.False,
                "An already assigned Malf AI must not receive a duplicate Malf verb.");

            var ordinary = entMan.SpawnEntity("MobHuman", map.GridCoords.Offset(2, 0));

            var ordinaryMind = mindSystem.CreateMind(null, "Ordinary human");
            mindSystem.TransferTo(ordinaryMind, ordinary, ghostCheckOverride: true);

            var ordinaryVerbs = new GetVerbsEvent<Verb>(
                session.AttachedEntity!.Value,
                ordinary,
                @using: null,
                hands: null,
                canInteract: true,
                canComplexInteract: true,
                canAccess: true,
                extraCategories: new List<VerbCategory>());
            entMan.EventBus.RaiseLocalEvent(ordinary, ordinaryVerbs, true);

            Assert.That(ordinaryVerbs.Verbs.Any(verb =>
                    verb.Category == VerbCategory.Antag && verb.Text == malfText),
                Is.False,
                "An ordinary MobHuman must not receive the Malf AI verb.");
        });

        await pair.CleanReturnAsync();
    }
}
