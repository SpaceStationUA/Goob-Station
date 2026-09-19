// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Threading;
using Robust.Shared.IoC;
using Timer = Robust.Shared.Timing.Timer;

// Pirate: engine 277 removed the obsolete TimerComponent/TimerExtensions
// (space-wizards engine #6475). This shim keeps the legacy `uid.SpawnTimer`
// call-sites compiling; behavior preserved. Migrate callsites to update-loop
// idioms over time.

namespace Robust.Shared.GameObjects
{
    public static class SpawnTimerExtensions
    {
        private static IEntityManager? _entMan;

        private static IEntityManager EntMan => _entMan ??= IoCManager.Resolve<IEntityManager>();

        public static void SpawnTimer(this EntityUid entity, int milliseconds, Action onFired, CancellationToken cancellationToken = default)
        {
            Timer.Spawn(milliseconds, () =>
            {
                if (EntMan.EntityExists(entity))
                    onFired();
            }, cancellationToken);
        }

        public static void SpawnTimer(this EntityUid entity, TimeSpan duration, Action onFired, CancellationToken cancellationToken = default)
        {
            SpawnTimer(entity, (int)duration.TotalMilliseconds, onFired, cancellationToken);
        }

        public static void SpawnRepeatingTimer(this EntityUid entity, TimeSpan duration, Action onFired, CancellationToken cancellationToken = default)
        {
            Timer.SpawnRepeating((int)duration.TotalMilliseconds, () =>
            {
                if (EntMan.EntityExists(entity))
                    onFired();
            }, cancellationToken);
        }
    }
}
