// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections;
using System.Reflection;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.Players.PlayTimeTracking;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests._Pirate.Newbies;

internal static class NoviceTestPlaytime
{
    public static void Set(PlayTimeTrackingManager tracking, ICommonSession session, double hours)
    {
        tracking.FlushTracker(session);
        tracking.GetTrackerTimes(session)[PlayTimeTrackingShared.TrackerOverall] = TimeSpan.FromHours(hours);
        tracking.QueueSendTimers(session);
    }

    // Model the asynchronous database-loading window without adding test-only production APIs.
    // Callers restore this inside the same server assertion so the manager itself never ticks unloaded data.
    public static void SetLoaded(PlayTimeTrackingManager tracking, ICommonSession session, bool loaded)
    {
        var field = typeof(PlayTimeTrackingManager).GetField("_playTimeData", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var data = ((IDictionary) field.GetValue(tracking)!)[session]!;
        data.GetType().GetField("Initialized")!.SetValue(data, loaded);
    }
}
