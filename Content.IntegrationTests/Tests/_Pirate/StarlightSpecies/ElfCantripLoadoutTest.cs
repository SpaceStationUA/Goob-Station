using System.Linq;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.Preferences.Managers;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Pirate.StarlightSpecies;

[TestFixture]
public sealed class ElfCantripLoadoutTest
{
    [TestCase("Elf", false, 1)]
    [TestCase("NeoElf", false, 1)]
    [TestCase("Human", false, 0)]
    [TestCase("Elf", true, 1)]
    [TestCase("NeoElf", true, 1)]
    [TestCase("Human", true, 0)]
    public async Task AntagSpawnGrantsOneGrimoireWithoutDuplicatingCrewLoadouts(string species, bool existingCrew, int expectedBooks)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            InLobby = true,
            Dirty = true,
        });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var session = pair.Player!;
        HumanoidCharacterProfile profile = default!;
        await server.WaitPost(() =>
        {
            profile = HumanoidCharacterProfile.DefaultWithSpecies(species);
            server.ResolveDependency<IServerPreferencesManager>().SetProfile(session.UserId, 0, profile).Wait();
        });

        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            EntityUid? crew = null;
            EntityUid[] originalBooks = [];
            Assert.That(session.AttachedEntity, Is.Null);
            if (existingCrew)
            {
                crew = em.Spawn("Mob" + species, map.MapCoords);
                var minds = em.System<MindSystem>();
                minds.TransferTo(minds.CreateMind(session.UserId, session.Name), crew.Value);
                var spawned = new PlayerSpawnCompleteEvent(crew.Value, session, jobId: null,
                    lateJoin: false, silent: true, joinOrder: 1, station: crew.Value, profile);
                em.EventBus.RaiseLocalEvent(crew.Value, spawned, broadcast: true);
                originalBooks = Books();
                Assert.That(originalBooks, Has.Length.EqualTo(expectedBooks));
            }

            var rule = em.Spawn(null, MapCoordinates.Nullspace);
            var selection = em.AddComponent<AntagSelectionComponent>(rule);
            em.AddComponent<AntagLoadProfileRuleComponent>(rule);
            var definition = new AntagSelectionDefinition { PrefRoles = ["Nukeops"] };
            Assert.That(em.System<AntagSelectionSystem>().MakeAntag((rule, selection), session, definition), Is.True);
            Assert.That(session.AttachedEntity, Is.Not.Null);
            var mob = session.AttachedEntity!.Value;
            em.System<SharedTransformSystem>().SetMapCoordinates(mob, map.MapCoords);
            Assert.That(em.GetComponent<HumanoidAppearanceComponent>(mob).Species.Id, Is.EqualTo(species));
            Assert.That(Books(), Has.Length.EqualTo(expectedBooks));
            if (existingCrew)
            {
                Assert.That(mob, Is.EqualTo(crew));
                Assert.That(Books(), Is.EquivalentTo(originalBooks), "Crew must retain their original grimoire without receiving another.");
            }

            EntityUid[] Books() => em.EntityQuery<MetaDataComponent>()
                .Where(meta => meta.EntityPrototype?.ID == "BookOfCantrips")
                .Select(meta => meta.Owner)
                .ToArray();
        });
        await pair.CleanReturnAsync();
    }
}
