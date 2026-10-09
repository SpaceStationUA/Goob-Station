// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using Content.IntegrationTests.Pair;
using Content.Server.Database;
using Content.Shared._Pirate.CustomMarkings;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Robust.Client.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.UnitTesting;
using SixLabors.ImageSharp.PixelFormats;
using ClientCustomMarkingSystem = Content.Client._Pirate.CustomMarkings.CustomMarkingSystem;
using ServerCustomMarkingSystem = Content.Server._Pirate.CustomMarkings.CustomMarkingSystem;
using StoredArt = Content.Server._Pirate.CustomMarkings.CustomMarkingStoredArt;

namespace Content.IntegrationTests.Tests._Pirate.CustomMarkings;

/// <summary>
/// The worn list through validation and the database, the library and art tables, and a marking's whole trip:
/// saved by a client, worn by a body, fetched and drawn, then blocked.
/// </summary>
[TestFixture]
[TestOf(typeof(ServerCustomMarkingSystem))]
public sealed class CustomMarkingLibraryTest
{
    private const int MaxWaitTicks = 300;

    private static readonly string HashA = new('a', CustomMarkingRules.HashLength);
    private static readonly string HashB = new('b', CustomMarkingRules.HashLength);

    /// <summary>A made-up art hash.</summary>
    private static string FakeHash(char digit)
    {
        return new string(digit, CustomMarkingRules.HashLength);
    }

    [Test]
    public async Task ProfileRoundTripTest()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var db = GetDb(server);
        var user = new NetUserId(Guid.NewGuid());

        var worn = new List<CustomMarking>
        {
            new(HashA, CustomMarkingPlacement.Behind),
            new(HashB, CustomMarkingPlacement.Front),
        };

        HumanoidCharacterProfile valid = default!;
        await server.WaitAssertion(() =>
        {
            var crafted = new List<CustomMarking>(worn) { new("junk", CustomMarkingPlacement.Skin), worn[0] };
            for (var i = 0; i < 20; i++)
            {
                crafted.Add(new CustomMarking(i.ToString("x64"), CustomMarkingPlacement.Skin));
            }

            var profile = HumanoidCharacterProfile.DefaultWithSpecies();
            valid = (HumanoidCharacterProfile) profile.WithCustomMarkings(worn).Validated(pair.Player!, IoCManager.Instance!);
            var cut = (HumanoidCharacterProfile) profile.WithCustomMarkings(crafted).Validated(pair.Player!, IoCManager.Instance!);
            var max = server.CfgMan.GetCVar(CustomMarkingCVars.MaxWorn);

            Assert.Multiple(() =>
            {
                Assert.That(valid.CustomMarkings, Is.EqualTo(worn), "validation keeps a good list");
                Assert.That(cut.CustomMarkings, Has.Count.EqualTo(max), "validation cuts a list to the server's limit");
                Assert.That(cut.CustomMarkings.Take(2), Is.EqualTo(worn), "and drops what isn't a marking");
                Assert.That(valid.Clone().CustomMarkings, Is.EqualTo(worn), "copies keep the list");
                Assert.That(valid.Clone().CustomMarkings, Is.Not.SameAs(valid.CustomMarkings), "as their own");
                Assert.That(valid.MemberwiseEquals(valid.Clone()), Is.True);
                Assert.That(valid.MemberwiseEquals(valid.WithCustomMarkings(worn.Take(1))), Is.False);
            });

            // Pirate: a crafted null list must reach validation through the copy constructor.
            var nullProfile = profile.Clone();
            nullProfile.CustomMarkings = null!;
            var validatedNull = (HumanoidCharacterProfile) nullProfile.Validated(pair.Player!, IoCManager.Instance!);
            Assert.Multiple(() =>
            {
                Assert.That(validatedNull.CustomMarkings, Is.Empty);
                Assert.That(nullProfile.Clone().CustomMarkings, Is.Empty);
                Assert.That(nullProfile.MemberwiseEquals(profile.WithCustomMarkings([])), Is.True);
                Assert.That(() => nullProfile.GetHashCode(), Throws.Nothing);
            });

            // A character exported to a file and imported again.
            var humanoids = server.System<SharedHumanoidAppearanceSystem>();
            using var file = new MemoryStream();
            using (var writer = new StreamWriter(file, leaveOpen: true))
            {
                humanoids.ToDataNode(valid).Write(writer);
            }

            file.Position = 0;
            Assert.That(humanoids.FromStream(file, pair.Player!).CustomMarkings, Is.EqualTo(worn), "an exported character keeps the list");
        });

        await db.InitPrefsAsync(user, valid);
        var prefs = await db.GetPlayerPreferencesAsync(user);
        var loaded = (HumanoidCharacterProfile) prefs!.Characters.Single().Value;
        Assert.That(loaded.CustomMarkings, Is.EqualTo(worn));

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LibraryStorageTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var db = GetDb(pair.Server);
        var user = Guid.NewGuid();
        var other = Guid.NewGuid();
        var pngA = new byte[] { 1, 2, 3 };
        var pngB = new byte[] { 4, 5 };
        var artA = new StoredArt(pngA, null, null);

        // Animated art that erases some of the body: its frame times and its mask are stored with the sheet.
        var timesB = new byte[] { 200, 0, 44, 1 };
        var eraseB = new byte[CustomMarkingRules.EraseBytes];
        eraseB[7] = 0x10;
        var artB = new StoredArt(pngB, timesB, eraseB);
        const int limit = 2;

        var added = await db.SaveCustomMarkingAsync(user, 0, "One", (int) CustomMarkingPlacement.Skin, HashA, artA, limit);
        Assert.That(added.Error, Is.Null);
        var id = added.Entry!.Id;
        Assert.Multiple(() =>
        {
            Assert.That(id, Is.GreaterThan(0));
            Assert.That(added.Entry.ArtHash, Is.EqualTo(HashA));
            Assert.That(added.PreviousHash, Is.Null);
        });
        Assert.That((await db.GetCustomMarkingArtAsync(HashA))?.Png, Is.EqualTo(pngA));

        // A change without art keeps the art.
        var renamed = await db.SaveCustomMarkingAsync(user, id, "Renamed", (int) CustomMarkingPlacement.Hair, null, null, limit);
        Assert.Multiple(() =>
        {
            Assert.That(renamed.Entry!.Name, Is.EqualTo("Renamed"));
            Assert.That(renamed.Entry.Placement, Is.EqualTo((int) CustomMarkingPlacement.Hair));
            Assert.That(renamed.Entry.ArtHash, Is.EqualTo(HashA));
            Assert.That(renamed.PreviousHash, Is.Null);
        });

        // New art replaces the entry's, and the old art stays for characters still wearing it.
        var redrawn = await db.SaveCustomMarkingAsync(user, id, "Renamed", (int) CustomMarkingPlacement.Hair, HashB, artB, limit);
        Assert.Multiple(() =>
        {
            Assert.That(redrawn.Entry!.ArtHash, Is.EqualTo(HashB));
            Assert.That(redrawn.PreviousHash, Is.EqualTo(HashA));
        });
        Assert.That((await db.GetCustomMarkingArtAsync(HashA))?.Png, Is.EqualTo(pngA));

        var storedA = await db.GetCustomMarkingArtAsync(HashA);
        var storedB = await db.GetCustomMarkingArtAsync(HashB);
        Assert.Multiple(() =>
        {
            Assert.That(storedA!.FrameTimes, Is.Null, "a still marking has no frame times");
            Assert.That(storedA.Erase, Is.Null);
            Assert.That(storedB!.Png, Is.EqualTo(pngB));
            Assert.That(storedB.FrameTimes, Is.EqualTo(timesB));
            Assert.That(storedB.Erase, Is.EqualTo(eraseB));
        });

        // Art another player already saved is shared, not stored twice or overwritten.
        var shared = await db.SaveCustomMarkingAsync(other, 0, "Theirs", (int) CustomMarkingPlacement.Skin, HashB, new StoredArt(new byte[] { 9 }, null, null), limit);
        Assert.That(shared.Error, Is.Null);
        Assert.That((await db.GetCustomMarkingArtAsync(HashB))?.Png, Is.EqualTo(pngB));

        Assert.That((await db.SaveCustomMarkingAsync(other, id, "Stolen", 0, null, null, limit)).Error,
            Is.EqualTo("wf-custom-marking-error-missing"), "another player's entry can't be changed");
        Assert.That(await db.DeleteCustomMarkingAsync(other, id), Is.False, "or deleted");
        Assert.That((await db.SaveCustomMarkingAsync(user, 0, "No art", 0, null, null, limit)).Error,
            Is.EqualTo("wf-custom-marking-error-invalid"), "a new entry needs art");

        Assert.That((await db.SaveCustomMarkingAsync(user, 0, "Two", 0, HashA, artA, limit)).Error, Is.Null);
        Assert.That((await db.SaveCustomMarkingAsync(user, 0, "Three", 0, HashA, artA, limit)).Error,
            Is.EqualTo("wf-custom-marking-error-full"));
        Assert.That((await db.SaveCustomMarkingAsync(user, id, "Still editable", 0, null, null, limit)).Error, Is.Null,
            "a full library can still be changed");

        var library = await db.GetCustomMarkingsAsync(user);
        Assert.That(library.Select(entry => entry.Name), Is.EqualTo(new[] { "Still editable", "Two" }));
        Assert.That(await db.GetCustomMarkingsAsync(other), Has.Count.EqualTo(1));

        // Blocking hides art from everyone and refuses it from then on.
        Assert.That(await db.SetCustomMarkingArtBlockedAsync(HashA, true), Is.EqualTo(user), "reports who first saved it");
        Assert.That(await db.SetCustomMarkingArtBlockedAsync(new string('c', CustomMarkingRules.HashLength), true), Is.Null);
        Assert.That(await db.GetCustomMarkingArtAsync(HashA), Is.Null);
        Assert.That((await db.SaveCustomMarkingAsync(other, 0, "Again", 0, HashA, artA, limit)).Error,
            Is.EqualTo("wf-custom-marking-error-blocked"));
        var blockedEntry = library.Single(entry => entry.ArtHash == HashA);
        Assert.That((await db.SaveCustomMarkingAsync(user, blockedEntry.Id, "Blocked rename", (int) CustomMarkingPlacement.Hair,
                null, null, limit)).Error,
            Is.EqualTo("wf-custom-marking-error-blocked"), "metadata-only saves also reject blocked art");
        var unchanged = (await db.GetCustomMarkingsAsync(user)).Single(entry => entry.Id == blockedEntry.Id);
        Assert.Multiple(() =>
        {
            Assert.That(unchanged.Name, Is.EqualTo(blockedEntry.Name));
            Assert.That(unchanged.Placement, Is.EqualTo(blockedEntry.Placement));
        });
        Assert.That(await db.SetCustomMarkingArtBlockedAsync(HashA, false), Is.EqualTo(user));
        Assert.That((await db.GetCustomMarkingArtAsync(HashA))?.Png, Is.EqualTo(pngA));

        Assert.That(await db.DeleteCustomMarkingAsync(user, id), Is.True);
        Assert.That(await db.GetCustomMarkingsAsync(user), Has.Count.EqualTo(1));
        Assert.That((await db.GetCustomMarkingArtAsync(HashB))?.Png, Is.EqualTo(pngB), "deleting an entry leaves its art");

        await pair.CleanReturnAsync();
    }

    /// <summary>Profiles put their markings on bodies, copies of a body keep them, and the setting turns them off.</summary>
    [Test]
    public async Task BodyWearsProfileMarkingsTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var worn = new List<CustomMarking> { new(HashA, CustomMarkingPlacement.Skin), new(HashB, CustomMarkingPlacement.Front) };

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var humanoids = entMan.System<SharedHumanoidAppearanceSystem>();
            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human").WithCustomMarkings(worn);

            var body = entMan.SpawnEntity("MobHuman", map.GridCoords);
            humanoids.LoadProfile(body, profile);
            Assert.That(entMan.GetComponent<HumanoidAppearanceComponent>(body).CustomMarkings, Is.EqualTo(worn));

            var clone = entMan.SpawnEntity("MobHuman", map.GridCoords);
            humanoids.CloneAppearance(body, clone);
            var copied = entMan.GetComponent<HumanoidAppearanceComponent>(clone).CustomMarkings;
            Assert.That(copied, Is.EqualTo(worn));
            Assert.That(copied, Is.Not.SameAs(entMan.GetComponent<HumanoidAppearanceComponent>(body).CustomMarkings));

            humanoids.LoadProfile(body, profile.WithCustomMarkings(new List<CustomMarking>()));
            Assert.That(entMan.GetComponent<HumanoidAppearanceComponent>(body).CustomMarkings, Is.Empty, "a profile without any takes them off");

            server.CfgMan.SetCVar(CustomMarkingCVars.Enabled, false);
            try
            {
                humanoids.LoadProfile(body, profile);
                Assert.That(entMan.GetComponent<HumanoidAppearanceComponent>(body).CustomMarkings, Is.Empty, "the setting turns them off");
            }
            finally
            {
                server.CfgMan.SetCVar(CustomMarkingCVars.Enabled, true);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SavedMarkingIsDrawnTest()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var cSystem = client.System<ClientCustomMarkingSystem>();
        var sSystem = server.System<ServerCustomMarkingSystem>();

        var art = new CustomMarkingArt();
        art.SetPixel(0, CustomMarkingArt.South, 15, 10, new Rgba32(200, 30, 30, 255));
        art.SetPixel(0, CustomMarkingArt.West, 2, 3, new Rgba32(0, 0, 0, 90));
        var hash = ServerCustomMarkingSystem.Hash(art);

        CustomMarkingSaveResultEvent answer = null;
        void OnAnswer(CustomMarkingSaveResultEvent ev) => answer = ev;
        await client.WaitPost(() => cSystem.SaveAnswered += OnAnswer);

        // Something that isn't a drawing is refused.
        await client.WaitPost(() => cSystem.Save(0, "Blank", CustomMarkingPlacement.Skin, new CustomMarkingArt()));
        await WaitFor(pair, () => answer != null, "the server to answer the blank save");
        Assert.That(answer.Error, Is.EqualTo("wf-custom-marking-error-blank"));
        Assert.That(answer.Entry, Is.Null);

        // The client saves a drawing to its library.
        answer = null;
        var request = 0;
        await client.WaitPost(() => request = cSystem.Save(0, "  My mark  ", CustomMarkingPlacement.Skin, art));
        await WaitFor(pair, () => answer != null, "the server to answer the save");
        Assert.Multiple(() =>
        {
            Assert.That(answer.Request, Is.EqualTo(request));
            Assert.That(answer.Error, Is.Null);
            Assert.That(answer.Entry?.Name, Is.EqualTo("My mark"));
            Assert.That(answer.Entry?.Hash, Is.EqualTo(hash));
            Assert.That(answer.Entry?.Placement, Is.EqualTo(CustomMarkingPlacement.Skin));
        });
        await WaitFor(pair, () => cSystem.Library is { Count: 1 }, "the library to reach the client");
        Assert.That(cSystem.Library![0], Is.EqualTo(answer.Entry));

        // Saves are spaced out.
        var saved = answer.Entry;
        answer = null;
        await client.WaitPost(() => cSystem.Save(saved!.Value.Id, "Renamed", CustomMarkingPlacement.Hair, null));
        await WaitFor(pair, () => answer != null, "the server to answer the hasty save");
        Assert.That(answer.Error, Is.EqualTo("wf-custom-marking-error-cooldown"));
        await client.WaitPost(() => cSystem.SaveAnswered -= OnAnswer);

        // A body wearing it: the client fetches the art by hash and draws it.
        var testMap = await pair.CreateTestMap();
        EntityUid body = default;
        await server.WaitPost(() =>
        {
            body = server.EntMan.SpawnEntity("MobHuman", testMap.GridCoords);
            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human")
                .WithCustomMarkings(new List<CustomMarking> { new(hash, CustomMarkingPlacement.Skin) });
            server.EntMan.System<SharedHumanoidAppearanceSystem>().LoadProfile(body, profile);
            server.PlayerMan.SetAttachedEntity(pair.Player!, body);
        });

        await pair.RunTicksSync(5);
        var clientBody = pair.ToClientUid(body);
        var sprites = client.System<SpriteSystem>();
        await WaitFor(pair, () => HasLayer(), "the client to draw the marking");
        await client.WaitAssertion(() =>
        {
            Assert.That(cSystem.TryGetPng(hash, out var png), Is.True);
            Assert.That(Content.Client._Pirate.CustomMarkings.CustomMarkingPng.Read(png).SamePixels(art), Is.True, "the art arrives as it was drawn");
        });

        // An admin blocks the art: it comes off the body and the client forgets it.
        var blocked = false;
        await server.WaitPost(async () => blocked = await sSystem.SetBlocked(hash, true, "the test"));
        await WaitFor(pair, () => blocked, "the block to be saved");
        await WaitFor(pair, () => !HasLayer(), "the client to stop drawing blocked art");
        await server.WaitAssertion(() =>
            Assert.That(server.EntMan.GetComponent<HumanoidAppearanceComponent>(body).CustomMarkings, Is.Empty));
        await client.WaitAssertion(() => Assert.That(cSystem.TryGetArt(hash, out _), Is.False));

        await pair.CleanReturnAsync();
        return;

        bool HasLayer()
        {
            return client.EntMan.TryGetComponent(clientBody, out SpriteComponent sprite)
                   && sprites.LayerMapTryGet((clientBody, sprite), ClientCustomMarkingSystem.LayerKey(0), out _, false);
        }
    }

    /// <summary>A player only gets to store so many new drawings a day, however few their library holds at once.</summary>
    [Test]
    public async Task DailyArtLimitTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var db = GetDb(pair.Server);
        var user = Guid.NewGuid();
        var other = Guid.NewGuid();
        var art = new StoredArt(new byte[] { 1 }, null, null);
        const int library = 24;
        const int daily = 2;

        // One entry, redrawn again and again: the library never grows, but each new drawing is a row of art.
        var first = await db.SaveCustomMarkingAsync(user, 0, "Mark", 0, FakeHash('1'), art, library, daily);
        Assert.That(first.Error, Is.Null);
        var id = first.Entry!.Id;
        Assert.That((await db.SaveCustomMarkingAsync(user, id, "Mark", 0, FakeHash('2'), art, library, daily)).Error, Is.Null);

        var refused = await db.SaveCustomMarkingAsync(user, id, "Mark", 0, FakeHash('3'), art, library, daily);
        Assert.Multiple(() =>
        {
            Assert.That(refused.Error, Is.EqualTo("wf-custom-marking-error-daily"));
            Assert.That(refused.Entry, Is.Null);
        });
        Assert.That(await db.GetCustomMarkingArtAsync(FakeHash('3')), Is.Null, "what is refused isn't stored");
        Assert.That((await db.GetCustomMarkingsAsync(user)).Single().ArtHash, Is.EqualTo(FakeHash('2')), "and the entry keeps its art");
        Assert.That((await db.SaveCustomMarkingAsync(user, 0, "Another", 0, FakeHash('3'), art, library, daily)).Error,
            Is.EqualTo("wf-custom-marking-error-daily"), "a new entry is no way round it");

        // Going back to a drawing the server already holds adds nothing, so it is always allowed; so is a rename.
        Assert.That((await db.SaveCustomMarkingAsync(user, id, "Mark", 0, FakeHash('1'), art, library, daily)).Error, Is.Null);
        Assert.That((await db.SaveCustomMarkingAsync(user, id, "Renamed", 1, null, null, library, daily)).Error, Is.Null);

        // Each player has a count of their own, and no limit means none.
        Assert.That((await db.SaveCustomMarkingAsync(other, 0, "Theirs", 0, FakeHash('3'), art, library, daily)).Error, Is.Null);
        Assert.That((await db.SaveCustomMarkingAsync(user, id, "Mark", 0, FakeHash('4'), art, library)).Error, Is.Null);

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Art nothing uses is found, kept a while and then deleted. What a library holds or a saved character wears
    /// stays, and so does what an admin blocked.
    /// </summary>
    [Test]
    public async Task UnusedArtCleanupTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var db = GetDb(server);
        var user = new NetUserId(Guid.NewGuid());
        var other = Guid.NewGuid();
        var art = new StoredArt(new byte[] { 1 }, null, null);
        var month = TimeSpan.FromDays(30);
        const int limit = 24;

        var held = FakeHash('1');
        var worn = FakeHash('2');
        var loose = FakeHash('3');
        var blocked = FakeHash('4');
        var back = FakeHash('5');

        // Each is saved to the library, and all but the first are deleted from it again, which leaves their art.
        var ids = new Dictionary<string, int>();
        foreach (var hash in new[] { held, worn, loose, blocked, back })
        {
            var saved = await db.SaveCustomMarkingAsync(user, 0, "Mark", 0, hash, art, limit);
            Assert.That(saved.Error, Is.Null);
            ids[hash] = saved.Entry!.Id;
        }

        foreach (var hash in new[] { worn, loose, blocked, back })
        {
            Assert.That(await db.DeleteCustomMarkingAsync(user, ids[hash]), Is.True);
        }

        Assert.That(await db.SetCustomMarkingArtBlockedAsync(blocked, true), Is.EqualTo(user.UserId));

        // A saved character still wears one of them.
        HumanoidCharacterProfile character = default!;
        await server.WaitPost(() =>
        {
            character = HumanoidCharacterProfile.DefaultWithSpecies()
                .WithCustomMarkings(new List<CustomMarking> { new(worn, CustomMarkingPlacement.Skin) });
        });
        await db.InitPrefsAsync(user, character);

        // The first look deletes nothing: what is unused is only noted. Nor does a second, within the time it is kept.
        Assert.That(await db.PurgeUnusedCustomMarkingArtAsync(month), Is.EqualTo((2, 0)), "the loose one and the one to come back");
        Assert.That(await db.PurgeUnusedCustomMarkingArtAsync(month), Is.EqualTo((0, 0)));
        foreach (var hash in new[] { held, worn, loose, back })
        {
            Assert.That(await db.GetCustomMarkingArtAsync(hash), Is.Not.Null);
        }

        // One is taken up again before its time is out.
        var again = await db.SaveCustomMarkingAsync(user, 0, "Back", 0, back, art, limit);
        Assert.That(again.Error, Is.Null);

        // Once the time is out, what was noted and is still unused goes, and nothing else.
        Assert.That(await db.PurgeUnusedCustomMarkingArtAsync(TimeSpan.Zero), Is.EqualTo((0, 1)));
        Assert.That(await db.GetCustomMarkingArtAsync(loose), Is.Null, "unused art is deleted");
        Assert.That(await db.GetCustomMarkingArtAsync(held), Is.Not.Null, "art in a library stays");
        Assert.That(await db.GetCustomMarkingArtAsync(worn), Is.Not.Null, "art a saved character wears stays");
        Assert.That(await db.GetCustomMarkingArtAsync(back), Is.Not.Null, "art taken up again stays");
        Assert.That((await db.SaveCustomMarkingAsync(other, 0, "Again", 0, blocked, art, limit)).Error,
            Is.EqualTo("wf-custom-marking-error-blocked"), "blocked art stays on record, so it stays blocked");

        // Dropped once more, the one that came back starts over: noted first, deleted the time after.
        Assert.That(await db.DeleteCustomMarkingAsync(user, again.Entry!.Id), Is.True);
        Assert.That(await db.PurgeUnusedCustomMarkingArtAsync(TimeSpan.Zero), Is.EqualTo((1, 0)));
        Assert.That(await db.GetCustomMarkingArtAsync(back), Is.Not.Null);
        Assert.That(await db.PurgeUnusedCustomMarkingArtAsync(TimeSpan.Zero), Is.EqualTo((0, 1)));
        Assert.That(await db.GetCustomMarkingArtAsync(back), Is.Null);

        // Deleted art can be saved again like any new drawing.
        Assert.That((await db.SaveCustomMarkingAsync(user, 0, "Redrawn", 0, loose, art, limit)).Error, Is.Null);
        Assert.That(await db.GetCustomMarkingArtAsync(loose), Is.Not.Null);

        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CleanupWaitsForProfileSaveTest(bool firstProfile)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var pause = new ProfileSavePause();
        // Allow independent contexts, as PostgreSQL does, while the profile write is deliberately paused.
        var db = GetDb(server, pause, concurrent: true);
        var user = new NetUserId(Guid.NewGuid());
        var hash = FakeHash('6');
        HumanoidCharacterProfile bare = default!;
        await server.WaitPost(() => bare = HumanoidCharacterProfile.DefaultWithSpecies());
        if (!firstProfile)
            await db.InitPrefsAsync(user, bare);

        var entry = await db.SaveCustomMarkingAsync(user, 0, "Orphan", 0, hash, new StoredArt(new byte[] { 1 }, null, null), 24);
        Assert.That(entry.Error, Is.Null);
        Assert.That(await db.DeleteCustomMarkingAsync(user, entry.Entry!.Id), Is.True);
        Assert.That(await db.PurgeUnusedCustomMarkingArtAsync(TimeSpan.Zero), Is.EqualTo((1, 0)));

        var worn = bare.WithCustomMarkings(new List<CustomMarking> { new(hash, CustomMarkingPlacement.Skin) });
        pause.HashToPause = hash;
        Task save = firstProfile ? db.InitPrefsAsync(user, worn) : db.SaveCharacterSlotAsync(user, worn, 0);
        await pause.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var cleanup = db.PurgeUnusedCustomMarkingArtAsync(TimeSpan.Zero);
        try
        {
            Assert.That(await Task.WhenAny(cleanup, Task.Delay(100)), Is.Not.SameAs(cleanup),
                "cleanup cannot snapshot references while a character save/import is in progress");
        }
        finally
        {
            pause.Release.TrySetResult();
            await save;
            await cleanup;
        }

        Assert.That(await cleanup, Is.EqualTo((0, 0)));
        Assert.That(await db.GetCustomMarkingArtAsync(hash), Is.Not.Null, "the newly saved character keeps its artwork");
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SaveCompletionCannotRestoreBlockedArtTest(bool pendingRead)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var system = server.System<ServerCustomMarkingSystem>();
        var clientSystem = client.System<ClientCustomMarkingSystem>();
        var original = server.ResolveDependency<IServerDbManager>();
        var databaseSaved = new TaskCompletionSource<Content.Server._Pirate.CustomMarkings.CustomMarkingSaveResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var artRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayed = new Mock<IServerDbManager>();
        delayed.Setup(db => db.SaveCustomMarkingAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<StoredArt>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(async (Guid user, int id, string name, int placement, string hash, StoredArt art, int limit, int daily, CancellationToken cancel) =>
            {
                var result = await original.SaveCustomMarkingAsync(user, id, name, placement, hash, art, limit, daily, cancel);
                databaseSaved.TrySetResult(result);
                await release.Task;
                return result;
            });
        delayed.Setup(db => db.GetCustomMarkingsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid user, CancellationToken cancel) => original.GetCustomMarkingsAsync(user, cancel));
        delayed.Setup(db => db.GetCustomMarkingArtAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string hash, CancellationToken cancel) =>
            {
                var art = await original.GetCustomMarkingArtAsync(hash, cancel);
                if (pendingRead && art != null)
                {
                    artRead.TrySetResult();
                    await releaseRead.Task;
                }

                return art;
            });
        delayed.Setup(db => db.SetCustomMarkingArtBlockedAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns((string hash, bool blocked, CancellationToken cancel) => original.SetCustomMarkingArtBlockedAsync(hash, blocked, cancel));

        var database = typeof(ServerCustomMarkingSystem).GetField("_db", BindingFlags.Instance | BindingFlags.NonPublic)!;
        CustomMarkingSaveResultEvent? answer = null;
        void OnAnswer(CustomMarkingSaveResultEvent result) => answer = result;
        await server.WaitPost(() => database.SetValue(system, delayed.Object));
        try
        {
            var art = new CustomMarkingArt();
            // Each case blocks its art; the pooled database keeps those moderation records.
            art.SetPixel(0, CustomMarkingArt.South, 16, 16, new Rgba32((byte) (pendingRead ? 80 : 79), 113, 211, 255));
            await client.WaitPost(() =>
            {
                clientSystem.SaveAnswered += OnAnswer;
                clientSystem.Save(0, "Moderated while saving", CustomMarkingPlacement.Skin, art);
            });
            await WaitFor(pair, () => databaseSaved.Task.IsCompleted, "the database to accept the upload");
            var saved = await databaseSaved.Task;
            Assert.That(saved.Error, Is.Null);
            var hash = saved.Entry!.ArtHash;
            if (pendingRead)
            {
                await client.WaitPost(() => clientSystem.TryGetArt(hash, out _));
                await WaitFor(pair, () => artRead.Task.IsCompleted, "a positive art read to pause before publication");
            }
            Task<bool> block = default!;
            await server.WaitPost(() => block = system.SetBlocked(hash, true, "the test"));
            await WaitFor(pair, () => block.IsCompleted, "moderation to commit before save completion");
            Assert.That(await block, Is.True);

            release.TrySetResult();
            releaseRead.TrySetResult();
            await WaitFor(pair, () => answer != null, "the delayed save response");
            await pair.RunTicksSync(30);
            await client.WaitAssertion(() =>
            {
                Assert.That(answer!.Error, Is.Null);
                Assert.That(clientSystem.TryGetArt(hash, out _), Is.False,
                    "the upload continuation must not serve pixels blocked before it resumed");
            });
        }
        finally
        {
            release.TrySetResult();
            releaseRead.TrySetResult();
            await server.WaitPost(() => database.SetValue(system, original));
            await client.WaitPost(() => clientSystem.SaveAnswered -= OnAnswer);
        }

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// An animated marking that erases some of the body makes the whole trip: its frames, their times and its mask
    /// are saved, stored, fetched by a client and put on a body.
    /// </summary>
    [Test]
    public async Task AnimatedErasingMarkingTest()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var cSystem = client.System<ClientCustomMarkingSystem>();

        // Two frames on the middle of the torso, the second held longer, and a patch of the torso erased beside them.
        var art = new CustomMarkingArt();
        art.SetPixel(0, CustomMarkingArt.South, 15, 14, new Rgba32(200, 30, 30, 255));
        art.AddFrame(0);
        art.SetPixel(1, CustomMarkingArt.South, 15, 14, new Rgba32(30, 200, 30, 255));
        art.SetFrameTime(0, 150);
        art.SetFrameTime(1, 900);
        art.SetErased(CustomMarkingArt.South, 16, 16, true);
        art.SetErased(CustomMarkingArt.South, 16, 17, true);
        var hash = ServerCustomMarkingSystem.Hash(art);

        // The hash tells apart art that differs only in its timing or in what it erases. A plain still marking's
        // is the hash of its pixels, as it was before markings could have either.
        var slower = art.Clone();
        slower.SetFrameTime(1, 950);
        var whole = art.Clone();
        whole.ClearErase(CustomMarkingArt.South);
        var still = new CustomMarkingArt();
        still.SetPixel(0, CustomMarkingArt.South, 15, 14, new Rgba32(200, 30, 30, 255));
        Assert.Multiple(() =>
        {
            Assert.That(ServerCustomMarkingSystem.Hash(art.Clone()), Is.EqualTo(hash));
            Assert.That(ServerCustomMarkingSystem.Hash(slower), Is.Not.EqualTo(hash));
            Assert.That(ServerCustomMarkingSystem.Hash(whole), Is.Not.EqualTo(hash));
            Assert.That(ServerCustomMarkingSystem.Hash(still), Is.EqualTo(Convert.ToHexString(SHA256.HashData(still.Pixels)).ToLowerInvariant()));
        });

        CustomMarkingSaveResultEvent answer = null;
        void OnAnswer(CustomMarkingSaveResultEvent ev) => answer = ev;
        await client.WaitPost(() => cSystem.SaveAnswered += OnAnswer);

        // More frames than the server allows are refused.
        await server.WaitPost(() => server.CfgMan.SetCVar(CustomMarkingCVars.MaxFrames, 1));
        await client.WaitPost(() => cSystem.Save(0, "Too long", CustomMarkingPlacement.Skin, art));
        await WaitFor(pair, () => answer != null, "the server to answer the long save");
        Assert.That(answer.Error, Is.EqualTo("wf-custom-marking-error-invalid"));
        await server.WaitPost(() => server.CfgMan.SetCVar(CustomMarkingCVars.MaxFrames, CustomMarkingRules.MaxFrames));

        answer = null;
        await client.WaitPost(() => cSystem.Save(0, "Blinker", CustomMarkingPlacement.Skin, art));
        await WaitFor(pair, () => answer != null, "the server to answer the save");
        Assert.Multiple(() =>
        {
            Assert.That(answer.Error, Is.Null);
            Assert.That(answer.Entry?.Hash, Is.EqualTo(hash));
        });
        await client.WaitPost(() => cSystem.SaveAnswered -= OnAnswer);

        var stored = await server.ResolveDependency<IServerDbManager>().GetCustomMarkingArtAsync(hash);
        Assert.That(stored, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(CustomMarkingRules.UnpackFrameTimes(stored.FrameTimes), Is.EqualTo(new[] { 150, 900 }));
            Assert.That(stored.Erase, Is.EqualTo(art.Erase));
        });

        // A body wearing it: the client fetches the art by hash and gets all of it.
        var testMap = await pair.CreateTestMap();
        EntityUid body = default;
        await server.WaitPost(() =>
        {
            body = server.EntMan.SpawnEntity("MobHuman", testMap.GridCoords);
            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human")
                .WithCustomMarkings(new List<CustomMarking> { new(hash, CustomMarkingPlacement.Skin) });
            server.EntMan.System<SharedHumanoidAppearanceSystem>().LoadProfile(body, profile);
            server.PlayerMan.SetAttachedEntity(pair.Player!, body);
        });

        await pair.RunTicksSync(5);
        var clientBody = pair.ToClientUid(body);
        var sprites = client.System<SpriteSystem>();
        await WaitFor(pair, () => client.EntMan.TryGetComponent(clientBody, out SpriteComponent drawn)
                                  && sprites.LayerMapTryGet((clientBody, drawn), ClientCustomMarkingSystem.LayerKey(0), out _, false),
            "the client to draw the marking");

        await client.WaitAssertion(() =>
        {
            Assert.That(cSystem.TryReadArt(hash, out var read), Is.True);
            Assert.That(read.Same(art), Is.True, "the art arrives as it was drawn: frames, times and what it erases");

            var sprite = client.EntMan.GetComponent<SpriteComponent>(clientBody);
            Assert.That(sprites.LayerMapTryGet((clientBody, sprite), ClientCustomMarkingSystem.LayerKey(0), out var index, false), Is.True);
            var layer = (SpriteComponent.Layer) sprite[index];
            Assert.That(layer.ActualRsi!.TryGetState(Content.Client._Pirate.CustomMarkings.CustomMarkingResources.State, out var state), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(state.IsAnimated, Is.True, "the marking runs through its frames like any animated sprite");
                Assert.That(state.GetDelays(), Is.EqualTo(new[] { 0.15f, 0.9f }).Within(0.0001f));
                Assert.That(layer.AutoAnimated, Is.True);
            });

            // And the torso under it is drawn through the mask.
            Assert.That(sprites.LayerMapTryGet((clientBody, sprite), HumanoidVisualLayers.Chest, out var chest, false), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(((SpriteComponent.Layer) sprite[chest]).ShaderPrototype, Is.EqualTo(ClientCustomMarkingSystem.EraseShader));
                Assert.That(sprites.LayerMapTryGet((clientBody, sprite), ClientCustomMarkingSystem.EraseKey(0), out _, false), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>With erasing turned off on the server, a mask is dropped from what is saved and the drawing kept.</summary>
    [Test]
    public async Task EraseTurnedOffTest()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var cSystem = client.System<ClientCustomMarkingSystem>();

        await server.WaitPost(() => server.CfgMan.SetCVar(CustomMarkingCVars.EraseBody, false));
        await pair.RunTicksSync(5);

        CustomMarkingSaveResultEvent answer = null;
        void OnAnswer(CustomMarkingSaveResultEvent ev) => answer = ev;
        await client.WaitPost(() => cSystem.SaveAnswered += OnAnswer);

        // A marking that only erases is then nothing at all.
        var eraser = new CustomMarkingArt();
        eraser.SetErased(CustomMarkingArt.South, 16, 16, true);
        await client.WaitPost(() => cSystem.Save(0, "Eraser", CustomMarkingPlacement.Skin, eraser));
        await WaitFor(pair, () => answer != null, "the server to answer the erasing save");
        Assert.That(answer.Error, Is.EqualTo("wf-custom-marking-error-blank"));

        var art = eraser.Clone();
        art.SetPixel(0, CustomMarkingArt.South, 15, 14, new Rgba32(200, 30, 30, 255));
        var plain = art.Clone();
        plain.ClearErase(CustomMarkingArt.South);

        answer = null;
        await client.WaitPost(() => cSystem.Save(0, "Mark", CustomMarkingPlacement.Skin, art));
        await WaitFor(pair, () => answer != null, "the server to answer the save");
        await client.WaitPost(() => cSystem.SaveAnswered -= OnAnswer);
        Assert.Multiple(() =>
        {
            Assert.That(answer.Error, Is.Null);
            Assert.That(answer.Entry?.Hash, Is.EqualTo(ServerCustomMarkingSystem.Hash(plain)), "saved as the drawing alone");
        });

        var stored = await server.ResolveDependency<IServerDbManager>().GetCustomMarkingArtAsync(answer.Entry!.Value.Hash);
        Assert.That(stored, Is.Not.Null);
        Assert.That(stored.Erase, Is.Null);

        await pair.CleanReturnAsync();
    }

    private static async Task WaitFor(TestPair pair, Func<bool> condition, string what)
    {
        for (var i = 0; i < MaxWaitTicks; i++)
        {
            if (condition())
                return;

            await pair.RunTicksSync(1);
        }

        Assert.That(condition(), Is.True, $"Timed out waiting for {what}.");
    }

    private sealed class ProfileSavePause : SaveChangesInterceptor
    {
        public string? HashToPause;
        public readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (HashToPause != null && eventData.Context!.ChangeTracker.Entries<Profile>()
                    .Any(entry => entry.Entity.CustomMarkings.Contains(HashToPause, StringComparison.Ordinal)))
            {
                Started.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }

            return result;
        }
    }

    private static ServerDbSqlite GetDb(RobustIntegrationTest.ServerIntegrationInstance server,
        SaveChangesInterceptor? interceptor = null, bool concurrent = false)
    {
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var opsLog = server.ResolveDependency<ILogManager>().GetSawmill("db.ops");
        var builder = new DbContextOptionsBuilder<SqliteServerDbContext>();
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        builder.UseSqlite(conn);
        if (interceptor != null)
            builder.AddInterceptors(interceptor);
        return new ServerDbSqlite(() => builder.Options, !concurrent, cfg, !concurrent, opsLog);
    }
}
