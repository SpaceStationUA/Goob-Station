// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Linq;
using Content.Goobstation.Server.StationEvents.Components;
using Content.Goobstation.Server.StationEvents.Metric;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Server.StationEvents.GameDirector;

public sealed partial class GameDirectorSystem
{
    // Escape braces because the log formatter treats them as template tokens.
    private void ConsoleLog(string message)
    {
        Log.Info($"t={RoundTime()} {message}".Replace("{", "{{").Replace("}", "}}"));
    }

    private void ConsoleLogPick(string kind,
        EntProtoId picked,
        GameDirectorComponent scheduler,
        StoryBeatPrototype beat,
        ChaosMetrics goal,
        ChaosMetrics chaos,
        PlayerCount count)
    {
        Log.Info($"t={RoundTime()} pick kind={kind} event={picked} story={scheduler.CurrentStoryName.Id ?? "-"} beat={beat.ID} " +
                 $"chaos=[{ShortChaos(chaos)}] goal=[{ShortChaos(goal)}] " +
                 $"budget={scheduler.DestructionBudget.ToString("0.##", CultureInfo.InvariantCulture)} " +
                 $"players={count.Players} ingame={count.InGame} ghosts={count.Ghosts}");
    }

    private string RoundTime()
    {
        return GameTicker.RoundDuration().ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
    }

    private static string ShortChaos(ChaosMetrics chaos)
    {
        return string.Join(" ", chaos.ChaosDict
            .Where(p => Math.Abs(p.Value) >= 0.5)
            .Select(p => $"{p.Key}:{p.Value.ToString("0", CultureInfo.InvariantCulture)}"));
    }
}
