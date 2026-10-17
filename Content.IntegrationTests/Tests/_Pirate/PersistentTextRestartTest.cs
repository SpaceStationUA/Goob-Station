// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server._Pirate.PersistentText;
using Content.Shared._Pirate.PersistentText;
using Content.Shared.CCVar;
using Robust.Server;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests._Pirate
{
    [TestFixture]
    [TestOf(typeof(PersistentTextSystem))]
    public sealed class PersistentTextRestartTest : RobustIntegrationTest
    {
        /// <summary>
        ///     Regression test for the round-restart server freeze: with a resolvable diary
        ///     present, PersistentTextSystem.OnRoundRestartCleanup used to save synchronously
        ///     via PersistSnapshotsAsync(...).GetAwaiter().GetResult() on the game thread.
        ///     The save hops to the thread pool and posts its continuation back to the game
        ///     thread's synchronization context, which is blocked waiting for exactly that
        ///     continuation - a permanent deadlock that froze the server between
        ///     "Restarting round!" and "Wiping all minds" and hung every connected client.
        /// </summary>
        [Test]
        public async Task RoundRestartCleanupSavesDiaryWithoutDeadlocking()
        {
            const string owner = "persistent-text-restart-test";

            var options = new ServerIntegrationOptions
            {
                ContentStart = true,
                ContentAssemblies = PoolManager.GetAssemblies(client: false, includePoolAssembly: false),
                Pool = false,
                FailureLogLevel = LogLevel.Fatal,
                Options = new ServerOptions
                {
                    LoadConfigAndUserData = false,
                    LoadContentResources = true,
                },
            };

            foreach (var (cvar, value) in PoolManager.TestCvars)
            {
                options.CVarOverrides[cvar] = value;
            }

            // Real ticker so RestartRound() actually restarts, lobby mode so the restart
            // never reaches StartRound/map loading (not the code under test here), and a
            // production-like database: with the synchronous in-memory DB every call
            // completes inline on the caller and the sync-over-async deadlock cannot
            // manifest at all.
            options.CVarOverrides[CCVars.GameDummyTicker.Name] = "false";
            options.CVarOverrides[CCVars.GameLobbyEnabled.Name] = "true";
            options.CVarOverrides[CCVars.DatabaseSynchronous.Name] = "false";

            using var server = StartServer(options);
            await server.WaitIdleAsync();

            await server.WaitAssertion(() =>
            {
                var diary = server.EntMan.SpawnEntity("DiaryLoadout", MapCoordinates.Nullspace);
                server.EntMan.GetComponent<PersistentTextComponent>(diary).OwnerId = owner;
            });

            var db = server.ResolveDependency<IServerDbManager>();
            var ticker = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<GameTicker>();

            // RestartRound runs on the server instance's own thread with the Robust
            // synchronization context set - exactly like the production game thread. The
            // old implementation blocked that thread forever: the synchronous wait needs
            // the thread back to run the very DB continuation it is waiting for.
            var restart = server.WaitPost(() => ticker.RestartRound());
            var finished = await Task.WhenAny(restart, Task.Delay(TimeSpan.FromSeconds(60)));

            if (!ReferenceEquals(finished, restart))
            {
                // The instance thread is permanently blocked, so its WaitIdleAsync can never
                // complete and the RobustIntegrationTest fixture teardown would hang on it.
                // The tracking list is private in the base class; unregister the instance
                // there before failing so the run still exits and reports cleanly.
                var notPooled = typeof(RobustIntegrationTest)
                    .GetField("_notPooledInstances", BindingFlags.Instance | BindingFlags.NonPublic);
                if (notPooled?.GetValue(this) is System.Collections.IList instances)
                    instances.Remove(server);

                Assert.Fail(
                    "RestartRound() with a persistent diary deadlocked the server thread: " +
                    "PersistentTextSystem.OnRoundRestartCleanup waits synchronously for a DB " +
                    "continuation posted back to the same blocked thread.");
            }

            await restart; // Propagate any exception thrown by the restart itself.

            var snapshot = await db.GetPersistentTextSnapshotAsync("profile", null, owner, "diary");
            Assert.That(snapshot, Is.Not.Null, "Diary content was not persisted by the round restart cleanup.");
        }
    }
}
