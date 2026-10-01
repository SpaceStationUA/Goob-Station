// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Pirate.Server.TV;
using Content.Pirate.Shared.TV;
using Content.Shared.DeviceLinking;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Pirate.TV;

[TestFixture]
public sealed class PirateTvLinkIntegrationTest
{
    /// <summary>
    ///     A linked mirror follows the master's channel and queue, and
    ///     unlinking resets it to off.
    /// </summary>
    [Test]
    public async Task LinkedTvMirrorsThenResetsOnUnlink()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var link = server.System<SharedDeviceLinkSystem>();
        var tv = server.System<PirateTvSystem>();

        await server.WaitAssertion(() =>
        {
            var coords = MapCoordinates.Nullspace;

            var master = entMan.SpawnEntity("ComputerTelevision", coords);
            var mirror = entMan.SpawnEntity("ComputerTelevision", coords);

            var masterComp = entMan.GetComponent<PirateTvComponent>(master);
            var mirrorComp = entMan.GetComponent<PirateTvComponent>(mirror);

            // Link master broadcast -> mirror receive via the device linker.
            link.SaveLinks(null, master, mirror, [("PirateTvBroadcast", "PirateTvReceive")]);

            Assert.That(mirrorComp.IsMirror, Is.True, "Mirror did not register its source.");
            Assert.That(mirrorComp.Source, Is.EqualTo(entMan.GetNetEntity(master)));

            // Pick on the master: the mirror should carry the same state.
            tv.Pick(master, masterComp, "https://www.youtube.com/watch?v=abcdefghijk", 1, "YouTube", "Test");

            Assert.That(masterComp.Queue.Count, Is.EqualTo(1), "Master queue empty after pick.");
            Assert.That(mirrorComp.Queue.Count, Is.EqualTo(1), "Mirror did not receive the queue.");
            Assert.That(mirrorComp.Url, Is.EqualTo(masterComp.Url), "Mirror did not receive the URL.");
            Assert.That(mirrorComp.Now, Is.EqualTo(masterComp.Now), "Mirror did not receive the index.");

            // Unlink: the mirror resets to off/empty.
            link.RemoveSinkFromSource(master, mirror);
            Assert.That(mirrorComp.IsMirror, Is.False, "Mirror still linked after unlink.");
            Assert.That(mirrorComp.Url, Is.EqualTo(""), "Unlinked mirror did not reset its URL.");
            Assert.That(mirrorComp.Queue.Count, Is.EqualTo(0), "Unlinked mirror did not clear its queue.");

            // The master is untouched.
            Assert.That(masterComp.Queue.Count, Is.EqualTo(1), "Unlink cleared the master's queue.");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Chains are allowed: 1→2→3 mirrors state all the way down, and a
    ///     cycle (3→1) is refused.
    /// </summary>
    [Test]
    public async Task ChainMirrorsAndCyclesAreRefused()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var link = server.System<SharedDeviceLinkSystem>();
        var tv = server.System<PirateTvSystem>();

        await server.WaitAssertion(() =>
        {
            var coords = MapCoordinates.Nullspace;

            var a = entMan.SpawnEntity("ComputerTelevision", coords);
            var b = entMan.SpawnEntity("ComputerTelevision", coords);
            var c = entMan.SpawnEntity("ComputerTelevision", coords);

            link.SaveLinks(null, a, b, [("PirateTvBroadcast", "PirateTvReceive")]);
            link.SaveLinks(null, b, c, [("PirateTvBroadcast", "PirateTvReceive")]);

            var cComp = entMan.GetComponent<PirateTvComponent>(c);
            Assert.That(cComp.IsMirror, Is.True, "Chained TV did not register a source.");

            // Pick on the root: the whole chain receives it.
            tv.Pick(a, entMan.GetComponent<PirateTvComponent>(a),
                "https://www.youtube.com/watch?v=abcdefghijk", 1, "YouTube", "Test");

            Assert.That(entMan.GetComponent<PirateTvComponent>(b).Queue.Count, Is.EqualTo(1),
                "Middle TV did not receive the state.");
            Assert.That(cComp.Queue.Count, Is.EqualTo(1), "Chained TV did not receive the state.");

            // Unlinking the middle from the root keeps the middle→(chained) link.
            link.RemoveSinkFromSource(a, b);
            Assert.That(entMan.GetComponent<PirateTvComponent>(b).IsMirror, Is.False,
                "Unlinked middle TV still reports a source.");
            Assert.That(cComp.IsMirror, Is.True, "Chained TV lost its own link.");
            Assert.That(cComp.Source, Is.EqualTo(entMan.GetNetEntity(b)),
                "Chained TV should now follow the middle TV.");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Closing the loop back to the root is refused: the chain stays
    ///     acyclic and the root stays a root.
    /// </summary>
    [Test]
    public async Task CycleBackToTheRootIsRefused()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var link = server.System<SharedDeviceLinkSystem>();

        await server.WaitAssertion(() =>
        {
            var coords = MapCoordinates.Nullspace;
            var a = entMan.SpawnEntity("ComputerTelevision", coords);
            var b = entMan.SpawnEntity("ComputerTelevision", coords);
            var c = entMan.SpawnEntity("ComputerTelevision", coords);

            link.SaveLinks(null, a, b, [("PirateTvBroadcast", "PirateTvReceive")]);
            link.SaveLinks(null, b, c, [("PirateTvBroadcast", "PirateTvReceive")]);

            // 3 -> 1 would make a loop. Must be refused.
            link.SaveLinks(null, c, a, [("PirateTvBroadcast", "PirateTvReceive")]);

            var aComp = entMan.GetComponent<PirateTvComponent>(a);
            Assert.That(aComp.IsMirror, Is.False, "Cycle link turned the root into a mirror.");
            Assert.That(entMan.GetComponent<PirateTvComponent>(b).Source,
                Is.EqualTo(entMan.GetNetEntity(a)), "Refused cycle disturbed the existing chain.");
            Assert.That(entMan.GetComponent<PirateTvComponent>(c).Source,
                Is.EqualTo(entMan.GetNetEntity(b)), "Refused cycle disturbed the existing chain.");
            Assert.That(entMan.GetComponent<DeviceLinkSinkComponent>(a).LinkedSources, Does.Not.Contain(c),
                "Refused cycle left a source without a reciprocal link.");
            entMan.DeleteEntity(c);
            Assert.That(entMan.GetComponent<DeviceLinkSinkComponent>(a).LinkedSources, Is.Empty,
                "Deleting a refused source left a stale link on the root.");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Deleting the middle of a 1→2→3 chain re-parents the tail onto the
    ///     root instead of dropping it. Playback and the reciprocal DeviceLink
    ///     graph must agree, including when the new root is deleted afterwards.
    /// </summary>
    [Test]
    public async Task DeletingTheMiddleOfAChainReparentsTheTail()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var link = server.System<SharedDeviceLinkSystem>();
        var tv = server.System<PirateTvSystem>();

        await server.WaitAssertion(() =>
        {
            var coords = MapCoordinates.Nullspace;
            var a = entMan.SpawnEntity("ComputerTelevision", coords);
            var b = entMan.SpawnEntity("ComputerTelevision", coords);
            var c = entMan.SpawnEntity("ComputerTelevision", coords);

            link.SaveLinks(null, a, b, [("PirateTvBroadcast", "PirateTvReceive")]);
            link.SaveLinks(null, b, c, [("PirateTvBroadcast", "PirateTvReceive")]);

            tv.Pick(a, entMan.GetComponent<PirateTvComponent>(a),
                "https://www.youtube.com/watch?v=abcdefghijk", 1, "YouTube", "Test");

            var cComp = entMan.GetComponent<PirateTvComponent>(c);
            Assert.That(cComp.Queue.Count, Is.EqualTo(1), "Precondition: tail received the state.");

            entMan.DeleteEntity(b);

            Assert.That(cComp.Source, Is.EqualTo(entMan.GetNetEntity(a)),
                "Tail was not re-parented onto the root when the middle died.");
            Assert.That(cComp.Queue.Count, Is.EqualTo(1),
                "Tail lost its queue when the middle died.");
            Assert.That(cComp.Url, Is.EqualTo(entMan.GetComponent<PirateTvComponent>(a).Url),
                "Tail is not showing the root's channel after re-parenting.");
            Assert.That(entMan.GetComponent<DeviceLinkSinkComponent>(c).LinkedSources, Is.EquivalentTo(new[] { a }),
                "Tail's DeviceLink sources do not match its new parent.");
            Assert.That(link.GetLinkedSinks(a, "PirateTvBroadcast"), Does.Contain(c),
                "Root has no reciprocal link to the re-parented tail.");

            entMan.DeleteEntity(a);
            Assert.That(cComp.IsMirror, Is.False, "Deleting the new root did not detach the tail.");
            Assert.That(cComp.Queue, Is.Empty, "Deleting the new root did not clear playback.");
            Assert.That(entMan.GetComponent<DeviceLinkSinkComponent>(c).LinkedSources, Is.Empty,
                "Deleting the new root left a stale DeviceLink source.");
            entMan.DeleteEntity(c);
        });

        await pair.CleanReturnAsync();
    }
}
